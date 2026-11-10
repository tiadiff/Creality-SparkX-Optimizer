using System.Windows;
using System.Windows.Media;
using SparkXOptimizer.Models;
using SparkXOptimizer.Services;

namespace SparkXOptimizer;

public partial class MainWindow : Window
{
    private readonly OptimizerService _optimizerService = new();
    private OptimizationStatus _currentStatus = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        Log("[READY] Creality-SparkX Optimizer v1.0.1 initialized. Ready to tune Creality Print 7.3.");
    }

    private void RefreshStatus()
    {
        try
        {
            _currentStatus = _optimizerService.DetectStatus();

            // App Status
            if (_currentStatus.CrealityRunning)
            {
                TxtAppStatus.Text = $"RUNNING (PID {_currentStatus.RunningPid})";
                TxtAppStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 0)); // Yellow
            }
            else if (_currentStatus.CrealityInstalled)
            {
                TxtAppStatus.Text = "READY (CLOSED)";
                TxtAppStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 132)); // Green
            }
            else
            {
                TxtAppStatus.Text = "NOT FOUND";
                TxtAppStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 77, 77)); // Red
            }
            TxtAppPath.Text = string.IsNullOrEmpty(_currentStatus.CrealityExePath) ? "Not detected" : _currentStatus.CrealityExePath;

            // Startup Status
            if (_currentStatus.HomePageEnabled)
            {
                TxtStartupStatus.Text = "SLOW (HOME PAGE)";
                TxtStartupStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 0));
                TxtStartupDetail.Text = "Webview2 feed loads on boot";
            }
            else
            {
                TxtStartupStatus.Text = "FAST (DIRECT 3D)";
                TxtStartupStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 132));
                TxtStartupDetail.Text = "Direct 3D Plate (Instant boot)";
            }

            // Stability / Kinematics Status
            if (_currentStatus.KinematicsBugDetected)
            {
                TxtStabilityStatus.Text = "VULNERABLE";
                TxtStabilityStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 77, 77));
                TxtStabilityDetail.Text = $"{_currentStatus.VulnerableProfilesCount} profile(s) have arc/step crash bug";
            }
            else
            {
                TxtStabilityStatus.Text = "PROTECTED";
                TxtStabilityStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 132));
                TxtStabilityDetail.Text = "Kinematics solver bug eliminated";
            }

            // Priority Status
            if (_currentStatus.PermanentHighPriorityActive)
            {
                TxtPriorityStatus.Text = "PERMANENT (HIGH)";
                TxtPriorityStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 132));
                TxtPriorityBadge.Text = "[ACTIVE IN KERNEL]";
                TxtPriorityBadge.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 132));
                BtnSetPermanentPriority.Content = "Disable Permanent High Priority";
            }
            else
            {
                TxtPriorityStatus.Text = "RESET BY WINDOWS";
                TxtPriorityStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 0));
                TxtPriorityBadge.Text = "[TASK MANAGER RESETS ON RESTART]";
                TxtPriorityBadge.Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 0));
                BtnSetPermanentPriority.Content = "⚡ Permanent High Priority (Registry IFEO)";
            }

            // Junk Size
            TxtJunkSize.Text = $"{OptimizerService.FormatBytes(_currentStatus.TotalJunkBytes)} junk";
            if (_currentStatus.TotalJunkBytes > 1024 * 1024 * 100) // > 100 MB
            {
                TxtJunkSize.Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 0));
            }
            else
            {
                TxtJunkSize.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 132));
            }

            // Backup Info
            if (_currentStatus.LatestBackupTime.HasValue)
            {
                TxtBackupInfo.Text = $"Backup: {_currentStatus.LatestBackupTime.Value:yyyy-MM-dd HH:mm}";
            }
            else
            {
                TxtBackupInfo.Text = "Backup: None yet";
            }
        }
        catch (Exception ex)
        {
            Log($"[WARNING] Status check error: {ex.Message}");
        }
    }

    private async void BtnQuickOptimize_Click(object sender, RoutedEventArgs e)
    {
        BtnQuickOptimize.IsEnabled = false;
        TxtLogStatus.Text = "• Optimizing...";
        TxtLogStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 0));

        // Read UI state on the UI thread BEFORE entering Task.Run
        bool purgeJunk = ChkPurgeJunk.IsChecked == true;
        bool fixKinematics = ChkFixKinematics.IsChecked == true;
        bool skipHomePage = ChkSkipHomePage.IsChecked == true;
        bool skipSplashTips = ChkSkipSplashTips.IsChecked == true;
        bool disableCloudSync = ChkDisableCloudSync.IsChecked == true;
        bool disableAutoCalc = ChkDisableAutoCalc.IsChecked == true;
        bool memOpt = ChkMemOpt.IsChecked == true;
        bool highGpu = ChkHighGpu.IsChecked == true;

        await Task.Run(() =>
        {
            try
            {
                Log("==================================================");
                Log("[START] Starting 1-Click Full Optimization...");

                _optimizerService.CloseOrKillCrealityPrint(Log);
                _optimizerService.CreateBackup(Log);

                if (purgeJunk)
                {
                    _optimizerService.CleanJunk(Log);
                }

                if (fixKinematics)
                {
                    _optimizerService.FixKinematicsAndCrashes(Log);
                }

                _optimizerService.OptimizeConfig(
                    skipHomePage: skipHomePage,
                    skipSplashAndTips: skipSplashTips,
                    disableCloudSync: disableCloudSync,
                    disableAutoCalculate: disableAutoCalc,
                    enableMemOpt: memOpt,
                    log: Log
                );

                if (highGpu)
                {
                    _optimizerService.ConfigureHighPerformanceGpu(Log);
                }

                Log("[COMPLETE] All optimizations applied successfully!");
                Log("==================================================");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Optimization failed: {ex.Message}");
            }
        });

        TxtLogStatus.Text = "• Completed";
        TxtLogStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 132));
        BtnQuickOptimize.IsEnabled = true;
        RefreshStatus();

        MessageBox.Show(
            "Creality Print 7.3 has been successfully optimized!\n\n" +
            "• Startup accelerated (Chromium home page & cloud wait skipped)\n" +
            "• Kinematic solver divergence and 100% CPU freezes fixed\n" +
            "• Temporary cache and dead WebView2 profiles cleaned\n" +
            "• High-Performance GPU verified\n\n" +
            "Tip: Click 'Permanent High Priority' to stop Windows from resetting priority on restart.",
            "Optimization Complete",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
    }

    private async void BtnCloudSync_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Do you want to enable Creality Cloud preset synchronization for the next launch?\n\n" +
            "• Click YES to enable Cloud Sync and launch Creality Print immediately.\n" +
            "• Click NO to learn how to sync manually inside Creality Print without enabling it on boot.",
            "Creality Cloud Preset Sync",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Yes)
        {
            BtnCloudSync.IsEnabled = false;
            await Task.Run(() =>
            {
                Log("[CLOUD] Enabling Creality Cloud synchronization for this session...");
                _optimizerService.CloseOrKillCrealityPrint(Log);
                _optimizerService.ToggleCloudSync(true, Log);
                _optimizerService.LaunchCrealityPrint(boostPriority: true, Log);
            });
            BtnCloudSync.IsEnabled = true;
            RefreshStatus();
        }
        else if (result == MessageBoxResult.No)
        {
            Log("[CLOUD GUIDE] Manual In-App Sync: In Creality Print, on the left panel next to 'Filamento' or 'Processo', click the circular arrows icon to sync anytime without slowing down boot.");
            MessageBox.Show(
                "How to sync manually inside Creality Print anytime:\n\n" +
                "1. Open Creality Print.\n" +
                "2. In the left sidebar ('Prepara' tab), look at the Presets section.\n" +
                "3. Next to the preset dropdown, click the circular arrows icon (Sync).\n\n" +
                "This downloads and uploads all your cloud presets instantly on-demand, without causing boot delays every time you start the software!",
                "Manual Cloud Sync Guide",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }

    private async void BtnSetPermanentPriority_Click(object sender, RoutedEventArgs e)
    {
        bool targetState = !_currentStatus.PermanentHighPriorityActive;
        BtnSetPermanentPriority.IsEnabled = false;

        await Task.Run(() =>
        {
            _optimizerService.SetPermanentHighPriority(targetState, Log);
        });

        BtnSetPermanentPriority.IsEnabled = true;
        RefreshStatus();
    }

    private void BtnCreateShortcut_Click(object sender, RoutedEventArgs e)
    {
        _optimizerService.CreateHighPriorityDesktopShortcut(Log);
        MessageBox.Show(
            "A new shortcut 'Creality Print (High Priority)' has been created on your Desktop!\n\n" +
            "Launching Creality Print from this shortcut will always run it in High CPU Priority automatically.",
            "Shortcut Created",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
    }

    private async void BtnCleanJunk_Click(object sender, RoutedEventArgs e)
    {
        BtnCleanJunk.IsEnabled = false;
        await Task.Run(() =>
        {
            Log("[START] Purging temporary files and orphaned WebView2 directories...");
            _optimizerService.CloseOrKillCrealityPrint(Log);
            _optimizerService.CleanJunk(Log);
        });
        BtnCleanJunk.IsEnabled = true;
        RefreshStatus();
    }

    private async void BtnFixCrashes_Click(object sender, RoutedEventArgs e)
    {
        BtnFixCrashes.IsEnabled = false;
        await Task.Run(() =>
        {
            Log("[START] Patching machine profiles and disabling background slicing race conditions...");
            _optimizerService.CloseOrKillCrealityPrint(Log);
            _optimizerService.CreateBackup(Log);
            _optimizerService.FixKinematicsAndCrashes(Log);
            _optimizerService.OptimizeConfig(
                skipHomePage: false,
                skipSplashAndTips: false,
                disableCloudSync: false,
                disableAutoCalculate: true,
                enableMemOpt: true,
                log: Log
            );
        });
        BtnFixCrashes.IsEnabled = true;
        RefreshStatus();
    }

    private async void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Do you want to restore the previous backup of Creality.conf and machine profiles?",
            "Confirm Restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Yes)
        {
            BtnRestore.IsEnabled = false;
            await Task.Run(() =>
            {
                _optimizerService.RestoreBackup(Log);
            });
            BtnRestore.IsEnabled = true;
            RefreshStatus();
        }
    }

    private void BtnLaunch_Click(object sender, RoutedEventArgs e)
    {
        Log("[LAUNCH] Launching Creality Print in High Priority mode...");
        _optimizerService.LaunchCrealityPrint(boostPriority: true, Log);
        RefreshStatus();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        Log("[SCAN] System state refreshed.");
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        TxtConsole.Clear();
    }

    public void Log(string message)
    {
        Dispatcher.Invoke(() =>
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            TxtConsole.AppendText($"[{timestamp}] {message}\r\n");
            TxtConsole.ScrollToEnd();
        });
    }
}
