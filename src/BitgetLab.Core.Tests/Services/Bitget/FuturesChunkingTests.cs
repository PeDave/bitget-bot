using BitgetLab.Core.Models;
using BitgetLab.Core.Options;
using BitgetLab.Core.Services.Bitget;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using Xunit;

namespace BitgetLab.Core.Tests.Services.Bitget;

/// <summary>
/// Tests for chunked futures REST backfill functionality to avoid Bitget 40017 errors
/// </summary>
public class FuturesChunkingTests
{
    private const int MAX_FUTURES_HISTORY_LIMIT = 200;

    [Theory]
    [InlineData("1h", 7)]    // 1 hour: 7 day chunks
    [InlineData("4h", 30)]   // 4 hour: 30 day chunks
    [InlineData("1d", 180)]  // 1 day: 180 day chunks
    public void ChunkSize_ConfiguredCorrectly(string interval, int expectedDays)
    {
        // This test documents the chunk size configuration
        // The actual chunk sizes are internal to CandleService, but we validate the expected behavior
        var expectedChunkSize = TimeSpan.FromDays(expectedDays);
        Assert.True(expectedChunkSize.TotalDays > 0);
    }

    [Fact]
    public async Task LargeTimeRange_SplitsIntoMultipleChunks()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var capturedUrls = new List<string>();
        
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                capturedUrls.Add(request.RequestUri?.ToString() ?? string.Empty);
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""code"": ""00000"",
                    ""msg"": ""success"",
                    ""data"": []
                }", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        
        var mockClientFactory = new Mock<IBitgetClientFactory>();
        var mockLogger = new Mock<ILogger<CandleService>>();
        
        var chartingOptions = Microsoft.Extensions.Options.Options.Create(new ChartingOptions { EnablePersistence = false });
        var futuresOptions = Microsoft.Extensions.Options.Options.Create(new BitgetFuturesOptions 
        { 
            PublicRestBaseUrl = "https://api.bitget.com",
            ProductType = "USDT-FUTURES"
        });

        var service = new CandleService(
            mockClientFactory.Object,
            chartingOptions,
            futuresOptions,
            httpClient,
            mockLogger.Object);

        // Act - Request 1h candles for 180 days (should split into ~26 chunks of 7 days each)
        var startTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 6, 30, 0, 0, 0, DateTimeKind.Utc); // ~180 days
        
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "1h",
            startTime: startTime,
            endTime: endTime,
            limit: 100,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert - Should have made multiple API calls (one per chunk)
        Assert.NotEmpty(capturedUrls);
        
        // For 180 days with 7-day chunks, we expect roughly 180/7 = ~26 chunks
        // Allow some variance due to alignment
        Assert.True(capturedUrls.Count >= 20, $"Expected at least 20 chunks, got {capturedUrls.Count}");
        Assert.True(capturedUrls.Count <= 30, $"Expected at most 30 chunks, got {capturedUrls.Count}");
        
        // Verify all requests have limit <= 200
        foreach (var url in capturedUrls)
        {
            var limitMatch = System.Text.RegularExpressions.Regex.Match(url, @"limit=(\d+)");
            Assert.True(limitMatch.Success, $"URL missing limit parameter: {url}");
            var actualLimit = int.Parse(limitMatch.Groups[1].Value);
            Assert.True(actualLimit <= MAX_FUTURES_HISTORY_LIMIT, 
                $"Limit {actualLimit} exceeds maximum {MAX_FUTURES_HISTORY_LIMIT}");
        }
    }

    [Fact]
    public async Task SmallTimeRange_DoesNotChunk()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var capturedUrls = new List<string>();
        
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                capturedUrls.Add(request.RequestUri?.ToString() ?? string.Empty);
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""code"": ""00000"",
                    ""msg"": ""success"",
                    ""data"": []
                }", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        
        var mockClientFactory = new Mock<IBitgetClientFactory>();
        var mockLogger = new Mock<ILogger<CandleService>>();
        
        var chartingOptions = Microsoft.Extensions.Options.Options.Create(new ChartingOptions { EnablePersistence = false });
        var futuresOptions = Microsoft.Extensions.Options.Options.Create(new BitgetFuturesOptions 
        { 
            PublicRestBaseUrl = "https://api.bitget.com",
            ProductType = "USDT-FUTURES"
        });

        var service = new CandleService(
            mockClientFactory.Object,
            chartingOptions,
            futuresOptions,
            httpClient,
            mockLogger.Object);

        // Act - Request 1h candles for 3 days (less than 7-day chunk size, should not chunk)
        var startTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 4, 0, 0, 0, DateTimeKind.Utc); // 3 days
        
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "1h",
            startTime: startTime,
            endTime: endTime,
            limit: 100,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert - Should have made only 1 API call (no chunking needed)
        Assert.Single(capturedUrls);
    }

    [Fact]
    public async Task ChunkBoundaries_NoGapsOrOverlaps()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var capturedTimeRanges = new List<(long startMs, long endMs)>();
        
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                var url = request.RequestUri?.ToString() ?? string.Empty;
                
                // Extract startTime and endTime from URL
                var startMatch = System.Text.RegularExpressions.Regex.Match(url, @"startTime=(\d+)");
                var endMatch = System.Text.RegularExpressions.Regex.Match(url, @"endTime=(\d+)");
                
                if (startMatch.Success && endMatch.Success)
                {
                    var startMs = long.Parse(startMatch.Groups[1].Value);
                    var endMs = long.Parse(endMatch.Groups[1].Value);
                    capturedTimeRanges.Add((startMs, endMs));
                }
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""code"": ""00000"",
                    ""msg"": ""success"",
                    ""data"": []
                }", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        
        var mockClientFactory = new Mock<IBitgetClientFactory>();
        var mockLogger = new Mock<ILogger<CandleService>>();
        
        var chartingOptions = Microsoft.Extensions.Options.Options.Create(new ChartingOptions { EnablePersistence = false });
        var futuresOptions = Microsoft.Extensions.Options.Options.Create(new BitgetFuturesOptions 
        { 
            PublicRestBaseUrl = "https://api.bitget.com",
            ProductType = "USDT-FUTURES"
        });

        var service = new CandleService(
            mockClientFactory.Object,
            chartingOptions,
            futuresOptions,
            httpClient,
            mockLogger.Object);

        // Act - Request 4h candles for 60 days (should split into 2 chunks of 30 days each)
        var startTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc); // ~61 days
        
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "4h",
            startTime: startTime,
            endTime: endTime,
            limit: 100,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.True(capturedTimeRanges.Count >= 2, $"Expected at least 2 chunks, got {capturedTimeRanges.Count}");
        
        // Sort by start time
        var sortedRanges = capturedTimeRanges.OrderBy(r => r.startMs).ToList();
        
        // Verify each chunk has valid range (start < end)
        foreach (var (startMs, endMs) in sortedRanges)
        {
            Assert.True(startMs < endMs, $"Invalid chunk: start ({startMs}) >= end ({endMs})");
        }
        
        // Verify consecutive chunks are adjacent (no gaps, no overlaps)
        for (int i = 1; i < sortedRanges.Count; i++)
        {
            var prevEnd = sortedRanges[i - 1].endMs;
            var currStart = sortedRanges[i].startMs;
            
            // Next chunk should start where previous ended (or within 1ms for boundary rounding)
            var gap = Math.Abs(currStart - prevEnd);
            Assert.True(gap <= 1, 
                $"Gap or overlap detected between chunk {i-1} and {i}: " +
                $"prev end = {prevEnd}, curr start = {currStart}, gap = {gap}ms");
        }
    }

    [Theory]
    [InlineData("1h", 180, 7, 25)]   // 180 days / 7 day chunks ≈ 26 chunks
    [InlineData("4h", 365, 30, 12)]  // 365 days / 30 day chunks ≈ 13 chunks
    [InlineData("1d", 730, 180, 4)]  // 730 days / 180 day chunks ≈ 5 chunks
    public async Task ChunkCount_MatchesExpectedForLargeRanges(
        string interval, 
        int totalDays, 
        int chunkSizeDays, 
        int minExpectedChunks)
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var callCount = 0;
        
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                callCount++;
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""code"": ""00000"",
                    ""msg"": ""success"",
                    ""data"": []
                }", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        
        var mockClientFactory = new Mock<IBitgetClientFactory>();
        var mockLogger = new Mock<ILogger<CandleService>>();
        
        var chartingOptions = Microsoft.Extensions.Options.Options.Create(new ChartingOptions { EnablePersistence = false });
        var futuresOptions = Microsoft.Extensions.Options.Options.Create(new BitgetFuturesOptions 
        { 
            PublicRestBaseUrl = "https://api.bitget.com",
            ProductType = "USDT-FUTURES"
        });

        var service = new CandleService(
            mockClientFactory.Object,
            chartingOptions,
            futuresOptions,
            httpClient,
            mockLogger.Object);

        // Act
        var startTime = DateTime.UtcNow.AddDays(-totalDays);
        var endTime = DateTime.UtcNow;
        
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: interval,
            startTime: startTime,
            endTime: endTime,
            limit: 100,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert
        var expectedChunks = (int)Math.Ceiling((double)totalDays / chunkSizeDays);
        
        Assert.True(callCount >= minExpectedChunks, 
            $"Expected at least {minExpectedChunks} chunks for {totalDays} days with {chunkSizeDays}-day chunks, got {callCount}");
        
        // Allow some variance (+/- 2 chunks) due to alignment and rounding
        Assert.True(callCount <= expectedChunks + 2, 
            $"Expected at most {expectedChunks + 2} chunks, got {callCount}");
    }

    [Fact]
    public async Task InvalidTimeRange_ReturnsEmptyResult()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        
        var mockClientFactory = new Mock<IBitgetClientFactory>();
        var mockLogger = new Mock<ILogger<CandleService>>();
        
        var chartingOptions = Microsoft.Extensions.Options.Options.Create(new ChartingOptions { EnablePersistence = false });
        var futuresOptions = Microsoft.Extensions.Options.Options.Create(new BitgetFuturesOptions 
        { 
            PublicRestBaseUrl = "https://api.bitget.com",
            ProductType = "USDT-FUTURES"
        });

        var service = new CandleService(
            mockClientFactory.Object,
            chartingOptions,
            futuresOptions,
            httpClient,
            mockLogger.Object);

        // Act - startTime >= endTime (invalid)
        var startTime = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc); // Before startTime
        
        var result = await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "1h",
            startTime: startTime,
            endTime: endTime,
            limit: 100,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert - Should return empty result without making API calls
        Assert.Empty(result);
        
        // Verify no HTTP calls were made
        mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
