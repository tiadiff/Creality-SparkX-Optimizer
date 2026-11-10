using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using SparkXOptimizer.Services;

namespace SparkXOptimizer;

public partial class App : Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);
    private const int ATTACH_PARENT_PROCESS = -1;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        bool hasCliArg = e.Args.Length > 0 && (
            e.Args.Contains("--optimize") || 
            e.Args.Contains("--silent") || 
            e.Args.Contains("-s") ||
            e.Args.Contains("/optimize")
        );

        if (hasCliArg)
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            Console.WriteLine();
            Console.WriteLine("==================================================");
            Console.WriteLine("    Creality-SparkX Optimizer (Headless Mode)");
            Console.WriteLine("==================================================");

            var service = new OptimizerService();
            service.CloseOrKillCrealityPrint(msg => Console.WriteLine(msg));
            service.CreateBackup(msg => Console.WriteLine(msg));
            service.CleanJunk(msg => Console.WriteLine(msg));
            service.FixKinematicsAndCrashes(msg => Console.WriteLine(msg));
            service.PatchSendPrintDialogZoom(0.80, msg => Console.WriteLine(msg));
            service.OptimizeConfig(
                skipHomePage: true,
                skipSplashAndTips: true,
                disableCloudSync: false,
                disableAutoCalculate: true,
                enableMemOpt: true,
                log: msg => Console.WriteLine(msg)
            );
            service.ConfigureHighPerformanceGpu(msg => Console.WriteLine(msg));

            Console.WriteLine("==================================================");
            Console.WriteLine("[SUCCESS] All optimizations applied successfully!");
            Console.WriteLine("==================================================");

            Shutdown(0);
            return;
        }

        // GUI Mode
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Application Error: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show($"Fatal Error: {ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
