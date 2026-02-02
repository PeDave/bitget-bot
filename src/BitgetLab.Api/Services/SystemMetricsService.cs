using BitgetLab.Core.Models;

namespace BitgetLab.Api.Services;

public interface ISystemMetricsService
{
    Task<SystemMetrics> GetMetricsAsync();
}

public class SystemMetricsService : ISystemMetricsService
{
    public async Task<SystemMetrics> GetMetricsAsync()
    {
        var metrics = new SystemMetrics();

        // Get CPU usage
        metrics.CpuUsagePercent = await GetCpuUsageAsync();

        // Get memory info
        metrics.Memory = await GetMemoryInfoAsync();

        // Get disk info
        metrics.Disk = await GetDiskInfoAsync();

        // Get load average
        metrics.LoadAverage1Min = await GetLoadAverageAsync();

        // Get uptime
        metrics.Uptime = await GetUptimeAsync();

        return metrics;
    }

    private async Task<double> GetCpuUsageAsync()
    {
        try
        {
            // Read CPU stats from /proc/stat
            var lines = await File.ReadAllLinesAsync("/proc/stat");
            var cpuLine = lines.FirstOrDefault(l => l.StartsWith("cpu "));
            if (cpuLine == null) return 0;

            var values = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length < 5) return 0;

            // Calculate basic CPU usage from first read
            var idle = long.Parse(values[4]);
            var total = values.Skip(1).Take(7).Sum(v => long.Parse(v));
            
            // For accurate usage, need two samples
            await Task.Delay(100);
            
            lines = await File.ReadAllLinesAsync("/proc/stat");
            cpuLine = lines.FirstOrDefault(l => l.StartsWith("cpu "));
            if (cpuLine == null) return 0;
            
            values = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var idle2 = long.Parse(values[4]);
            var total2 = values.Skip(1).Take(7).Sum(v => long.Parse(v));

            var idleDelta = idle2 - idle;
            var totalDelta = total2 - total;

            return totalDelta > 0 ? 100.0 * (1.0 - (double)idleDelta / totalDelta) : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<MemoryInfo> GetMemoryInfoAsync()
    {
        try
        {
            var lines = await File.ReadAllLinesAsync("/proc/meminfo");
            var memTotal = ParseMemInfoLine(lines, "MemTotal:");
            var memFree = ParseMemInfoLine(lines, "MemFree:");
            var memAvailable = ParseMemInfoLine(lines, "MemAvailable:");

            var usedBytes = memTotal - memAvailable;

            return new MemoryInfo
            {
                TotalBytes = memTotal,
                UsedBytes = usedBytes,
                FreeBytes = memAvailable
            };
        }
        catch
        {
            return new MemoryInfo();
        }
    }

    private long ParseMemInfoLine(string[] lines, string key)
    {
        var line = lines.FirstOrDefault(l => l.StartsWith(key));
        if (line == null) return 0;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return 0;

        if (long.TryParse(parts[1], out var value))
        {
            return value * 1024; // Convert KB to bytes
        }

        return 0;
    }

    private async Task<DiskInfo> GetDiskInfoAsync()
    {
        try
        {
            // Get root filesystem info
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "df",
                    Arguments = "-B1 /",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var lines = output.Split('\n');
            if (lines.Length < 2) return new DiskInfo();

            var parts = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return new DiskInfo();

            var total = long.Parse(parts[1]);
            var used = long.Parse(parts[2]);
            var free = long.Parse(parts[3]);

            return new DiskInfo
            {
                TotalBytes = total,
                UsedBytes = used,
                FreeBytes = free
            };
        }
        catch
        {
            return new DiskInfo();
        }
    }

    private async Task<double> GetLoadAverageAsync()
    {
        try
        {
            var content = await File.ReadAllTextAsync("/proc/loadavg");
            var parts = content.Split(' ');
            if (parts.Length > 0 && double.TryParse(parts[0], out var load))
            {
                return load;
            }
        }
        catch
        {
            // Ignore
        }

        return 0;
    }

    private async Task<TimeSpan> GetUptimeAsync()
    {
        try
        {
            var content = await File.ReadAllTextAsync("/proc/uptime");
            var parts = content.Split(' ');
            if (parts.Length > 0 && double.TryParse(parts[0], out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }
        catch
        {
            // Ignore
        }

        return TimeSpan.Zero;
    }
}
