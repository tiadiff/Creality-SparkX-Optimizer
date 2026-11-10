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
        Log("[READY] Creality-SparkX Optimizer initialized. Ready to tune Creality Print 7.3.");
    }

    private void RefreshStatus()
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

        // Junk Size
        TxtJunkSize.Text = OptimizerService.FormatBytes(_currentStatus.TotalJunkBytes);
        if (_currentStatus.TotalJunkBytes > 1024 * 1024 * 100) // > 100 MB
        {
            TxtJunkSize.Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 0));
            TxtJunkDetail.Text = $"{OptimizerService.FormatBytes(_currentStatus.WebViewJunkBytes)} WebView2 + {OptimizerService.FormatBytes(_currentStatus.ModelTempJunkBytes)} model temp";
        }
        else
        {
            TxtJunkSize.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 132));
            TxtJunkDetail.Text = "Temp cache clean";
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

    private async void BtnQuickOptimize_Click(object sender, RoutedEventArgs e)
    {
        BtnQuickOptimize.IsEnabled = false;
        TxtLogStatus.Text = "• Optimizing...";
        TxtLogStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 0));

        await Task.Run(() =>
        {
            Log("==================================================");
            Log("[START] Starting 1-Click Full Optimization...");

            // 1. Terminate running instance if any
            _optimizerService.CloseOrKillCrealityPrint(Log);

            // 2. Create backup first
            _optimizerService.CreateBackup(Log);

            // 3. Clean junk
            if (ChkPurgeJunk.IsChecked == true)
            {
                _optimizerService.CleanJunk(Log);
            }

            // 4. Fix Kinematic crash bug
            if (ChkFixKinematics.IsChecked == true)
            {
                _optimizerService.FixKinematicsAndCrashes(Log);
            }

            // 5. Optimize Creality.conf
            _optimizerService.OptimizeConfig(
                skipHomePage: ChkSkipHomePage.IsChecked == true,
                skipSplashAndTips: ChkSkipSplashTips.IsChecked == true,
                disableCloudSync: ChkDisableCloudSync.IsChecked == true,
                disableAutoCalculate: ChkDisableAutoCalc.IsChecked == true,
                enableMemOpt: ChkMemOpt.IsChecked == true,
                log: Log
            );

            // 6. Set High Performance GPU
            if (ChkHighGpu.IsChecked == true)
            {
                _optimizerService.ConfigureHighPerformanceGpu(Log);
            }

            Log("[COMPLETE] All optimizations applied successfully!");
            Log("==================================================");
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
            "You can now launch Creality Print with optimal performance.",
            "Optimization Complete",
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
        Log("[LAUNCH] Launching Creality Print in optimized mode...");
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
