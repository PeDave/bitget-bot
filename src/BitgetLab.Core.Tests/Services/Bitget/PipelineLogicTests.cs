using BitgetLab.Core.Options;
using Xunit;

namespace BitgetLab.Core.Tests.Services.Bitget;

/// <summary>
/// Tests for pipeline retention and interval categorization logic
/// </summary>
public class PipelineLogicTests
{
    [Fact]
    public void RetentionCalculation_ComputesCorrectCutoffTime()
    {
        // Arrange
        var lookbackDays = 45;
        var currentTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        
        // Act
        var cutoffTime = currentTime.AddDays(-lookbackDays);
        
        // Assert
        var expectedCutoff = new DateTime(2023, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedCutoff, cutoffTime);
    }

    [Fact]
    public void RetentionMaxRows_ShouldDeleteOldestWhenExceeded()
    {
        // Arrange
        var maxRows = 10000;
        var currentRowCount = 12000;
        
        // Act
        var shouldDelete = currentRowCount > maxRows;
        var rowsToDelete = currentRowCount - maxRows;
        
        // Assert
        Assert.True(shouldDelete);
        Assert.Equal(2000, rowsToDelete);
    }

    [Theory]
    [InlineData("15m", true)]
    [InlineData("30m", true)]
    [InlineData("1h", true)]
    [InlineData("4h", false)]
    [InlineData("1d", false)]
    public void IntervalCategorization_IdentifiesWsIntervals(string interval, bool expectedIsWs)
    {
        // Arrange
        var options = new PipelineOptions
        {
            WsIntervals = new[] { "15m", "30m", "1h" },
            RestIntervals = new[] { "4h", "1d" }
        };
        
        // Act
        var isWsInterval = options.WsIntervals.Contains(interval);
        
        // Assert
        Assert.Equal(expectedIsWs, isWsInterval);
    }

    [Theory]
    [InlineData("4h", true)]
    [InlineData("1d", true)]
    [InlineData("15m", false)]
    [InlineData("30m", false)]
    [InlineData("1h", false)]
    public void IntervalCategorization_IdentifiesRestIntervals(string interval, bool expectedIsRest)
    {
        // Arrange
        var options = new PipelineOptions
        {
            WsIntervals = new[] { "15m", "30m", "1h" },
            RestIntervals = new[] { "4h", "1d" }
        };
        
        // Act
        var isRestInterval = options.RestIntervals.Contains(interval);
        
        // Assert
        Assert.Equal(expectedIsRest, isRestInterval);
    }

    [Theory]
    [InlineData("15m", 45)]
    [InlineData("30m", 90)]
    [InlineData("1h", 180)]
    [InlineData("4h", 365)]
    [InlineData("1d", 730)]
    public void LookbackConfiguration_ContainsAllIntervals(string interval, int expectedDays)
    {
        // Arrange
        var options = new PipelineOptions();
        
        // Act
        var hasInterval = options.LookbackDays.TryGetValue(interval, out var actualDays);
        
        // Assert
        Assert.True(hasInterval);
        Assert.Equal(expectedDays, actualDays);
    }

    [Fact]
    public void PipelineOptions_HasCorrectDefaults()
    {
        // Arrange & Act
        var options = new PipelineOptions();
        
        // Assert
        Assert.True(options.Enabled);
        Assert.Equal(new[] { "15m", "30m", "1h" }, options.WsIntervals);
        Assert.Equal(new[] { "4h", "1d" }, options.RestIntervals);
        Assert.Equal(30, options.SyncEveryMinutes);
        Assert.Equal(10000, options.MaxRows);
        Assert.Equal(5, options.LookbackDays.Count);
    }

    [Fact]
    public void RetentionWindow_ComputesCorrectTimeRange()
    {
        // Arrange
        var lookbackDays = 180;
        var endTime = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Act
        var startTime = endTime.AddDays(-lookbackDays);
        var timeSpan = endTime - startTime;
        
        // Assert
        Assert.Equal(180, timeSpan.TotalDays);
        Assert.Equal(new DateTime(2023, 12, 4, 0, 0, 0, DateTimeKind.Utc), startTime);
    }
}
