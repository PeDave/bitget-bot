using BitgetLab.Core.Options;
using BitgetLab.Core.Services.Bitget;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BitgetLab.Core.Tests.Services.Bitget;

/// <summary>
/// Tests for the futures pipeline retention logic and interval categorization
/// </summary>
public class FuturesPipelineTests
{
    private readonly ILogger<CandleRetentionService> _logger;
    private readonly FuturesPipelineOptions _pipelineOptions;

    public FuturesPipelineTests()
    {
        _logger = new LoggerFactory().CreateLogger<CandleRetentionService>();
        
        // Default pipeline options for testing
        _pipelineOptions = new FuturesPipelineOptions
        {
            Enabled = true,
            WsIntervals = new List<string> { "15m", "30m", "1h" },
            RestIntervals = new List<string> { "4h", "1d" },
            LookbackDays = new Dictionary<string, int>
            {
                { "15m", 45 },
                { "30m", 90 },
                { "1h", 180 },
                { "4h", 365 },
                { "1d", 730 }
            },
            MaxRows = 10000,
            SyncEveryMinutes = 30
        };
    }

    [Fact]
    public void PipelineOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new FuturesPipelineOptions();

        // Assert
        Assert.False(options.Enabled);
        Assert.Equal(3, options.WsIntervals.Count);
        Assert.Contains("15m", options.WsIntervals);
        Assert.Contains("30m", options.WsIntervals);
        Assert.Contains("1h", options.WsIntervals);
        Assert.Equal(2, options.RestIntervals.Count);
        Assert.Contains("4h", options.RestIntervals);
        Assert.Contains("1d", options.RestIntervals);
        Assert.Equal(10000, options.MaxRows);
        Assert.Equal(30, options.SyncEveryMinutes);
    }

    [Fact]
    public void PipelineOptions_LookbackDays_AreCorrect()
    {
        // Arrange & Act
        var options = new FuturesPipelineOptions();

        // Assert
        Assert.Equal(45, options.LookbackDays["15m"]);
        Assert.Equal(90, options.LookbackDays["30m"]);
        Assert.Equal(180, options.LookbackDays["1h"]);
        Assert.Equal(365, options.LookbackDays["4h"]);
        Assert.Equal(730, options.LookbackDays["1d"]);
    }

    [Fact]
    public void CalculateCutoffTime_WithKnownInterval_ReturnsCorrectCutoff()
    {
        // Arrange
        var service = new CandleRetentionService(
            Microsoft.Extensions.Options.Options.Create(_pipelineOptions),
            _logger,
            null);

        var now = DateTime.UtcNow;

        // Act
        var cutoff15m = service.CalculateCutoffTime("15m");
        var cutoff30m = service.CalculateCutoffTime("30m");
        var cutoff1h = service.CalculateCutoffTime("1h");
        var cutoff4h = service.CalculateCutoffTime("4h");
        var cutoff1d = service.CalculateCutoffTime("1d");

        // Assert - cutoff should be approximately lookback days ago
        Assert.True(cutoff15m >= now.AddDays(-45).AddMinutes(-1));
        Assert.True(cutoff15m <= now.AddDays(-45).AddMinutes(1));

        Assert.True(cutoff30m >= now.AddDays(-90).AddMinutes(-1));
        Assert.True(cutoff30m <= now.AddDays(-90).AddMinutes(1));

        Assert.True(cutoff1h >= now.AddDays(-180).AddMinutes(-1));
        Assert.True(cutoff1h <= now.AddDays(-180).AddMinutes(1));

        Assert.True(cutoff4h >= now.AddDays(-365).AddMinutes(-1));
        Assert.True(cutoff4h <= now.AddDays(-365).AddMinutes(1));

        Assert.True(cutoff1d >= now.AddDays(-730).AddMinutes(-1));
        Assert.True(cutoff1d <= now.AddDays(-730).AddMinutes(1));
    }

    [Fact]
    public void CalculateCutoffTime_WithUnknownInterval_UsesDefaultLookback()
    {
        // Arrange
        var service = new CandleRetentionService(
            Microsoft.Extensions.Options.Options.Create(_pipelineOptions),
            _logger,
            null);

        var now = DateTime.UtcNow;

        // Act
        var cutoff = service.CalculateCutoffTime("5m"); // Not in config

        // Assert - should use default 90 days
        Assert.True(cutoff >= now.AddDays(-90).AddMinutes(-1));
        Assert.True(cutoff <= now.AddDays(-90).AddMinutes(1));
    }

    [Theory]
    [InlineData("15m", true)]
    [InlineData("30m", true)]
    [InlineData("1h", true)]
    [InlineData("4h", false)]
    [InlineData("1d", false)]
    public void IntervalCategorization_WsIntervals_AreCorrect(string interval, bool shouldBeWs)
    {
        // Assert
        var isWs = _pipelineOptions.WsIntervals.Contains(interval);
        Assert.Equal(shouldBeWs, isWs);
    }

    [Theory]
    [InlineData("15m", false)]
    [InlineData("30m", false)]
    [InlineData("1h", false)]
    [InlineData("4h", true)]
    [InlineData("1d", true)]
    public void IntervalCategorization_RestIntervals_AreCorrect(string interval, bool shouldBeRest)
    {
        // Assert
        var isRest = _pipelineOptions.RestIntervals.Contains(interval);
        Assert.Equal(shouldBeRest, isRest);
    }

    [Fact]
    public void LookbackDays_IncreaseWithInterval_AsExpected()
    {
        // Arrange
        var intervals = new[] { "15m", "30m", "1h", "4h", "1d" };
        var expectedDays = new[] { 45, 90, 180, 365, 730 };

        // Act & Assert
        for (int i = 0; i < intervals.Length; i++)
        {
            Assert.Equal(expectedDays[i], _pipelineOptions.LookbackDays[intervals[i]]);
        }

        // Verify increasing order
        for (int i = 1; i < expectedDays.Length; i++)
        {
            Assert.True(expectedDays[i] > expectedDays[i - 1], 
                $"Lookback days should increase: {expectedDays[i-1]} < {expectedDays[i]}");
        }
    }

    [Fact]
    public void MaxRows_IsWithinReasonableRange()
    {
        // Assert
        Assert.True(_pipelineOptions.MaxRows >= 1000, "MaxRows should be at least 1000");
        Assert.True(_pipelineOptions.MaxRows <= 100000, "MaxRows should not exceed 100000");
    }

    [Fact]
    public void SyncEveryMinutes_IsWithinReasonableRange()
    {
        // Assert
        Assert.True(_pipelineOptions.SyncEveryMinutes >= 5, "Sync interval should be at least 5 minutes");
        Assert.True(_pipelineOptions.SyncEveryMinutes <= 1440, "Sync interval should not exceed 1 day (1440 minutes)");
    }

    [Fact]
    public void AllIntervals_HaveLookbackDaysConfigured()
    {
        // Arrange
        var allIntervals = _pipelineOptions.WsIntervals
            .Concat(_pipelineOptions.RestIntervals)
            .Distinct()
            .ToList();

        // Act & Assert
        foreach (var interval in allIntervals)
        {
            Assert.True(_pipelineOptions.LookbackDays.ContainsKey(interval),
                $"Interval {interval} should have lookback days configured");
            Assert.True(_pipelineOptions.LookbackDays[interval] > 0,
                $"Lookback days for {interval} should be positive");
        }
    }

    [Fact]
    public void WsAndRestIntervals_DoNotOverlap()
    {
        // Arrange
        var wsSet = new HashSet<string>(_pipelineOptions.WsIntervals);
        var restSet = new HashSet<string>(_pipelineOptions.RestIntervals);

        // Act
        var intersection = wsSet.Intersect(restSet).ToList();

        // Assert
        Assert.Empty(intersection);
    }

    [Fact]
    public void RetentionService_CalculatesCutoffCorrectly_ForMultipleIntervals()
    {
        // Arrange
        var service = new CandleRetentionService(
            Microsoft.Extensions.Options.Options.Create(_pipelineOptions),
            _logger,
            null);

        var intervals = new[] { "15m", "30m", "1h", "4h", "1d" };
        var cutoffs = new Dictionary<string, DateTime>();

        // Act
        foreach (var interval in intervals)
        {
            cutoffs[interval] = service.CalculateCutoffTime(interval);
        }

        // Assert - cutoffs should be in increasing order (older for longer intervals)
        Assert.True(cutoffs["1d"] < cutoffs["4h"], "1d cutoff should be older than 4h");
        Assert.True(cutoffs["4h"] < cutoffs["1h"], "4h cutoff should be older than 1h");
        Assert.True(cutoffs["1h"] < cutoffs["30m"], "1h cutoff should be older than 30m");
        Assert.True(cutoffs["30m"] < cutoffs["15m"], "30m cutoff should be older than 15m");
    }
}
