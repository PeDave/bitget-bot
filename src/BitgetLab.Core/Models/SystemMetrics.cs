namespace BitgetLab.Core.Models;

/// <summary>
/// System metrics (CPU, RAM, Disk, Load)
/// </summary>
public class SystemMetrics
{
    public double CpuUsagePercent { get; set; }
    public MemoryInfo Memory { get; set; } = new();
    public DiskInfo Disk { get; set; } = new();
    public double LoadAverage1Min { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class MemoryInfo
{
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long FreeBytes { get; set; }
    public double UsagePercent => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
}

public class DiskInfo
{
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long FreeBytes { get; set; }
    public double UsagePercent => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
}
