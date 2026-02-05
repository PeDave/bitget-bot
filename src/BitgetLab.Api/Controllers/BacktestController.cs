using Microsoft.AspNetCore.Mvc;
using BitgetLab.Core.Models;
using BitgetLab.Core.Services.Backtest;
using Microsoft.Extensions.Options;

namespace BitgetLab.Api.Controllers;

/// <summary>
/// Controller for backtest API endpoints (issue #44)
/// </summary>
[ApiController]
[Route("api/backtest")]
public class BacktestController : ControllerBase
{
    private readonly IBacktestDataService _backtestDataService;
    private readonly ILogger<BacktestController> _logger;
    private readonly BacktestApiOptions _options;

    public BacktestController(
        IBacktestDataService backtestDataService,
        ILogger<BacktestController> logger,
        IOptions<BacktestApiOptions> options)
    {
        _backtestDataService = backtestDataService;
        _logger = logger;
        _options = options.Value;
    }

    private const decimal INFINITE_PROFIT_FACTOR = 999m;

    /// <summary>
    /// Run a backtest for RSI mean-reversion strategy
    /// </summary>
    /// <param name="request">Backtest configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backtest results with trades and equity curve</returns>
    [HttpPost("run")]
    public async Task<IActionResult> RunBacktest(
        [FromBody] BacktestRunRequest request,
        CancellationToken cancellationToken)
    {
        // API Key authentication
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            if (!Request.Headers.TryGetValue("X-Api-Key", out var headerValue) ||
                headerValue != _options.ApiKey)
            {
                _logger.LogWarning("Unauthorized backtest request from {IP}", 
                    HttpContext.Connection.RemoteIpAddress);
                return Unauthorized(new { error = "Invalid or missing API key" });
            }
        }

        try
        {
            // Validate request
            ValidateRequest(request);

            // Parse market type
            var market = MarketTypeExtensions.ParseMarketType(request.Market);

            // Only support RSI reversion strategy for now
            if (request.Strategy.Type.ToLowerInvariant() != "rsi-reversion")
            {
                return BadRequest(new
                {
                    error = "Unsupported strategy",
                    message = "Only 'rsi-reversion' strategy is supported in this version"
                });
            }

            // Get candles with lookback for RSI warmup
            var candles = await _backtestDataService.GetCandlesWithLookbackAsync(
                request.Symbol,
                request.Interval,
                request.Start,
                request.End,
                request.Strategy.RsiPeriod,
                market,
                cancellationToken);

            // Create and configure strategy
            var strategy = new RsiReversionStrategy();
            strategy.Configure(new Dictionary<string, object>
            {
                { "rsiPeriod", request.Strategy.RsiPeriod },
                { "entryBelow", request.Strategy.EntryBelow },
                { "exitAbove", request.Strategy.ExitAbove }
            });

            // Generate signals
            var signals = strategy.GenerateSignals(candles).ToList();

            // Filter signals to only those within the requested time range
            var filteredSignals = signals
                .Where(s => s.Time >= request.Start && s.Time <= request.End)
                .OrderBy(s => s.Time)
                .ToList();

            // Simulate trades
            var trades = SimulateTrades(filteredSignals, request);

            // Calculate summary metrics
            var summary = CalculateSummary(trades, request.InitialQuote);

            // Build response
            var response = new BacktestRunResponse
            {
                Summary = summary,
                Trades = trades.Where(t => t.ExitTime.HasValue).Select(t => new TradeDetail
                {
                    EntryTime = t.EntryTime,
                    EntryPrice = t.EntryPrice,
                    ExitTime = t.ExitTime!.Value,
                    ExitPrice = t.ExitPrice ?? 0,
                    Qty = t.Qty,
                    Pnl = t.Pnl ?? 0,
                    PnlPct = t.EntryPrice > 0 ? ((t.ExitPrice ?? 0) - t.EntryPrice) / t.EntryPrice * 100 : 0
                }).ToList(),
                EquityCurve = CalculateEquityCurve(trades, request.InitialQuote),
                Parameters = new BacktestParameters
                {
                    Symbol = request.Symbol,
                    Market = request.Market,
                    Interval = request.Interval,
                    Start = request.Start,
                    End = request.End,
                    Strategy = request.Strategy,
                    FeesBps = request.FeesBps,
                    SlippageBps = request.SlippageBps,
                    InitialQuote = request.InitialQuote
                }
            };

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid backtest request");
            return BadRequest(new
            {
                error = "Invalid request",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Backtest execution failed");
            return BadRequest(new
            {
                error = "Execution failed",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during backtest");
            return StatusCode(500, new
            {
                error = "Internal server error",
                message = "An unexpected error occurred"
            });
        }
    }

    private void ValidateRequest(BacktestRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
        {
            throw new ArgumentException("Symbol is required");
        }

        if (string.IsNullOrWhiteSpace(request.Interval))
        {
            throw new ArgumentException("Interval is required");
        }

        if (request.Start >= request.End)
        {
            throw new ArgumentException("Start time must be before end time");
        }

        // Validate time range limits
        var timeRange = request.End - request.Start;
        if (timeRange > _options.MaxTimeRange)
        {
            throw new ArgumentException(
                $"Time range exceeds maximum allowed ({_options.MaxTimeRange.TotalDays} days). " +
                $"Requested: {timeRange.TotalDays:F1} days");
        }

        if (request.InitialQuote <= 0)
        {
            throw new ArgumentException("InitialQuote must be positive");
        }

        if (request.FeesBps < 0)
        {
            throw new ArgumentException("FeesBps cannot be negative");
        }

        if (request.SlippageBps < 0)
        {
            throw new ArgumentException("SlippageBps cannot be negative");
        }

        // Validate strategy parameters
        if (request.Strategy.RsiPeriod < 2)
        {
            throw new ArgumentException("RsiPeriod must be at least 2");
        }

        if (request.Strategy.EntryBelow >= request.Strategy.ExitAbove)
        {
            throw new ArgumentException("EntryBelow must be less than ExitAbove");
        }

        if (request.Strategy.EntryBelow < 0 || request.Strategy.ExitAbove > 100)
        {
            throw new ArgumentException("RSI thresholds must be between 0 and 100");
        }
    }

    private List<BacktestTradeDto> SimulateTrades(
        List<TradingSignal> signals,
        BacktestRunRequest request)
    {
        var trades = new List<BacktestTradeDto>();
        BacktestTradeDto? openTrade = null;
        var balance = request.InitialQuote;

        foreach (var signal in signals)
        {
            if (signal.Type == SignalType.Buy && openTrade == null)
            {
                // Open long position
                var entryPrice = ApplySlippage(signal.Price, request.SlippageBps, isBuy: true);
                var qty = balance / entryPrice; // Invest full balance
                var fee = qty * entryPrice * (request.FeesBps / 10000m);

                openTrade = new BacktestTradeDto
                {
                    Id = Guid.NewGuid(),
                    EntryTime = signal.Time,
                    Side = "long",
                    EntryPrice = entryPrice,
                    Qty = qty,
                    Metadata = new Dictionary<string, object>
                    {
                        { "entryReason", signal.Reason },
                        { "entryFee", fee }
                    }
                };

                balance -= fee; // Deduct entry fee from balance
            }
            else if (signal.Type == SignalType.Sell && openTrade != null)
            {
                // Close long position
                var exitPrice = ApplySlippage(signal.Price, request.SlippageBps, isBuy: false);
                var exitValue = openTrade.Qty * exitPrice;
                var fee = exitValue * (request.FeesBps / 10000m);
                var entryValue = openTrade.Qty * openTrade.EntryPrice;
                var entryFee = openTrade.Metadata != null && openTrade.Metadata.ContainsKey("entryFee")
                    ? Convert.ToDecimal(openTrade.Metadata["entryFee"])
                    : 0m;

                openTrade.ExitTime = signal.Time;
                openTrade.ExitPrice = exitPrice;
                openTrade.Pnl = exitValue - entryValue - entryFee - fee;

                if (openTrade.Metadata != null)
                {
                    openTrade.Metadata["exitReason"] = signal.Reason;
                    openTrade.Metadata["exitFee"] = fee;
                }

                balance = exitValue - fee; // Update balance after exit
                trades.Add(openTrade);
                openTrade = null;
            }
        }

        return trades;
    }

    private decimal ApplySlippage(decimal price, decimal slippageBps, bool isBuy)
    {
        var slippageMultiplier = slippageBps / 10000m;
        return isBuy
            ? price * (1 + slippageMultiplier)  // Pay more when buying
            : price * (1 - slippageMultiplier); // Receive less when selling
    }

    private SummaryMetrics CalculateSummary(
        List<BacktestTradeDto> trades,
        decimal initialBalance)
    {
        var winningTrades = trades.Where(t => t.Pnl > 0).ToList();
        var losingTrades = trades.Where(t => t.Pnl <= 0).ToList();
        var totalPnl = trades.Sum(t => t.Pnl ?? 0);
        var finalBalance = initialBalance + totalPnl;

        // Calculate profit factor
        var totalWinning = winningTrades.Sum(t => t.Pnl ?? 0);
        var totalLosing = Math.Abs(losingTrades.Sum(t => t.Pnl ?? 0));
        var profitFactor = totalLosing > 0 ? totalWinning / totalLosing : (totalWinning > 0 ? INFINITE_PROFIT_FACTOR : 0m);

        // Calculate max drawdown
        var runningBalance = initialBalance;
        var peak = initialBalance;
        var maxDrawdownPct = 0m;

        foreach (var trade in trades.OrderBy(t => t.ExitTime))
        {
            if (trade.ExitTime.HasValue)
            {
                runningBalance += trade.Pnl ?? 0;

                if (runningBalance > peak)
                {
                    peak = runningBalance;
                }

                var drawdown = (peak - runningBalance) / peak * 100;
                if (drawdown > maxDrawdownPct)
                {
                    maxDrawdownPct = drawdown;
                }
            }
        }

        return new SummaryMetrics
        {
            TotalPnL = totalPnl,
            TotalReturnPct = initialBalance > 0 ? (finalBalance - initialBalance) / initialBalance * 100 : 0,
            TradeCount = trades.Count,
            WinRate = trades.Count > 0 ? (decimal)winningTrades.Count / trades.Count * 100 : 0,
            MaxDrawdownPct = maxDrawdownPct,
            ProfitFactor = profitFactor
        };
    }

    private List<EquityPoint> CalculateEquityCurve(
        List<BacktestTradeDto> trades,
        decimal initialBalance)
    {
        var curve = new List<EquityPoint>();
        var runningBalance = initialBalance;

        // Add initial point
        if (trades.Count > 0)
        {
            curve.Add(new EquityPoint
            {
                Time = trades.OrderBy(t => t.EntryTime).First().EntryTime,
                Balance = initialBalance
            });
        }

        // Add point for each trade exit
        foreach (var trade in trades.OrderBy(t => t.ExitTime))
        {
            if (trade.ExitTime.HasValue)
            {
                runningBalance += trade.Pnl ?? 0;
                curve.Add(new EquityPoint
                {
                    Time = trade.ExitTime.Value,
                    Balance = runningBalance
                });
            }
        }

        return curve;
    }
}

/// <summary>
/// Configuration options for backtest API
/// </summary>
public class BacktestApiOptions
{
    public const string SectionName = "BacktestApi";

    /// <summary>
    /// API key for authentication. If empty, no authentication is required.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Maximum time range allowed for backtests in days
    /// </summary>
    public int MaxTimeRangeDays { get; set; } = 730; // 2 years default

    /// <summary>
    /// Maximum time range allowed for backtests as TimeSpan
    /// </summary>
    public TimeSpan MaxTimeRange => TimeSpan.FromDays(MaxTimeRangeDays);
}
