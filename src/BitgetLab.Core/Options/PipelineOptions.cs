namespace BitgetLab.Core.Options;

/// <summary>
/// Configuration options for the futures symbol data pipeline
/// </summary>
public class PipelineOptions
{
    public const string SectionName = "Bitget:Futures:Pipeline";

    /// <summary>
    /// Enable/disable the futures data pipeline feature
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Intervals to keep fresh via WebSocket subscriptions
    /// </summary>
    public string[] WsIntervals { get; set; } = new[] { "15m", "30m", "1h" };

    /// <summary>
    /// Intervals to keep fresh via periodic REST sync
    /// </summary>
    public string[] RestIntervals { get; set; } = new[] { "4h", "1d" };

    /// <summary>
    /// How often to sync REST intervals (in minutes)
    /// </summary>
    public int SyncEveryMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum number of rows to keep per (symbol, market_type, interval)
    /// </summary>
    public int MaxRows { get; set; } = 10000;

    /// <summary>
    /// Lookback days per interval for initial backfill and retention
    /// </summary>
    public Dictionary<string, int> LookbackDays { get; set; } = new()
    {
        { "15m", 45 },
        { "30m", 90 },
        { "1h", 180 },
        { "4h", 365 },
        { "1d", 730 }
    };
}
