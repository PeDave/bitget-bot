namespace BitgetLab.Core.Options;

/// <summary>
/// Configuration options for charting features
/// </summary>
public class ChartingOptions
{
    public const string SectionName = "Charting";

    /// <summary>
    /// Enable Postgres persistence for candle data
    /// </summary>
    public bool EnablePersistence { get; set; } = false;

    /// <summary>
    /// Size of the in-memory candle ring buffer per subscription
    /// </summary>
    public int BufferSize { get; set; } = 500;

    /// <summary>
    /// Enable automatic gap detection and backfill
    /// </summary>
    public bool EnableGapDetection { get; set; } = true;
}
