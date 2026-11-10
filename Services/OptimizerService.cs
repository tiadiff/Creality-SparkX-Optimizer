using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using SparkXOptimizer.Models;

namespace SparkXOptimizer.Services;

public class OptimizerService
{
    private static readonly string[] DefaultProgramFilesPaths =
    [
        @"C:\Program Files\Creality\Creality Print 7.3\CrealityPrint.exe",
        @"C:\Program Files\Creality\Creality Print\CrealityPrint.exe"
    ];

    public string GetCrealityExePath()
    {
        foreach (var path in DefaultProgramFilesPaths)
        {
            if (File.Exists(path)) return path;
        }

        var running = Process.GetProcessesByName("CrealityPrint");
        if (running.Length > 0)
        {
            try
            {
                var mainMod = running[0].MainModule?.FileName;
                if (!string.IsNullOrEmpty(mainMod) && File.Exists(mainMod))
                    return mainMod;
            }
            catch { }
        }

        return DefaultProgramFilesPaths[0];
    }

    public string GetAppDataPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string v73 = Path.Combine(appData, "Creality", "Creality Print", "7.3");
        if (Directory.Exists(v73)) return v73;

        string generic = Path.Combine(appData, "Creality", "Creality Print");
        return Directory.Exists(generic) ? generic : v73;
    }

    public string GetConfigPath()
    {
        return Path.Combine(GetAppDataPath(), "Creality.conf");
    }

    public OptimizationStatus DetectStatus()
    {
        var status = new OptimizationStatus
        {
            CrealityExePath = GetCrealityExePath(),
            AppDataDir = GetAppDataPath(),
            ConfigFilePath = GetConfigPath(),
            CrealityInstalled = File.Exists(GetCrealityExePath()) || Directory.Exists(GetAppDataPath())
        };

        // Process check
        var processes = Process.GetProcessesByName("CrealityPrint");
        if (processes.Length > 0)
        {
            status.CrealityRunning = true;
            status.RunningPid = processes[0].Id;
        }

        // Config check
        if (File.Exists(status.ConfigFilePath))
        {
            try
            {
                string text = File.ReadAllText(status.ConfigFilePath, Encoding.UTF8);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.TryGetProperty("app", out var app))
                {
                    if (app.TryGetProperty("show_home_page", out var shp))
                        status.HomePageEnabled = shp.ValueKind == JsonValueKind.True || shp.GetString() == "true" || shp.GetString() == "1";
                    if (app.TryGetProperty("show_splash_screen", out var sss))
                        status.SplashEnabled = sss.ValueKind == JsonValueKind.True || sss.GetString() == "true" || sss.GetString() == "1";
                    if (app.TryGetProperty("show_daily_tips", out var sdt))
                        status.DailyTipsEnabled = sdt.ValueKind == JsonValueKind.True || sdt.GetString() == "true" || sdt.GetString() == "1";
                    if (app.TryGetProperty("sync_user_preset", out var sup))
                        status.CloudSyncEnabled = sup.ValueKind == JsonValueKind.True || sup.GetString() == "true" || sup.GetString() == "1";
                    if (app.TryGetProperty("auto_calculate", out var ac))
                        status.AutoCalculateEnabled = ac.ValueKind == JsonValueKind.True || ac.GetString() == "true" || ac.GetString() == "1";
                    if (app.TryGetProperty("enable_preview_mem_opt", out var pmo))
                        status.PreviewMemOptEnabled = pmo.ValueKind == JsonValueKind.True || pmo.GetString() == "true" || pmo.GetString() == "1";
                }
            }
            catch { }
        }

        // Kinematics / Helical Arc Crash Check
        string machinesDir = Path.Combine(status.AppDataDir, "system", "Creality", "machine");
        if (Directory.Exists(machinesDir))
        {
            var jsonFiles = Directory.GetFiles(machinesDir, "*.json");
            foreach (var file in jsonFiles)
            {
                try
                {
                    string content = File.ReadAllText(file, Encoding.UTF8);
                    if (content.Contains("\"xy_step_dist\": \"0.0121875\"") ||
                        content.Contains("I0.86 J0.86 P1") ||
                        content.Contains("G2 Z") && content.Contains("P1"))
                    {
                        status.KinematicsBugDetected = true;
                        status.VulnerableProfilesCount++;
                    }
                    else if (content.Contains("\"xy_step_dist\": \"0\"") && Path.GetFileName(file).StartsWith("SPARKX"))
                    {
                        status.PatchedProfilesCount++;
                    }
                }
                catch { }
            }
        }

        // Cache sizes
        string tempRoot = Path.GetTempPath();
        string webviewDir = Path.Combine(tempRoot, "slicer_webview_userdata");
        if (Directory.Exists(webviewDir))
        {
            status.WebViewJunkBytes = GetDirectorySize(webviewDir);
        }

        string modelDir = Path.Combine(tempRoot, "crealityprint_model");
        if (Directory.Exists(modelDir))
        {
            status.ModelTempJunkBytes = GetDirectorySize(modelDir);
        }

        string logDir = Path.Combine(status.AppDataDir, "log");
        if (Directory.Exists(logDir))
        {
            status.LogJunkBytes = GetDirectorySize(logDir, "*.dmp") + GetDirectorySize(logDir, "*.log.*");
        }

        // GPU Preference check
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences");
            if (key != null)
            {
                var val = key.GetValue(status.CrealityExePath) as string;
                if (!string.IsNullOrEmpty(val) && val.Contains("GpuPreference=2"))
                {
                    status.HighPerformanceGpuActive = true;
                }
            }
        }
        catch { }

        // Permanent High Priority check (IFEO)
        status.PermanentHighPriorityActive = CheckPermanentHighPriority();

        // Latest backup check
        string backupDir = Path.Combine(status.AppDataDir, "SparkX_Backups");
        if (Directory.Exists(backupDir))
        {
            var backups = Directory.GetDirectories(backupDir).OrderByDescending(Directory.GetCreationTime).ToArray();
            if (backups.Length > 0)
            {
                status.LatestBackupPath = backups[0];
                status.LatestBackupTime = Directory.GetCreationTime(backups[0]);
            }
        }

        return status;
    }

    public bool CheckPermanentHighPriority()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\CrealityPrint.exe\PerfOptions");
            if (key != null)
            {
                var val = key.GetValue("CpuPriorityClass");
                if (val is int intVal && intVal == 3) // 3 = High Priority
                    return true;
            }
        }
        catch { }
        return false;
    }

    public bool SetPermanentHighPriority(bool enable, Action<string> log)
    {
        try
        {
            string command = enable
                ? @"add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\CrealityPrint.exe\PerfOptions"" /v CpuPriorityClass /t REG_DWORD /d 3 /f"
                : @"delete ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\CrealityPrint.exe\PerfOptions"" /f";

            log($"[PRIORITY] Requesting Administrator privilege to configure Windows Kernel Process Priority...");
            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = command,
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var proc = Process.Start(psi);
            proc?.WaitForExit(5000);

            bool success = CheckPermanentHighPriority() == enable;
            if (success)
            {
                log(enable
                    ? "[SUCCESS] Permanent High Priority applied via Windows IFEO (CrealityPrint will always launch with High Priority)."
                    : "[SUCCESS] Permanent High Priority removed.");
                return true;
            }
        }
        catch (Exception ex)
        {
            log($"[WARNING] Could not set permanent IFEO priority: {ex.Message}");
        }

        return false;
    }

    public bool CreateHighPriorityDesktopShortcut(Action<string> log)
    {
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktop, "Creality Print (High Priority).lnk");
            string exePath = GetCrealityExePath();

            if (!File.Exists(exePath))
            {
                log("[ERROR] CrealityPrint.exe not found to create shortcut.");
                return false;
            }

            // Create a .bat launcher or powershell shortcut
            string launcherBat = Path.Combine(GetAppDataPath(), "Launch_High_Priority.bat");
            string batContent = $"@echo off\r\nstart \"\" /high \"{exePath}\" %*\r\n";
            File.WriteAllText(launcherBat, batContent);

            // Create WScript shortcut to launcher with Creality Print icon
            string vbsScript = $@"
Set oWS = WScript.CreateObject(""WScript.Shell"")
sLinkFile = ""{shortcutPath}""
Set oLink = oWS.CreateShortcut(sLinkFile)
oLink.TargetPath = ""{launcherBat}""
oLink.Arguments = """"
oLink.Description = ""Creality Print 7.3 - High Priority Launcher""
oLink.IconLocation = ""{exePath}, 0""
oLink.WindowStyle = 7
oLink.WorkingDirectory = ""{Path.GetDirectoryName(exePath)}""
oLink.Save
";
            string tempVbs = Path.Combine(Path.GetTempPath(), "create_shortcut.vbs");
            File.WriteAllText(tempVbs, vbsScript);

            var psi = new ProcessStartInfo("wscript.exe", $"\"{tempVbs}\"") { UseShellExecute = true };
            var p = Process.Start(psi);
            p?.WaitForExit(3000);
            File.Delete(tempVbs);

            log($"[SHORTCUT] Created 'Creality Print (High Priority)' shortcut on your Desktop.");
            return true;
        }
        catch (Exception ex)
        {
            log($"[WARNING] Failed to create desktop shortcut: {ex.Message}");
            return false;
        }
    }

    public void CloseOrKillCrealityPrint(Action<string> log)
    {
        var processes = Process.GetProcessesByName("CrealityPrint");
        if (processes.Length > 0)
        {
            log($"Terminating {processes.Length} running CrealityPrint instance(s)...");
            foreach (var p in processes)
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(3000);
                    log($"Process PID {p.Id} terminated successfully.");
                }
                catch (Exception ex)
                {
                    log($"Warning: Could not terminate PID {p.Id}: {ex.Message}");
                }
            }
        }
    }

    public string CreateBackup(Action<string> log)
    {
        string appData = GetAppDataPath();
        string backupRoot = Path.Combine(appData, "SparkX_Backups");
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string destFolder = Path.Combine(backupRoot, $"Backup_{timestamp}");
        Directory.CreateDirectory(destFolder);

        string conf = GetConfigPath();
        if (File.Exists(conf))
        {
            File.Copy(conf, Path.Combine(destFolder, "Creality.conf"), true);
            log($"[BACKUP] Saved Creality.conf to {destFolder}");
        }

        string machinesDir = Path.Combine(appData, "system", "Creality", "machine");
        if (Directory.Exists(machinesDir))
        {
            string backupMachines = Path.Combine(destFolder, "machine");
            Directory.CreateDirectory(backupMachines);
            foreach (var f in Directory.GetFiles(machinesDir, "*.json"))
            {
                File.Copy(f, Path.Combine(backupMachines, Path.GetFileName(f)), true);
            }
            log($"[BACKUP] Saved {Directory.GetFiles(machinesDir, "*.json").Length} machine profiles.");
        }

        return destFolder;
    }

    public bool RestoreBackup(Action<string> log)
    {
        string appData = GetAppDataPath();
        string backupRoot = Path.Combine(appData, "SparkX_Backups");
        if (!Directory.Exists(backupRoot))
        {
            log("[RESTORE] No backup folder found.");
            return false;
        }

        var backups = Directory.GetDirectories(backupRoot).OrderByDescending(Directory.GetCreationTime).ToArray();
        if (backups.Length == 0)
        {
            log("[RESTORE] No backups available.");
            return false;
        }

        string latest = backups[0];
        log($"[RESTORE] Restoring from: {Path.GetFileName(latest)}...");

        CloseOrKillCrealityPrint(log);

        string confBackup = Path.Combine(latest, "Creality.conf");
        if (File.Exists(confBackup))
        {
            File.Copy(confBackup, GetConfigPath(), true);
            log("[RESTORE] Restored Creality.conf.");
        }

        string machinesBackup = Path.Combine(latest, "machine");
        if (Directory.Exists(machinesBackup))
        {
            string destMachines = Path.Combine(appData, "system", "Creality", "machine");
            Directory.CreateDirectory(destMachines);
            foreach (var f in Directory.GetFiles(machinesBackup, "*.json"))
            {
                File.Copy(f, Path.Combine(destMachines, Path.GetFileName(f)), true);
            }
            log("[RESTORE] Restored machine profiles.");
        }

        log("[SUCCESS] Restore completed successfully.");
        return true;
    }

    public long CleanJunk(Action<string> log)
    {
        long freedBytes = 0;
        string tempRoot = Path.GetTempPath();

        // 1. slicer_webview_userdata
        string webviewDir = Path.Combine(tempRoot, "slicer_webview_userdata");
        if (Directory.Exists(webviewDir))
        {
            log("[CLEAN] Analyzing and purging slicer_webview_userdata...");
            long size = GetDirectorySize(webviewDir);
            try
            {
                Directory.Delete(webviewDir, recursive: true);
                freedBytes += size;
                log($"[CLEAN] Purged WebView2 cache: {FormatBytes(size)} freed.");
            }
            catch (Exception ex)
            {
                log($"[CLEAN] Partial purge of WebView2 cache: {ex.Message}");
                freedBytes += SafeDeleteDirectoryContents(webviewDir);
            }
        }

        // 2. crealityprint_model temp cache
        string modelDir = Path.Combine(tempRoot, "crealityprint_model");
        if (Directory.Exists(modelDir))
        {
            long size = GetDirectorySize(modelDir);
            try
            {
                Directory.Delete(modelDir, recursive: true);
                freedBytes += size;
                log($"[CLEAN] Purged stale model/slice cache: {FormatBytes(size)} freed.");
            }
            catch
            {
                freedBytes += SafeDeleteDirectoryContents(modelDir);
            }
        }

        // 3. Old crash dumps & logs
        string logDir = Path.Combine(GetAppDataPath(), "log");
        if (Directory.Exists(logDir))
        {
            var dmpFiles = Directory.GetFiles(logDir, "*.dmp");
            foreach (var dmp in dmpFiles)
            {
                try
                {
                    long len = new FileInfo(dmp).Length;
                    File.Delete(dmp);
                    freedBytes += len;
                }
                catch { }
            }

            var oldLogs = Directory.GetFiles(logDir, "*.log.*")
                .Where(f => (DateTime.Now - File.GetLastWriteTime(f)).TotalDays > 3);
            foreach (var oldLog in oldLogs)
            {
                try
                {
                    long len = new FileInfo(oldLog).Length;
                    File.Delete(oldLog);
                    freedBytes += len;
                }
                catch { }
            }
            log("[CLEAN] Cleared stale crash dump files (.dmp).");
        }

        log($"[SUCCESS] Total disk space reclaimed: {FormatBytes(freedBytes)}.");
        return freedBytes;
    }

    public bool ToggleCloudSync(bool enable, Action<string> log)
    {
        string confPath = GetConfigPath();
        if (!File.Exists(confPath))
        {
            log("[WARNING] Creality.conf not found.");
            return false;
        }

        try
        {
            string jsonText = File.ReadAllText(confPath, Encoding.UTF8);
            string md5Line = string.Empty;
            int md5Idx = jsonText.LastIndexOf("# MD5 checksum", StringComparison.OrdinalIgnoreCase);
            if (md5Idx > 0)
            {
                md5Line = jsonText.Substring(md5Idx);
                jsonText = jsonText.Substring(0, md5Idx).TrimEnd();
            }

            var jsonNode = JsonNode.Parse(jsonText);
            var appNode = jsonNode?["app"]?.AsObject();
            if (appNode != null)
            {
                appNode["sync_user_preset"] = enable;
                appNode["sync_system_preset"] = enable;
            }

            string updatedJson = jsonNode!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            if (!string.IsNullOrEmpty(md5Line)) updatedJson += "\n" + md5Line + "\n";
            File.WriteAllText(confPath, updatedJson, Encoding.UTF8);

            log(enable
                ? "[CLOUD] Cloud preset sync enabled for next launch."
                : "[CLOUD] Cloud preset sync disabled at startup (Manual sync in-app is still available).");
            return true;
        }
        catch (Exception ex)
        {
            log($"[ERROR] Failed to update cloud sync setting: {ex.Message}");
            return false;
        }
    }

    public bool OptimizeConfig(
        bool skipHomePage,
        bool skipSplashAndTips,
        bool disableCloudSync,
        bool disableAutoCalculate,
        bool enableMemOpt,
        Action<string> log)
    {
        string confPath = GetConfigPath();
        if (!File.Exists(confPath))
        {
            log("[WARNING] Creality.conf not found. Optimization aborted.");
            return false;
        }

        try
        {
            string jsonText = File.ReadAllText(confPath, Encoding.UTF8);

            string md5Line = string.Empty;
            int md5Idx = jsonText.LastIndexOf("# MD5 checksum", StringComparison.OrdinalIgnoreCase);
            if (md5Idx > 0)
            {
                md5Line = jsonText.Substring(md5Idx);
                jsonText = jsonText.Substring(0, md5Idx).TrimEnd();
            }

            var jsonNode = JsonNode.Parse(jsonText);
            if (jsonNode == null)
            {
                log("[ERROR] Could not parse Creality.conf JSON.");
                return false;
            }

            var appNode = jsonNode["app"]?.AsObject();
            if (appNode != null)
            {
                if (skipHomePage)
                {
                    appNode["show_home_page"] = false;
                    log("[OPTIMIZE] Startup: Home Page webview disabled (Opens directly to 3D plate).");
                }

                if (skipSplashAndTips)
                {
                    appNode["show_splash_screen"] = false;
                    appNode["show_daily_tips"] = false;
                    appNode["show_hints"] = false;
                    log("[OPTIMIZE] Startup: Splash screen and Daily Tips disabled.");
                }

                if (disableCloudSync)
                {
                    appNode["sync_user_preset"] = false;
                    appNode["sync_system_preset"] = false;
                    log("[OPTIMIZE] Network: Automatic cloud preset check at startup disabled.");
                }

                if (disableAutoCalculate)
                {
                    appNode["auto_calculate"] = false;
                    appNode["auto_calculate_when_filament_change"] = false;
                    log("[OPTIMIZE] Slicer: Background auto-calculate disabled (Stops slicing race conditions & freezes).");
                }

                if (enableMemOpt)
                {
                    appNode["enable_preview_mem_opt"] = true;
                    appNode["enable_preview_mem_optimize"] = true;
                    log("[OPTIMIZE] Memory: 3D preview memory optimization enabled.");
                }
            }

            var printNode = jsonNode["print"]?.AsObject();
            if (printNode != null)
            {
                if (printNode.ContainsKey("timelapse"))
                {
                    printNode["timelapse"] = "0";
                    log("[OPTIMIZE] Print: Default timelapse set to 0 (Prevents broken arc injection).");
                }
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string updatedJson = jsonNode.ToJsonString(options);

            if (!string.IsNullOrEmpty(md5Line))
            {
                updatedJson += "\n" + md5Line + "\n";
            }

            File.WriteAllText(confPath, updatedJson, Encoding.UTF8);
            log("[SUCCESS] Creality.conf updated and saved.");
            return true;
        }
        catch (Exception ex)
        {
            log($"[ERROR] Failed to optimize Creality.conf: {ex.Message}");
            return false;
        }
    }

    public int FixKinematicsAndCrashes(Action<string> log)
    {
        string machinesDir = Path.Combine(GetAppDataPath(), "system", "Creality", "machine");
        if (!Directory.Exists(machinesDir))
        {
            log("[WARNING] Machine profiles directory not found.");
            return 0;
        }

        int patchedCount = 0;
        var files = Directory.GetFiles(machinesDir, "*.json");

        foreach (var file in files)
        {
            try
            {
                string content = File.ReadAllText(file, Encoding.UTF8);
                string newContent = content;

                if (newContent.Contains("\"xy_step_dist\": \"0.0121875\""))
                {
                    newContent = newContent.Replace("\"xy_step_dist\": \"0.0121875\"", "\"xy_step_dist\": \"0\"");
                }

                newContent = newContent.Replace(
                    "G2 Z{z_after_toolchange + 0.4} I0.86 J0.86 P1 F10000",
                    "G1 Z{z_after_toolchange + 0.4} F10000");

                newContent = newContent.Replace(
                    "G2 Z{position[2]+3} I0.86 J0.86 P1 F10000 ; spiral lift a little from second lift",
                    "G1 Z{position[2]+3} F10000 ; lift a little from second lift");

                newContent = newContent.Replace(
                    "M5001 G2 Z{layer_z + 0.4} I0.86 J0.86 P1 F20000 ; spiral lift a little",
                    "M5001 G1 Z{layer_z + 0.4} F20000 ; lift a little");

                if (newContent != content)
                {
                    File.WriteAllText(file, newContent, Encoding.UTF8);
                    patchedCount++;
                    log($"[PATCH] Patched kinematics in machine profile: {Path.GetFileName(file)}");
                }
            }
            catch (Exception ex)
            {
                log($"[ERROR] Failed to patch {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        log($"[SUCCESS] Kinematic divergence fix applied to {patchedCount} profile(s).");
        return patchedCount;
    }

    public bool ConfigureHighPerformanceGpu(Action<string> log)
    {
        string exePath = GetCrealityExePath();
        if (!File.Exists(exePath))
        {
            log($"[WARNING] CrealityPrint.exe not found at {exePath}.");
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences");
            if (key != null)
            {
                key.SetValue(exePath, "GpuPreference=2;");
                log($"[GPU] Registered High-Performance GPU preference for: {Path.GetFileName(exePath)}");
                return true;
            }
        }
        catch (Exception ex)
        {
            log($"[WARNING] Could not set GPU registry preference: {ex.Message}");
        }

        return false;
    }

    public bool LaunchCrealityPrint(bool boostPriority, Action<string> log)
    {
        string exePath = GetCrealityExePath();
        if (!File.Exists(exePath))
        {
            log($"[ERROR] CrealityPrint executable not found at: {exePath}");
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exePath)
            };

            var proc = Process.Start(psi);
            if (proc != null)
            {
                log($"[LAUNCH] Started Creality Print (PID {proc.Id}).");
                if (boostPriority)
                {
                    try
                    {
                        Thread.Sleep(500);
                        if (!proc.HasExited)
                        {
                            proc.PriorityClass = ProcessPriorityClass.High;
                            log("[LAUNCH] Set process CPU priority to High.");
                        }
                    }
                    catch { }
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            log($"[ERROR] Failed to launch Creality Print: {ex.Message}");
        }

        return false;
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
        return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
    }

    private static long GetDirectorySize(string path, string pattern = "*.*")
    {
        try
        {
            if (!Directory.Exists(path)) return 0;
            return Directory.GetFiles(path, pattern, SearchOption.AllDirectories)
                .Select(f => { try { return new FileInfo(f).Length; } catch { return 0L; } })
                .Sum();
        }
        catch { return 0; }
    }

    private static long SafeDeleteDirectoryContents(string dirPath)
    {
        long freed = 0;
        try
        {
            var dir = new DirectoryInfo(dirPath);
            foreach (var file in dir.GetFiles())
            {
                try
                {
                    long len = file.Length;
                    file.Delete();
                    freed += len;
                }
                catch { }
            }

            foreach (var subDir in dir.GetDirectories())
            {
                try
                {
                    freed += SafeDeleteDirectoryContents(subDir.FullName);
                    subDir.Delete(true);
                }
                catch { }
            }
        }
        catch { }
        return freed;
    }
}
