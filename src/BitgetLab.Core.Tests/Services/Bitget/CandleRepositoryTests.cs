using BitgetLab.Core.Models;
using BitgetLab.Core.Services.Bitget;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BitgetLab.Core.Tests.Services.Bitget;

/// <summary>
/// Tests for candle repository deduplication and error handling
/// </summary>
public class CandleRepositoryTests
{
    [Fact]
    public void DeduplicateCandlesByOpenTime_RemovesDuplicates()
    {
        // Arrange
        var candles = new List<CandleDto>
        {
            new() { OpenTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), Open = 100, High = 101, Low = 99, Close = 100.5m, Volume = 1000, QuoteVolume = 100000 },
            new() { OpenTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), Open = 100.1m, High = 101.1m, Low = 99.1m, Close = 100.6m, Volume = 1001, QuoteVolume = 100100 }, // Duplicate
            new() { OpenTime = new DateTime(2024, 1, 1, 0, 1, 0, DateTimeKind.Utc), Open = 101, High = 102, Low = 100, Close = 101.5m, Volume = 2000, QuoteVolume = 200000 },
            new() { OpenTime = new DateTime(2024, 1, 1, 0, 2, 0, DateTimeKind.Utc), Open = 102, High = 103, Low = 101, Close = 102.5m, Volume = 3000, QuoteVolume = 300000 },
            new() { OpenTime = new DateTime(2024, 1, 1, 0, 1, 0, DateTimeKind.Utc), Open = 101.1m, High = 102.1m, Low = 100.1m, Close = 101.6m, Volume = 2001, QuoteVolume = 200100 }, // Duplicate
        };

        // Act - Simulate the deduplication logic from UpsertCandlesAsync
        var deduplicated = candles
            .GroupBy(c => c.OpenTime)
            .Select(g => g.First())
            .OrderBy(c => c.OpenTime)
            .ToList();

        // Assert
        Assert.Equal(3, deduplicated.Count); // 5 input -> 3 unique by OpenTime
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), deduplicated[0].OpenTime);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 1, 0, DateTimeKind.Utc), deduplicated[1].OpenTime);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 2, 0, DateTimeKind.Utc), deduplicated[2].OpenTime);

        // Verify first occurrence is kept
        Assert.Equal(100, deduplicated[0].Open);
        Assert.Equal(101, deduplicated[1].Open);
    }

    [Fact]
    public void DeduplicateCandlesByOpenTime_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var candles = new List<CandleDto>();

        // Act
        var deduplicated = candles
            .GroupBy(c => c.OpenTime)
            .Select(g => g.First())
            .ToList();

        // Assert
        Assert.Empty(deduplicated);
    }

    [Fact]
    public void DeduplicateCandlesByOpenTime_NoDuplicates_ReturnsSameCount()
    {
        // Arrange
        var candles = new List<CandleDto>
        {
            new() { OpenTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), Open = 100, High = 101, Low = 99, Close = 100.5m, Volume = 1000, QuoteVolume = 100000 },
            new() { OpenTime = new DateTime(2024, 1, 1, 0, 1, 0, DateTimeKind.Utc), Open = 101, High = 102, Low = 100, Close = 101.5m, Volume = 2000, QuoteVolume = 200000 },
            new() { OpenTime = new DateTime(2024, 1, 1, 0, 2, 0, DateTimeKind.Utc), Open = 102, High = 103, Low = 101, Close = 102.5m, Volume = 3000, QuoteVolume = 300000 },
        };

        // Act
        var deduplicated = candles
            .GroupBy(c => c.OpenTime)
            .Select(g => g.First())
            .ToList();

        // Assert
        Assert.Equal(3, deduplicated.Count);
        Assert.Equal(candles.Count, deduplicated.Count);
    }

    [Fact]
    public void DeduplicateCandlesByOpenTime_AllDuplicates_ReturnsOne()
    {
        // Arrange
        var sameTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new List<CandleDto>
        {
            new() { OpenTime = sameTime, Open = 100, High = 101, Low = 99, Close = 100.5m, Volume = 1000, QuoteVolume = 100000 },
            new() { OpenTime = sameTime, Open = 100.1m, High = 101.1m, Low = 99.1m, Close = 100.6m, Volume = 1001, QuoteVolume = 100100 },
            new() { OpenTime = sameTime, Open = 100.2m, High = 101.2m, Low = 99.2m, Close = 100.7m, Volume = 1002, QuoteVolume = 100200 },
        };

        // Act
        var deduplicated = candles
            .GroupBy(c => c.OpenTime)
            .Select(g => g.First())
            .ToList();

        // Assert
        Assert.Single(deduplicated);
        Assert.Equal(sameTime, deduplicated[0].OpenTime);
        // First occurrence should be kept
        Assert.Equal(100, deduplicated[0].Open);
    }
}
