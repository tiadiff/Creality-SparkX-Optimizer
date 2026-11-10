namespace SparkXOptimizer.Models;

public class OptimizationStatus
{
    public bool CrealityInstalled { get; set; }
    public string CrealityExePath { get; set; } = string.Empty;
    public string AppDataDir { get; set; } = string.Empty;
    public string ConfigFilePath { get; set; } = string.Empty;
    
    public bool CrealityRunning { get; set; }
    public int RunningPid { get; set; }
    
    public bool HomePageEnabled { get; set; } = true;
    public bool SplashEnabled { get; set; } = true;
    public bool DailyTipsEnabled { get; set; } = true;
    public bool CloudSyncEnabled { get; set; } = true;
    public bool AutoCalculateEnabled { get; set; } = true;
    public bool PreviewMemOptEnabled { get; set; }
    
    public bool KinematicsBugDetected { get; set; }
    public int VulnerableProfilesCount { get; set; }
    public int PatchedProfilesCount { get; set; }
    
    public long WebViewJunkBytes { get; set; }
    public long ModelTempJunkBytes { get; set; }
    public long LogJunkBytes { get; set; }
    public long TotalJunkBytes => WebViewJunkBytes + ModelTempJunkBytes + LogJunkBytes;
    
    public bool HighPerformanceGpuActive { get; set; }
    public string? LatestBackupPath { get; set; }
    public DateTime? LatestBackupTime { get; set; }
}
