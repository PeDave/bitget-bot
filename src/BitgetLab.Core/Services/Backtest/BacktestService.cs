using BitgetLab.Core.Models;
using Microsoft.Extensions.Logging;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// Service for managing backtest execution and storage
/// </summary>
public interface IBacktestService
{
    Task<BacktestDto> RunBacktestAsync(RunBacktestRequest request, CancellationToken cancellationToken = default);
    Task<BacktestDto?> GetBacktestAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<BacktestDto>> GetBacktestsAsync(string? symbol = null, string? strategy = null, string? status = null, int limit = 50, CancellationToken cancellationToken = default);
    Task<IEnumerable<BacktestTradeDto>> GetBacktestTradesAsync(Guid backtestId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of backtest service
/// </summary>
public class BacktestService : IBacktestService
{
    private readonly IBacktestEngine _engine;
    private readonly IBacktestRepository? _repository;
    private readonly ILogger<BacktestService> _logger;

    public BacktestService(
        IBacktestEngine engine,
        ILogger<BacktestService> logger,
        IBacktestRepository? repository = null)
    {
        _engine = engine;
        _repository = repository;
        _logger = logger;
    }

    public async Task<BacktestDto> RunBacktestAsync(RunBacktestRequest request, CancellationToken cancellationToken = default)
    {
        // Validate request
        ValidateRequest(request);

        // Parse market type
        var market = MarketTypeExtensions.ParseMarketType(request.Market);

        // Create strategy
        var strategy = CreateStrategy(request.Strategy);
        strategy.Configure(request.Parameters);

        // Create backtest record
        var backtest = new BacktestDto
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Symbol = request.Symbol,
            Interval = request.Interval,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Strategy = request.Strategy,
            Parameters = request.Parameters,
            Status = BacktestStatus.Running,
            Market = market.ToStringValue()
        };

        // Save initial backtest record if repository is available
        if (_repository != null)
        {
            try
            {
                await _repository.SaveBacktestAsync(backtest, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save backtest to database");
            }
        }

        try
        {
            // Configure and run backtest
            var config = new BacktestConfig
            {
                InitialBalance = request.InitialBalance ?? 10000m,
                FeeBps = request.FeeBps ?? 10m,
                SlippageBps = request.SlippageBps ?? 5m
            };

            var result = await _engine.RunBacktestAsync(
                request.Symbol,
                request.Interval,
                request.StartTime,
                request.EndTime,
                strategy,
                config,
                market,
                cancellationToken);

            // Update backtest with results
            backtest.Status = BacktestStatus.Completed;
            backtest.Summary = result.Summary;

            // Set backtest ID on all trades
            foreach (var trade in result.Trades)
            {
                trade.BacktestId = backtest.Id;
            }

            // Save results if repository is available
            if (_repository != null)
            {
                try
                {
                    await _repository.UpdateBacktestAsync(
                        backtest.Id,
                        backtest.Status,
                        backtest.Summary,
                        null,
                        cancellationToken);

                    if (result.Trades.Count > 0)
                    {
                        await _repository.SaveTradesAsync(result.Trades, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save backtest results to database");
                }
            }

            return backtest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backtest execution failed");
            
            backtest.Status = BacktestStatus.Failed;
            backtest.Error = ex.Message;

            // Update backtest with error if repository is available
            if (_repository != null)
            {
                try
                {
                    await _repository.UpdateBacktestAsync(
                        backtest.Id,
                        backtest.Status,
                        null,
                        backtest.Error,
                        cancellationToken);
                }
                catch (Exception updateEx)
                {
                    _logger.LogWarning(updateEx, "Failed to update backtest error in database");
                }
            }

            throw;
        }
    }

    public async Task<BacktestDto?> GetBacktestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_repository == null)
        {
            return null;
        }

        return await _repository.GetBacktestAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<BacktestDto>> GetBacktestsAsync(
        string? symbol = null,
        string? strategy = null,
        string? status = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (_repository == null)
        {
            return Enumerable.Empty<BacktestDto>();
        }

        return await _repository.GetBacktestsAsync(symbol, strategy, status, limit, cancellationToken);
    }

    public async Task<IEnumerable<BacktestTradeDto>> GetBacktestTradesAsync(Guid backtestId, CancellationToken cancellationToken = default)
    {
        if (_repository == null)
        {
            return Enumerable.Empty<BacktestTradeDto>();
        }

        return await _repository.GetTradesAsync(backtestId, cancellationToken);
    }

    private void ValidateRequest(RunBacktestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
        {
            throw new ArgumentException("Symbol is required", nameof(request.Symbol));
        }

        if (string.IsNullOrWhiteSpace(request.Interval))
        {
            throw new ArgumentException("Interval is required", nameof(request.Interval));
        }

        if (string.IsNullOrWhiteSpace(request.Strategy))
        {
            throw new ArgumentException("Strategy is required", nameof(request.Strategy));
        }

        if (request.StartTime >= request.EndTime)
        {
            throw new ArgumentException("StartTime must be before EndTime");
        }

        if (request.InitialBalance.HasValue && request.InitialBalance.Value <= 0)
        {
            throw new ArgumentException("InitialBalance must be positive");
        }

        if (request.FeeBps.HasValue && request.FeeBps.Value < 0)
        {
            throw new ArgumentException("FeeBps cannot be negative");
        }

        if (request.SlippageBps.HasValue && request.SlippageBps.Value < 0)
        {
            throw new ArgumentException("SlippageBps cannot be negative");
        }
    }

    private IStrategy CreateStrategy(string strategyName)
    {
        return strategyName.ToLowerInvariant() switch
        {
            StrategyTypes.EmaCross or "ema_crossover" => new EmaCrossoverStrategy(),
            StrategyTypes.Rsi or "rsi_threshold" => new RsiStrategy(),
            _ => throw new ArgumentException($"Unknown strategy: {strategyName}. Supported strategies: {StrategyTypes.EmaCross}, {StrategyTypes.Rsi}")
        };
    }
}
