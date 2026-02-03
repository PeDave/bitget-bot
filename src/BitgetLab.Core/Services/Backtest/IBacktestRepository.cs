using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// Repository for persisting backtest data to database
/// </summary>
public interface IBacktestRepository
{
    /// <summary>
    /// Save a backtest record
    /// </summary>
    Task<Guid> SaveBacktestAsync(BacktestDto backtest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update backtest status and results
    /// </summary>
    Task UpdateBacktestAsync(Guid id, string status, BacktestSummary? summary, string? error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a backtest by ID
    /// </summary>
    Task<BacktestDto?> GetBacktestAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get list of backtests with optional filters
    /// </summary>
    Task<IEnumerable<BacktestDto>> GetBacktestsAsync(
        string? symbol = null,
        string? strategy = null,
        string? status = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save trades for a backtest
    /// </summary>
    Task SaveTradesAsync(IEnumerable<BacktestTradeDto> trades, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get trades for a backtest
    /// </summary>
    Task<IEnumerable<BacktestTradeDto>> GetTradesAsync(Guid backtestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if database connection is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
