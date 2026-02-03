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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert a single candle (insert or update if exists)
    /// </summary>
    Task UpsertCandleAsync(
        string symbol,
        string interval,
        CandleDto candle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert multiple candles in batch
    /// </summary>
    Task UpsertCandlesAsync(
        string symbol,
        string interval,
        IEnumerable<CandleDto> candles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if database connection is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
