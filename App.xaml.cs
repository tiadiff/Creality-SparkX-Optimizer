using System.Windows;
using SparkXOptimizer.Services;

namespace SparkXOptimizer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0 && (e.Args.Contains("--optimize") || e.Args.Contains("--silent") || e.Args.Contains("-s")))
        {
            // Headless / Silent Mode
            var service = new OptimizerService();
            Console.WriteLine("[SparkX Optimizer] Running headless optimization...");
            
            service.CloseOrKillCrealityPrint(msg => Console.WriteLine(msg));
            service.CreateBackup(msg => Console.WriteLine(msg));
            service.CleanJunk(msg => Console.WriteLine(msg));
            service.FixKinematicsAndCrashes(msg => Console.WriteLine(msg));
            service.OptimizeConfig(
                skipHomePage: true,
                skipSplashAndTips: true,
                disableCloudSync: true,
                disableAutoCalculate: true,
                enableMemOpt: true,
                log: msg => Console.WriteLine(msg)
            );
            service.ConfigureHighPerformanceGpu(msg => Console.WriteLine(msg));
            
            Console.WriteLine("[SparkX Optimizer] Optimization complete.");
            Shutdown(0);
            return;
        }
    }
}
