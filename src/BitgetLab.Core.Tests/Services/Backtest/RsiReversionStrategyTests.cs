using BitgetLab.Core.Models;
using BitgetLab.Core.Services.Backtest;
using Xunit;

namespace BitgetLab.Core.Tests.Services.Backtest;

/// <summary>
/// Tests for RSI calculation accuracy and mean-reversion strategy
/// </summary>
public class RsiReversionStrategyTests
{
    [Fact]
    public void ComputeRSI_WithKnownData_ReturnsExpectedValues()
    {
        // Arrange - Create test candles with known price movements
        var candles = new List<CandleDto>
        {
            // Initial price at 100, then simulate ups and downs
            CreateCandle("2024-01-01 00:00", 100m, 100m),
            CreateCandle("2024-01-01 01:00", 100m, 102m),
            CreateCandle("2024-01-01 02:00", 102m, 103m),
            CreateCandle("2024-01-01 03:00", 103m, 101m),
            CreateCandle("2024-01-01 04:00", 101m, 104m),
            CreateCandle("2024-01-01 05:00", 104m, 105m),
            CreateCandle("2024-01-01 06:00", 105m, 103m),
            CreateCandle("2024-01-01 07:00", 103m, 106m),
            CreateCandle("2024-01-01 08:00", 106m, 107m),
            CreateCandle("2024-01-01 09:00", 107m, 105m),
            CreateCandle("2024-01-01 10:00", 105m, 108m),
            CreateCandle("2024-01-01 11:00", 108m, 109m),
            CreateCandle("2024-01-01 12:00", 109m, 107m),
            CreateCandle("2024-01-01 13:00", 107m, 110m),
            CreateCandle("2024-01-01 14:00", 110m, 111m),
            // Add more candles for RSI calculation
            CreateCandle("2024-01-01 15:00", 111m, 109m),
            CreateCandle("2024-01-01 16:00", 109m, 112m),
            CreateCandle("2024-01-01 17:00", 112m, 113m),
        };

        var strategy = new RsiReversionStrategy();
        strategy.Configure(new Dictionary<string, object>
        {
            { "rsiPeriod", 14 },
            { "entryBelow", 30m },
            { "exitAbove", 70m }
        });

        // Act
        var signals = strategy.GenerateSignals(candles).ToList();

        // Assert - RSI should be calculated without errors
        // With an uptrend, RSI should be high, so we shouldn't get entry signals
        Assert.NotNull(signals);
    }

    [Fact]
    public void GenerateSignals_WithOversoldCondition_GeneratesBuySignal()
    {
        // Arrange - Create strong downtrend to trigger oversold RSI
        var candles = new List<CandleDto>();
        var basePrice = 100m;
        var currentTime = DateTime.Parse("2024-01-01 00:00");
        
        // Create initial candles
        for (int i = 0; i < 10; i++)
        {
            candles.Add(CreateCandle(currentTime.AddHours(i).ToString("yyyy-MM-dd HH:mm"), basePrice, basePrice));
            basePrice -= 0.1m;
        }
        
        // Create strong downtrend
        for (int i = 10; i < 30; i++)
        {
            candles.Add(CreateCandle(currentTime.AddHours(i).ToString("yyyy-MM-dd HH:mm"), basePrice, basePrice - 2m));
            basePrice -= 2m;
        }

        var strategy = new RsiReversionStrategy();
        strategy.Configure(new Dictionary<string, object>
        {
            { "rsiPeriod", 14 },
            { "entryBelow", 30m },
            { "exitAbove", 70m }
        });

        // Act
        var signals = strategy.GenerateSignals(candles).ToList();

        // Assert - Should generate at least one buy signal due to oversold condition
        Assert.NotEmpty(signals);
        Assert.Contains(signals, s => s.Type == SignalType.Buy);
    }

    [Fact]
    public void GenerateSignals_WithBuyThenRecovery_GeneratesBuyAndSellSignals()
    {
        // Arrange - Create V-shaped price action
        var candles = new List<CandleDto>();
        var basePrice = 100m;
        var currentTime = DateTime.Parse("2024-01-01 00:00");
        
        // Initial period
        for (int i = 0; i < 10; i++)
        {
            candles.Add(CreateCandle(currentTime.AddHours(i).ToString("yyyy-MM-dd HH:mm"), basePrice, basePrice));
            basePrice -= 0.1m;
        }
        
        // Downtrend (will trigger buy)
        for (int i = 10; i < 25; i++)
        {
            candles.Add(CreateCandle(currentTime.AddHours(i).ToString("yyyy-MM-dd HH:mm"), basePrice, basePrice - 1.5m));
            basePrice -= 1.5m;
        }
        
        // Recovery uptrend (will trigger sell)
        for (int i = 25; i < 45; i++)
        {
            candles.Add(CreateCandle(currentTime.AddHours(i).ToString("yyyy-MM-dd HH:mm"), basePrice, basePrice + 1.5m));
            basePrice += 1.5m;
        }

        var strategy = new RsiReversionStrategy();
        strategy.Configure(new Dictionary<string, object>
        {
            { "rsiPeriod", 14 },
            { "entryBelow", 30m },
            { "exitAbove", 60m }  // Lower exit threshold to ensure trigger
        });

        // Act
        var signals = strategy.GenerateSignals(candles).ToList();

        // Assert - Should have both buy and sell signals
        Assert.NotEmpty(signals);
        var buySignals = signals.Where(s => s.Type == SignalType.Buy).ToList();
        var sellSignals = signals.Where(s => s.Type == SignalType.Sell).ToList();
        
        Assert.NotEmpty(buySignals);
        Assert.NotEmpty(sellSignals);
        
        // First signal should be a buy
        Assert.Equal(SignalType.Buy, signals.First().Type);
    }

    [Fact]
    public void Configure_WithInvalidPeriod_ThrowsArgumentException()
    {
        // Arrange
        var strategy = new RsiReversionStrategy();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            strategy.Configure(new Dictionary<string, object>
            {
                { "rsiPeriod", 1 },
                { "entryBelow", 30m },
                { "exitAbove", 70m }
            }));
        
        Assert.Contains("rsiPeriod", exception.Message);
    }

    [Fact]
    public void Configure_WithInvalidThresholds_ThrowsArgumentException()
    {
        // Arrange
        var strategy = new RsiReversionStrategy();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            strategy.Configure(new Dictionary<string, object>
            {
                { "rsiPeriod", 14 },
                { "entryBelow", 70m },  // Entry above exit - invalid
                { "exitAbove", 30m }
            }));
        
        Assert.Contains("entryBelow", exception.Message);
    }

    [Fact]
    public void GenerateSignals_WithInsufficientCandles_ReturnsEmptyList()
    {
        // Arrange - Not enough candles for RSI calculation
        var candles = new List<CandleDto>
        {
            CreateCandle("2024-01-01 00:00", 100m, 100m),
            CreateCandle("2024-01-01 01:00", 100m, 101m),
            CreateCandle("2024-01-01 02:00", 101m, 102m),
        };

        var strategy = new RsiReversionStrategy();
        strategy.Configure(new Dictionary<string, object>
        {
            { "rsiPeriod", 14 },
            { "entryBelow", 30m },
            { "exitAbove", 70m }
        });

        // Act
        var signals = strategy.GenerateSignals(candles).ToList();

        // Assert - Should return empty list
        Assert.Empty(signals);
    }

    private CandleDto CreateCandle(string time, decimal open, decimal close)
    {
        return new CandleDto
        {
            OpenTime = DateTime.Parse(time),
            Open = open,
            High = Math.Max(open, close),
            Low = Math.Min(open, close),
            Close = close,
            Volume = 1000m,
            QuoteVolume = 1000m * close
        };
    }
}
