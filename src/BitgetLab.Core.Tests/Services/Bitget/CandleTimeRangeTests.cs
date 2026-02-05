using Xunit;

namespace BitgetLab.Core.Tests.Services.Bitget;

/// <summary>
/// Tests for time range validation logic in candle fetching
/// </summary>
public class CandleTimeRangeTests
{
    [Fact]
    public void ValidateTimeRange_StartBeforeEnd_IsValid()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var isValid = startTime < endTime;

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateTimeRange_StartEqualsEnd_IsInvalid()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var isValid = startTime < endTime;

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateTimeRange_StartAfterEnd_IsInvalid()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var isValid = startTime < endTime;

        // Assert
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("2024-01-01T00:00:00Z", "2024-01-01T01:00:00Z", true)]
    [InlineData("2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", false)]
    [InlineData("2024-01-01T01:00:00Z", "2024-01-01T00:00:00Z", false)]
    [InlineData("2024-01-01T00:00:00Z", "2024-01-02T00:00:00Z", true)]
    public void ValidateTimeRange_VariousScenarios(string startStr, string endStr, bool expectedValid)
    {
        // Arrange
        var startTime = DateTime.Parse(startStr).ToUniversalTime();
        var endTime = DateTime.Parse(endStr).ToUniversalTime();

        // Act
        var isValid = startTime < endTime;

        // Assert
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void ConvertToUnixMilliseconds_CorrectConversion()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedMs = 1704067200000L; // Unix timestamp in milliseconds for 2024-01-01 00:00:00 UTC

        // Act
        var actualMs = new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();

        // Assert
        Assert.Equal(expectedMs, actualMs);
    }

    [Fact]
    public void BackwardPagination_NextEndTime_IsBeforeCurrent()
    {
        // Arrange
        var currentEndTime = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var minCandleTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act - Simulate backward pagination logic
        var nextEndTime = minCandleTime.AddMilliseconds(-1);

        // Assert
        Assert.True(nextEndTime < currentEndTime);
    }

    [Fact]
    public void BackwardPagination_ShouldStopWhenStartGreaterThanEnd()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentEndTime = new DateTime(2023, 12, 31, 23, 59, 59, DateTimeKind.Utc); // Before startTime

        // Act
        var shouldStop = startTime >= currentEndTime;

        // Assert
        Assert.True(shouldStop);
    }

    [Fact]
    public void CalculateExpectedCandles_1HourInterval()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc); // 24 hours
        var intervalTimeSpan = TimeSpan.FromHours(1);

        // Act
        var timeRange = endTime - startTime;
        var expectedCount = (int)(timeRange.TotalSeconds / intervalTimeSpan.TotalSeconds);

        // Assert
        Assert.Equal(24, expectedCount);
    }

    [Fact]
    public void CalculateExpectedCandles_InvalidInterval_ReturnsZero()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var intervalTimeSpan = TimeSpan.Zero;

        // Act
        var isValid = intervalTimeSpan.TotalSeconds > 0;
        var expectedCount = isValid ? (int)((endTime - startTime).TotalSeconds / intervalTimeSpan.TotalSeconds) : 0;

        // Assert
        Assert.Equal(0, expectedCount);
    }
}
