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

/// <summary>
/// Request for manual candle backfill
/// </summary>
public class CandleBackfillRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? Limit { get; set; }
}

/// <summary>
/// Request for range-based candle backfill with enhanced tracking
/// </summary>
public class RangeBackfillRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Market { get; set; } = "spot";
    public string Interval { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public int? Limit { get; set; }
    public int? MaxConcurrency { get; set; }
}

/// <summary>
/// Response for range-based candle backfill with detailed metrics
/// </summary>
public class RangeBackfillResponse
{
    public bool Ok { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public int FetchedBatches { get; set; }
    public int FetchedCandles { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public long DurationMs { get; set; }
}

/// <summary>
/// Response for candle range query with warmup candles
/// </summary>
public class CandleRangeResponse
{
    public string Symbol { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int WarmupCandles { get; set; }
    public int Count { get; set; }
    public IEnumerable<CandleDto> Candles { get; set; } = Enumerable.Empty<CandleDto>();
}
