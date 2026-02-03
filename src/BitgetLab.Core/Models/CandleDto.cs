namespace BitgetLab.Core.Models;

/// <summary>
/// Represents OHLCV candle data
/// </summary>
public class CandleDto
{
    public DateTime OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public decimal QuoteVolume { get; set; }
}

/// <summary>
/// Query parameters for fetching candles
/// </summary>
public class CandleQueryParams
{
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? Limit { get; set; }
}

/// <summary>
/// Statistics for candle data in database
/// </summary>
public class CandleStatsDto
{
    public bool DbEnabled { get; set; }
    public bool DbAvailable { get; set; }
    public int Count { get; set; }
    public DateTime? MinOpenTime { get; set; }
    public DateTime? MaxOpenTime { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}
