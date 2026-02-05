using BitgetLab.Core.Models;
using Xunit;

namespace BitgetLab.Core.Tests.Services.Bitget;

/// <summary>
/// Tests for the range-based backfill endpoint functionality
/// </summary>
public class RangeBackfillEndpointTests
{
    [Fact]
    public void RangeBackfillRequest_ValidData_CreatesSuccessfully()
    {
        // Verify the request model can be instantiated with valid data
        var backfillReq = new RangeBackfillRequest
        {
            Symbol = "BTCUSDT",
            Market = "spot",
            Interval = "1h",
            Start = "2024-01-01T00:00:00Z",
            End = "2024-01-02T00:00:00Z",
            Limit = 500,
            MaxConcurrency = 3
        };

        Assert.Equal("BTCUSDT", backfillReq.Symbol);
        Assert.Equal("spot", backfillReq.Market);
        Assert.Equal("1h", backfillReq.Interval);
        Assert.NotNull(backfillReq.Start);
        Assert.NotNull(backfillReq.End);
    }

    [Fact]
    public void RangeBackfillResponse_ValidData_CreatesSuccessfully()
    {
        // Verify the response model can be instantiated with valid metrics
        var backfillResp = new RangeBackfillResponse
        {
            Ok = true,
            Symbol = "ETHUSDT",
            Market = "futures",
            Interval = "15m",
            Start = "2024-01-01T00:00:00Z",
            End = "2024-01-03T00:00:00Z",
            FetchedBatches = 5,
            FetchedCandles = 480,
            Inserted = 300,
            Updated = 180,
            Skipped = 0,
            DurationMs = 1500
        };

        Assert.True(backfillResp.Ok);
        Assert.Equal(5, backfillResp.FetchedBatches);
        Assert.Equal(480, backfillResp.FetchedCandles);
        Assert.Equal(300, backfillResp.Inserted);
        Assert.Equal(180, backfillResp.Updated);
        Assert.InRange(backfillResp.DurationMs, 0, long.MaxValue);
    }

    [Fact]
    public void RangeBackfillRequest_DefaultMarket_IsSpot()
    {
        // Verify default market is spot when not specified
        var backfillReq = new RangeBackfillRequest
        {
            Symbol = "BTCUSDT",
            Interval = "1d",
            Start = "2024-01-01T00:00:00Z",
            End = "2024-01-31T00:00:00Z"
        };

        Assert.Equal("spot", backfillReq.Market);
    }

    [Theory]
    [InlineData("1m")]
    [InlineData("5m")]
    [InlineData("15m")]
    [InlineData("1h")]
    [InlineData("4h")]
    [InlineData("1d")]
    public void RangeBackfillRequest_SupportsCommonIntervals(string intervalValue)
    {
        // Verify common interval formats are supported
        var backfillReq = new RangeBackfillRequest
        {
            Symbol = "BTCUSDT",
            Interval = intervalValue,
            Start = "2024-01-01T00:00:00Z",
            End = "2024-01-02T00:00:00Z"
        };

        Assert.Equal(intervalValue, backfillReq.Interval);
    }
}
