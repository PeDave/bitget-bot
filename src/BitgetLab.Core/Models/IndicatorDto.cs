namespace BitgetLab.Core.Models;

/// <summary>
/// Represents a computed indicator value at a specific time
/// </summary>
public class IndicatorValueDto
{
    public DateTime OpenTime { get; set; }
    public decimal Value { get; set; }
}

/// <summary>
/// Query parameters for computing indicators
/// </summary>
public class IndicatorQueryParams
{
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public string Indicator { get; set; } = string.Empty;
    public int Period { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? Limit { get; set; }
}

/// <summary>
/// Supported indicator types
/// </summary>
public static class IndicatorTypes
{
    public const string SMA = "SMA";
    public const string EMA = "EMA";
    public const string RSI = "RSI";
}
