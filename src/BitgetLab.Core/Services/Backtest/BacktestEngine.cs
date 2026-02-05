using BitgetLab.Core.Models;
using BitgetLab.Core.Services.Bitget;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// Interface for running backtests
/// </summary>
public interface IBacktestEngine
{
    Task<BacktestResult> RunBacktestAsync(
        string symbol,
        string interval,
        DateTime startTime,
        DateTime endTime,
        IStrategy strategy,
        BacktestConfig config,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for backtest execution
/// </summary>
public class BacktestConfig
{
    public decimal InitialBalance { get; set; } = 10000m;
    public decimal FeeBps { get; set; } = 10m; // 0.1% default
    public decimal SlippageBps { get; set; } = 5m; // 0.05% default
}

/// <summary>
/// Result of a backtest run
/// </summary>
public class BacktestResult
{
    public List<BacktestTradeDto> Trades { get; set; } = new();
    public BacktestSummary Summary { get; set; } = new();
}

/// <summary>
/// Backtest engine implementation
/// </summary>
public class BacktestEngine : IBacktestEngine
{
    private readonly ICandleService _candleService;

    public BacktestEngine(ICandleService candleService)
    {
        _candleService = candleService;
    }

    public async Task<BacktestResult> RunBacktestAsync(
        string symbol,
        string interval,
        DateTime startTime,
        DateTime endTime,
        IStrategy strategy,
        BacktestConfig config,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default)
    {
        // Load candles
        var candles = await _candleService.GetCandlesAsync(
            symbol, interval, startTime, endTime, 1000, market, cancellationToken);
        
        var candleList = candles.OrderBy(c => c.OpenTime).ToList();

        if (candleList.Count == 0)
        {
            throw new InvalidOperationException("No candles found for the specified date range");
        }

        // Generate signals
        var signals = strategy.GenerateSignals(candleList).OrderBy(s => s.Time).ToList();

        // Simulate trades
        var trades = SimulateTrades(signals, config);

        // Calculate summary
        var summary = CalculateSummary(trades, config.InitialBalance);

        return new BacktestResult
        {
            Trades = trades,
            Summary = summary
        };
    }

    private List<BacktestTradeDto> SimulateTrades(List<TradingSignal> signals, BacktestConfig config)
    {
        var trades = new List<BacktestTradeDto>();
        BacktestTradeDto? openTrade = null;
        var balance = config.InitialBalance;

        foreach (var signal in signals)
        {
            if (signal.Type == SignalType.Buy && openTrade == null)
            {
                // Open long position
                var entryPrice = ApplySlippage(signal.Price, config.SlippageBps, isBuy: true);
                var qty = (balance * 0.95m) / entryPrice; // Use 95% of balance
                var fee = qty * entryPrice * (config.FeeBps / 10000m);
                
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
                
                balance -= (qty * entryPrice + fee);
            }
            else if (signal.Type == SignalType.Sell && openTrade != null)
            {
                // Close long position
                var exitPrice = ApplySlippage(signal.Price, config.SlippageBps, isBuy: false);
                var exitValue = openTrade.Qty * exitPrice;
                var fee = exitValue * (config.FeeBps / 10000m);
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
                
                balance += (exitValue - fee);
                trades.Add(openTrade);
                openTrade = null;
            }
        }

        // Close any open position at the end (if signals were incomplete)
        // We'll leave it open and not count it in final results

        return trades;
    }

    private decimal ApplySlippage(decimal price, decimal slippageBps, bool isBuy)
    {
        var slippageMultiplier = slippageBps / 10000m;
        return isBuy 
            ? price * (1 + slippageMultiplier)  // Pay more when buying
            : price * (1 - slippageMultiplier); // Receive less when selling
    }

    private BacktestSummary CalculateSummary(List<BacktestTradeDto> trades, decimal initialBalance)
    {
        var winningTrades = trades.Where(t => t.Pnl > 0).ToList();
        var losingTrades = trades.Where(t => t.Pnl <= 0).ToList();
        var totalPnl = trades.Sum(t => t.Pnl ?? 0);
        var finalBalance = initialBalance + totalPnl;

        // Calculate profit factor (total winning / total losing)
        var totalWinning = winningTrades.Sum(t => t.Pnl ?? 0);
        var totalLosing = Math.Abs(losingTrades.Sum(t => t.Pnl ?? 0));
        var profitFactor = totalLosing > 0 ? totalWinning / totalLosing : (totalWinning > 0 ? 999m : 0m);

        // Calculate max drawdown
        var runningBalance = initialBalance;
        var peak = initialBalance;
        var maxDrawdown = 0m;
        
        // Use first trade entry time or current time if no trades
        var startTime = trades.Count > 0 ? trades.OrderBy(t => t.EntryTime).First().EntryTime : DateTime.UtcNow;
        
        var equityCurve = new List<EquityPoint>
        {
            new EquityPoint { Time = startTime, Balance = initialBalance }
        };

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
                if (drawdown > maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }

                equityCurve.Add(new EquityPoint 
                { 
                    Time = trade.ExitTime.Value, 
                    Balance = runningBalance 
                });
            }
        }

        return new BacktestSummary
        {
            TotalTrades = trades.Count,
            WinningTrades = winningTrades.Count,
            LosingTrades = losingTrades.Count,
            WinRate = trades.Count > 0 ? (decimal)winningTrades.Count / trades.Count * 100 : 0,
            NetPnl = totalPnl,
            MaxDrawdown = maxDrawdown,
            InitialBalance = initialBalance,
            FinalBalance = finalBalance,
            ReturnPercent = initialBalance > 0 ? (finalBalance - initialBalance) / initialBalance * 100 : 0,
            ProfitFactor = profitFactor,
            EquityCurve = equityCurve
        };
    }
}
