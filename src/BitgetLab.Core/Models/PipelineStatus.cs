namespace BitgetLab.Core.Models;

/// <summary>
/// Status of a per-symbol futures data pipeline
/// </summary>
public class PipelineStatus
{
    /// <summary>
    /// The symbol being tracked
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// The market type
    /// </summary>
    public MarketType Market { get; set; } = MarketType.Futures;

    /// <summary>
    /// Whether the pipeline is currently running
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// When the pipeline was started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Last backfill completion times per interval
    /// </summary>
    public Dictionary<string, DateTime?> LastBackfillTimes { get; set; } = new();

    /// <summary>
    /// Last WebSocket update time
    /// </summary>
    public DateTime? LastWsUpdate { get; set; }

    /// <summary>
    /// Last REST sync time
    /// </summary>
    public DateTime? LastRestSync { get; set; }

    /// <summary>
    /// Any errors encountered
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Active WebSocket intervals
    /// </summary>
    public List<string> ActiveWsIntervals { get; set; } = new();

    /// <summary>
    /// Active REST sync intervals
    /// </summary>
    public List<string> ActiveRestIntervals { get; set; } = new();
}
