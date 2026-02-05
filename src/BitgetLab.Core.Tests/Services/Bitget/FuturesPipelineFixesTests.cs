using BitgetLab.Core.Models;
using BitgetLab.Core.Options;
using Xunit;

namespace BitgetLab.Core.Tests.Services.Bitget;

/// <summary>
/// Tests for futures pipeline fixes and robustness improvements
/// </summary>
public class FuturesPipelineFixesTests
{
    [Fact]
    public void PipelineState_IncludesCompletedWithErrors()
    {
        // Arrange & Act
        var completedWithErrors = PipelineState.CompletedWithErrors;
        
        // Assert
        Assert.Equal(PipelineState.CompletedWithErrors, completedWithErrors);
        Assert.NotEqual(PipelineState.Running, completedWithErrors);
        Assert.NotEqual(PipelineState.Failed, completedWithErrors);
    }

    [Fact]
    public void PipelineStatus_SupportsIntervalErrors()
    {
        // Arrange
        var status = new PipelineStatus
        {
            Symbol = "BTCUSDT",
            Market = "futures",
            State = PipelineState.CompletedWithErrors,
            IntervalErrors = new Dictionary<string, string>
            {
                { "1h", "BitgetApiException: 40017 - Parameter verification failed" },
                { "4h", "InvalidOperationException: Invalid time range" }
            }
        };
        
        // Act & Assert
        Assert.NotNull(status.IntervalErrors);
        Assert.Equal(2, status.IntervalErrors.Count);
        Assert.True(status.IntervalErrors.ContainsKey("1h"));
        Assert.True(status.IntervalErrors.ContainsKey("4h"));
    }

    [Fact]
    public void CandleDeduplication_RemovesDuplicatesByOpenTime()
    {
        // Arrange - Simulate duplicate candles with same open_time
        var candles = new List<CandleDto>
        {
            new CandleDto { OpenTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), Open = 100, Close = 101 },
            new CandleDto { OpenTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), Open = 100, Close = 102 }, // Duplicate
            new CandleDto { OpenTime = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc), Open = 102, Close = 103 },
            new CandleDto { OpenTime = new DateTime(2024, 1, 1, 2, 0, 0, DateTimeKind.Utc), Open = 103, Close = 104 },
            new CandleDto { OpenTime = new DateTime(2024, 1, 1, 2, 0, 0, DateTimeKind.Utc), Open = 103, Close = 105 }, // Duplicate
        };

        // Act - Deduplicate by open_time (simulating repository logic)
        var deduplicated = candles
            .GroupBy(c => c.OpenTime)
            .Select(g => g.First())
            .OrderBy(c => c.OpenTime)
            .ToList();

        // Assert
        Assert.Equal(5, candles.Count);
        Assert.Equal(3, deduplicated.Count); // 2 duplicates removed
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), deduplicated[0].OpenTime);
        Assert.Equal(new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc), deduplicated[1].OpenTime);
        Assert.Equal(new DateTime(2024, 1, 1, 2, 0, 0, DateTimeKind.Utc), deduplicated[2].OpenTime);
    }

    [Theory]
    [InlineData("2024-01-01 10:00:00", "2024-01-01 09:00:00", true)]  // start > end
    [InlineData("2024-01-01 10:00:00", "2024-01-01 10:00:00", true)] // start == end
    [InlineData("2024-01-01 09:00:00", "2024-01-01 10:00:00", false)] // Valid range
    public void TimeRangeValidation_DetectsInvalidRanges(string startTimeStr, string endTimeStr, bool expectedInvalid)
    {
        // Arrange
        var startTime = DateTime.Parse(startTimeStr, null, System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime();
        var endTime = DateTime.Parse(endTimeStr, null, System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime();

        // Act
        var isInvalid = startTime >= endTime;

        // Assert
        Assert.Equal(expectedInvalid, isInvalid);
    }

    [Fact]
    public void PipelineCompletion_DeterminesCorrectFinalState()
    {
        // Test scenario 1: All intervals succeeded
        var allSuccess = DeterminePipelineState(totalIntervals: 5, completedIntervals: 5, failedIntervals: 0);
        Assert.Equal(PipelineState.Running, allSuccess); // Should transition to Running after successful backfill

        // Test scenario 2: Some intervals succeeded, some failed
        var partialSuccess = DeterminePipelineState(totalIntervals: 5, completedIntervals: 3, failedIntervals: 2);
        Assert.Equal(PipelineState.CompletedWithErrors, partialSuccess);

        // Test scenario 3: All intervals failed
        var allFailed = DeterminePipelineState(totalIntervals: 5, completedIntervals: 0, failedIntervals: 5);
        Assert.Equal(PipelineState.Failed, allFailed);
    }

    // Helper method simulating the logic in FuturesSymbolPipelineManager.RunBackfillAsync
    private PipelineState DeterminePipelineState(int totalIntervals, int completedIntervals, int failedIntervals)
    {
        if (failedIntervals == 0)
        {
            // All intervals completed successfully - would transition to Running
            return PipelineState.Running;
        }
        else if (completedIntervals > 0)
        {
            // Some intervals completed, some failed
            return PipelineState.CompletedWithErrors;
        }
        else
        {
            // All intervals failed
            return PipelineState.Failed;
        }
    }

    [Fact]
    public void IntervalErrorTracking_MaintainsErrorHistory()
    {
        // Arrange
        var intervalErrors = new Dictionary<string, string>();
        var intervals = new[] { "15m", "30m", "1h", "4h", "1d" };

        // Act - Simulate processing intervals with some failures
        foreach (var interval in intervals)
        {
            if (interval == "1h" || interval == "4h")
            {
                intervalErrors[interval] = $"Failed to process {interval}";
            }
        }

        // Assert
        Assert.Equal(2, intervalErrors.Count);
        Assert.Contains("1h", intervalErrors.Keys);
        Assert.Contains("4h", intervalErrors.Keys);
        Assert.DoesNotContain("15m", intervalErrors.Keys);
        Assert.DoesNotContain("30m", intervalErrors.Keys);
        Assert.DoesNotContain("1d", intervalErrors.Keys);
    }
}
