namespace BitgetLab.Core.Options;

/// <summary>
/// Configuration options for the futures data pipeline
/// </summary>
public class FuturesPipelineOptions
{
    public const string SectionName = "Bitget:Futures:Pipeline";

    /// <summary>
    /// Enable or disable the futures data pipeline
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Intervals to subscribe via WebSocket (smaller intervals)
    /// </summary>
    public List<string> WsIntervals { get; set; } = new() { "15m", "30m", "1h" };

    /// <summary>
    /// Intervals to sync via periodic REST calls (larger intervals)
    /// </summary>
    public List<string> RestIntervals { get; set; } = new() { "4h", "1d" };

    /// <summary>
    /// Lookback days for each interval (retention window)
    /// </summary>
    public Dictionary<string, int> LookbackDays { get; set; } = new()
    {
        { "15m", 45 },
        { "30m", 90 },
        { "1h", 180 },
        { "4h", 365 },
        { "1d", 730 }
    };

    /// <summary>
    /// Maximum number of rows to keep per symbol+market+interval (safety cap)
    /// </summary>
    public int MaxRows { get; set; } = 10000;

    /// <summary>
    /// How often to run REST sync for larger intervals (in minutes)
    /// </summary>
    public int SyncEveryMinutes { get; set; } = 30;
}
