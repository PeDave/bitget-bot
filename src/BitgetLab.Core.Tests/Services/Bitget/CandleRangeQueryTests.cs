using BitgetLab.Core.Models;
using BitgetLab.Core.Services.Bitget;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BitgetLab.Core.Tests.Services.Bitget;

/// <summary>
/// Tests for candle range query with warmup functionality
/// NOTE: These tests require a PostgreSQL database to be configured.
/// If no database is available, tests will be skipped gracefully.
/// </summary>
public class CandleRangeQueryTests
{
    private readonly PostgresCandleRepository _repository;
    private readonly bool _dbAvailable;

    public CandleRangeQueryTests()
    {
        // Create a minimal configuration - tests will skip if DB not available
        var configuration = new ConfigurationBuilder().Build();

        var logger = Mock.Of<ILogger<PostgresCandleRepository>>();
        _repository = new PostgresCandleRepository(configuration, logger);
        _dbAvailable = _repository.IsAvailableAsync().Result;
    }

    [Fact]
    public void CandleRangeResponse_ValidData_CreatesSuccessfully()
    {
        // Verify the response model can be instantiated with valid data
        var response = new CandleRangeResponse
        {
            Symbol = "BTCUSDT",
            Market = "spot",
            Interval = "1h",
            Start = DateTime.Parse("2024-01-01T00:00:00Z").ToUniversalTime(),
            End = DateTime.Parse("2024-01-02T00:00:00Z").ToUniversalTime(),
            WarmupCandles = 200,
            Count = 250,
            Candles = new List<CandleDto>()
        };

        Assert.Equal("BTCUSDT", response.Symbol);
        Assert.Equal("spot", response.Market);
        Assert.Equal("1h", response.Interval);
        Assert.Equal(200, response.WarmupCandles);
        Assert.Equal(250, response.Count);
        Assert.NotNull(response.Candles);
    }

    [Fact]
    public async Task GetCandlesWithWarmupAsync_NoDatabase_ReturnsEmpty()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build(); // No connection string
        var logger = Mock.Of<ILogger<PostgresCandleRepository>>();
        var repository = new PostgresCandleRepository(configuration, logger);

        // Act
        var candles = await repository.GetCandlesWithWarmupAsync(
            "BTCUSDT",
            "1h",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            200,
            MarketType.Spot);

        // Assert
        Assert.Empty(candles);
    }

    [Fact]
    public async Task GetCandlesWithWarmupAsync_EmptyDatabase_ReturnsEmpty()
    {
        if (!_dbAvailable)
        {
            // Skip test if database is not available
            return;
        }

        // Act - Query for a non-existent symbol
        var candles = await _repository.GetCandlesWithWarmupAsync(
            "NONEXISTENT_SYMBOL_TEST",
            "1h",
            DateTime.Parse("2024-01-01T00:00:00Z").ToUniversalTime(),
            DateTime.Parse("2024-01-02T00:00:00Z").ToUniversalTime(),
            50,
            MarketType.Spot);

        // Assert
        Assert.Empty(candles);
    }

    [Fact]
    public async Task GetCandlesWithWarmupAsync_WithData_ReturnsOrderedCandles()
    {
        if (!_dbAvailable)
        {
            // Skip test if database is not available
            return;
        }

        // Arrange - Insert test candles
        var symbol = $"TEST_SYMBOL_{Guid.NewGuid():N}";
        var interval = "1h";
        var baseTime = DateTime.Parse("2024-01-01T00:00:00Z").ToUniversalTime();
        
        var testCandles = new List<CandleDto>();
        for (int i = 0; i < 300; i++)
        {
            testCandles.Add(new CandleDto
            {
                OpenTime = baseTime.AddHours(i),
                Open = 100m + i,
                High = 105m + i,
                Low = 95m + i,
                Close = 102m + i,
                Volume = 1000m,
                QuoteVolume = 100000m
            });
        }

        await _repository.UpsertCandlesAsync(symbol, interval, testCandles, MarketType.Spot);

        // Act - Query with warmup
        var startTime = baseTime.AddHours(200); // Start at hour 200
        var endTime = baseTime.AddHours(250);   // End at hour 250
        var warmupCandles = 50;

        var result = await _repository.GetCandlesWithWarmupAsync(
            symbol,
            interval,
            startTime,
            endTime,
            warmupCandles,
            MarketType.Spot);

        var resultList = result.ToList();

        // Assert
        Assert.NotEmpty(resultList);
        
        // Should have warmup candles (50) + main range candles (51, including both start and end)
        // Total: 101 candles
        Assert.Equal(101, resultList.Count);

        // Verify ascending order
        for (int i = 1; i < resultList.Count; i++)
        {
            Assert.True(resultList[i].OpenTime > resultList[i - 1].OpenTime,
                $"Candles should be in ascending order. Index {i}: {resultList[i].OpenTime} should be > {resultList[i - 1].OpenTime}");
        }

        // Verify first candle is a warmup candle (before start time)
        Assert.True(resultList[0].OpenTime < startTime,
            $"First candle should be before start time. Got {resultList[0].OpenTime}, expected < {startTime}");

        // Verify last candle is within or at end of range
        Assert.True(resultList[^1].OpenTime <= endTime,
            $"Last candle should be at or before end time. Got {resultList[^1].OpenTime}, expected <= {endTime}");

        // Verify no duplicates
        var distinctTimes = resultList.Select(c => c.OpenTime).Distinct().ToList();
        Assert.Equal(resultList.Count, distinctTimes.Count);
    }

    [Fact]
    public async Task GetCandlesWithWarmupAsync_InsufficientWarmupCandles_ReturnsAvailable()
    {
        if (!_dbAvailable)
        {
            // Skip test if database is not available
            return;
        }

        // Arrange - Insert only 20 candles
        var symbol = $"TEST_SYMBOL_{Guid.NewGuid():N}";
        var interval = "15m";
        var baseTime = DateTime.Parse("2024-01-01T00:00:00Z").ToUniversalTime();
        
        var testCandles = new List<CandleDto>();
        for (int i = 0; i < 20; i++)
        {
            testCandles.Add(new CandleDto
            {
                OpenTime = baseTime.AddMinutes(i * 15),
                Open = 100m,
                High = 105m,
                Low = 95m,
                Close = 102m,
                Volume = 1000m,
                QuoteVolume = 100000m
            });
        }

        await _repository.UpsertCandlesAsync(symbol, interval, testCandles, MarketType.Spot);

        // Act - Query for 100 warmup candles but only 10 available before start
        var startTime = baseTime.AddMinutes(10 * 15); // Start at 10th candle
        var endTime = baseTime.AddMinutes(19 * 15);   // End at last candle
        var warmupCandles = 100;

        var result = await _repository.GetCandlesWithWarmupAsync(
            symbol,
            interval,
            startTime,
            endTime,
            warmupCandles,
            MarketType.Spot);

        var resultList = result.ToList();

        // Assert
        Assert.NotEmpty(resultList);
        
        // Should have 10 warmup candles + 10 main range candles = 20 total
        Assert.Equal(20, resultList.Count);

        // Verify ascending order
        for (int i = 1; i < resultList.Count; i++)
        {
            Assert.True(resultList[i].OpenTime > resultList[i - 1].OpenTime);
        }
    }

    [Fact]
    public async Task GetCandlesWithWarmupAsync_ZeroWarmupCandles_ReturnsOnlyRange()
    {
        if (!_dbAvailable)
        {
            // Skip test if database is not available
            return;
        }

        // Arrange
        var symbol = $"TEST_SYMBOL_{Guid.NewGuid():N}";
        var interval = "5m";
        var baseTime = DateTime.Parse("2024-01-01T00:00:00Z").ToUniversalTime();
        
        var testCandles = new List<CandleDto>();
        for (int i = 0; i < 50; i++)
        {
            testCandles.Add(new CandleDto
            {
                OpenTime = baseTime.AddMinutes(i * 5),
                Open = 100m,
                High = 105m,
                Low = 95m,
                Close = 102m,
                Volume = 1000m,
                QuoteVolume = 100000m
            });
        }

        await _repository.UpsertCandlesAsync(symbol, interval, testCandles, MarketType.Spot);

        // Act - Query with zero warmup candles
        var startTime = baseTime.AddMinutes(20 * 5); // Start at 20th candle
        var endTime = baseTime.AddMinutes(29 * 5);   // End at 29th candle
        var warmupCandles = 0;

        var result = await _repository.GetCandlesWithWarmupAsync(
            symbol,
            interval,
            startTime,
            endTime,
            warmupCandles,
            MarketType.Spot);

        var resultList = result.ToList();

        // Assert
        Assert.NotEmpty(resultList);
        
        // Should have only main range candles (10 candles)
        Assert.Equal(10, resultList.Count);

        // Verify all candles are within range
        Assert.All(resultList, candle =>
        {
            Assert.True(candle.OpenTime >= startTime && candle.OpenTime <= endTime);
        });
    }

    [Fact]
    public async Task GetCandlesWithWarmupAsync_DifferentMarkets_IsolatesData()
    {
        if (!_dbAvailable)
        {
            // Skip test if database is not available
            return;
        }

        // Arrange - Insert candles for both spot and futures
        var symbol = $"TEST_SYMBOL_{Guid.NewGuid():N}";
        var interval = "1h";
        var baseTime = DateTime.Parse("2024-01-01T00:00:00Z").ToUniversalTime();
        
        var spotCandles = new List<CandleDto>();
        var futuresCandles = new List<CandleDto>();
        
        for (int i = 0; i < 50; i++)
        {
            spotCandles.Add(new CandleDto
            {
                OpenTime = baseTime.AddHours(i),
                Open = 100m,
                High = 105m,
                Low = 95m,
                Close = 102m,
                Volume = 1000m,
                QuoteVolume = 100000m
            });
            
            futuresCandles.Add(new CandleDto
            {
                OpenTime = baseTime.AddHours(i),
                Open = 200m, // Different prices to distinguish
                High = 205m,
                Low = 195m,
                Close = 202m,
                Volume = 2000m,
                QuoteVolume = 200000m
            });
        }

        await _repository.UpsertCandlesAsync(symbol, interval, spotCandles, MarketType.Spot);
        await _repository.UpsertCandlesAsync(symbol, interval, futuresCandles, MarketType.Futures);

        // Act - Query spot market
        var startTime = baseTime.AddHours(10);
        var endTime = baseTime.AddHours(20);
        var warmupCandles = 5;

        var spotResult = await _repository.GetCandlesWithWarmupAsync(
            symbol,
            interval,
            startTime,
            endTime,
            warmupCandles,
            MarketType.Spot);

        var futuresResult = await _repository.GetCandlesWithWarmupAsync(
            symbol,
            interval,
            startTime,
            endTime,
            warmupCandles,
            MarketType.Futures);

        var spotList = spotResult.ToList();
        var futuresList = futuresResult.ToList();

        // Assert - Both should have data
        Assert.NotEmpty(spotList);
        Assert.NotEmpty(futuresList);
        Assert.Equal(spotList.Count, futuresList.Count);

        // Assert - Spot candles should have price ~100
        Assert.All(spotList, candle =>
        {
            Assert.True(candle.Open >= 90m && candle.Open <= 110m);
        });

        // Assert - Futures candles should have price ~200
        Assert.All(futuresList, candle =>
        {
            Assert.True(candle.Open >= 190m && candle.Open <= 210m);
        });
    }
}
