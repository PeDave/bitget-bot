namespace BitgetLab.Core.Models;

/// <summary>
/// Status of a futures symbol data pipeline
/// </summary>
public class PipelineStatus
{
    /// <summary>
    /// Symbol being tracked
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Market type (e.g., "futures")
    /// </summary>
    public string Market { get; set; } = string.Empty;

    /// <summary>
    /// Current state of the pipeline
    /// </summary>
    public PipelineState State { get; set; } = PipelineState.Stopped;

    /// <summary>
    /// Current progress message
    /// </summary>
    public string Progress { get; set; } = string.Empty;

    /// <summary>
    /// Intervals being tracked
    /// </summary>
    public string[] Intervals { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Intervals completed in backfill
    /// </summary>
    public string[] CompletedIntervals { get; set; } = Array.Empty<string>();

    /// <summary>
    /// When the pipeline was started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Error message if pipeline failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// States of a pipeline
/// </summary>
public enum PipelineState
{
    Stopped,
    Backfilling,
    Running,
    Failed
}
