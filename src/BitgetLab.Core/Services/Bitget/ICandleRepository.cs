using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Repository for persisting candle data to database
/// </summary>
public interface ICandleRepository
{
    /// <summary>
    /// Get candles from database for a symbol/interval
    /// </summary>
    Task<IEnumerable<CandleDto>> GetCandlesAsync(
        string symbol,
        string interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 500,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert a single candle (insert or update if exists)
    /// </summary>
    Task UpsertCandleAsync(
        string symbol,
        string interval,
        CandleDto candle,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert multiple candles in batch
    /// </summary>
    Task UpsertCandlesAsync(
        string symbol,
        string interval,
        IEnumerable<CandleDto> candles,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if database connection is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get statistics for candles in database
    /// </summary>
    Task<CandleStatsDto> GetStatsAsync(
        string symbol,
        string interval,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete candles older than the specified cutoff time
    /// </summary>
    Task<int> DeleteCandlesOlderThanAsync(
        string symbol,
        string interval,
        DateTime cutoffTime,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete candles beyond the max rows limit, keeping only the newest rows
    /// </summary>
    Task<int> DeleteCandlesBeyondMaxRowsAsync(
        string symbol,
        string interval,
        int maxRows,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of candles for a symbol/interval
    /// </summary>
    Task<int> GetCandleCountAsync(
        string symbol,
        string interval,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default);
}
