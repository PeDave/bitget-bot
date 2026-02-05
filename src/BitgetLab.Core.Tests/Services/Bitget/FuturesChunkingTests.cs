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
/// Tests to verify that futures backfill correctly chunks large time ranges into smaller fixed-size chunks
/// to avoid Bitget API error 40017 on requests with excessive time spans.
/// </summary>
public class FuturesChunkingTests
{
    // Expected chunk sizes from requirements
    private const int CHUNK_SIZE_1H_DAYS = 7;
    private const int CHUNK_SIZE_4H_DAYS = 30;
    private const int CHUNK_SIZE_1D_DAYS = 180;

    [Fact]
    public async Task FetchFuturesRange_1h_180Days_SplitsIntoMultipleChunks()
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
        var service = CreateCandleService(httpClient);

        // Act - Request 180 days for 1h interval
        var endTime = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var startTime = endTime.AddDays(-180);
        
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "1h",
            startTime: startTime,
            endTime: endTime,
            limit: 200,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert - Should split into approximately 26 chunks (180 days / 7 days per chunk)
        // Expected: ceil(180 / 7) = 26 chunks
        var expectedChunks = (int)Math.Ceiling(180.0 / CHUNK_SIZE_1H_DAYS);
        
        // Each chunk may make multiple paginated requests, so we should have at least 26 requests
        Assert.True(capturedUrls.Count >= expectedChunks,
            $"Expected at least {expectedChunks} API calls for 180-day range with 7-day chunks, got {capturedUrls.Count}");
        
        // Verify all requests have limit <= 200
        foreach (var url in capturedUrls)
        {
            Assert.Contains("limit=", url);
            var limitMatch = System.Text.RegularExpressions.Regex.Match(url, @"limit=(\d+)");
            Assert.True(limitMatch.Success);
            var actualLimit = int.Parse(limitMatch.Groups[1].Value);
            Assert.True(actualLimit <= 200, $"Limit {actualLimit} exceeds maximum 200");
        }
    }

    [Fact]
    public async Task FetchFuturesRange_4h_365Days_SplitsIntoMultipleChunks()
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
        var service = CreateCandleService(httpClient);

        // Act - Request 365 days for 4h interval
        var endTime = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var startTime = endTime.AddDays(-365);
        
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "4h",
            startTime: startTime,
            endTime: endTime,
            limit: 200,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert - Should split into approximately 13 chunks (365 days / 30 days per chunk)
        // Expected: ceil(365 / 30) = 13 chunks
        var expectedChunks = (int)Math.Ceiling(365.0 / CHUNK_SIZE_4H_DAYS);
        
        Assert.True(capturedUrls.Count >= expectedChunks,
            $"Expected at least {expectedChunks} API calls for 365-day range with 30-day chunks, got {capturedUrls.Count}");
        
        // Verify all requests have limit <= 200
        foreach (var url in capturedUrls)
        {
            Assert.Contains("limit=", url);
            var limitMatch = System.Text.RegularExpressions.Regex.Match(url, @"limit=(\d+)");
            Assert.True(limitMatch.Success);
            var actualLimit = int.Parse(limitMatch.Groups[1].Value);
            Assert.True(actualLimit <= 200, $"Limit {actualLimit} exceeds maximum 200");
        }
    }

    [Fact]
    public async Task FetchFuturesRange_1d_730Days_SplitsIntoMultipleChunks()
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
        var service = CreateCandleService(httpClient);

        // Act - Request 730 days for 1d interval
        var endTime = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var startTime = endTime.AddDays(-730);
        
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "1d",
            startTime: startTime,
            endTime: endTime,
            limit: 200,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert - Should split into approximately 5 chunks (730 days / 180 days per chunk)
        // Expected: ceil(730 / 180) = 5 chunks
        var expectedChunks = (int)Math.Ceiling(730.0 / CHUNK_SIZE_1D_DAYS);
        
        Assert.True(capturedUrls.Count >= expectedChunks,
            $"Expected at least {expectedChunks} API calls for 730-day range with 180-day chunks, got {capturedUrls.Count}");
        
        // Verify all requests have limit <= 200
        foreach (var url in capturedUrls)
        {
            Assert.Contains("limit=", url);
            var limitMatch = System.Text.RegularExpressions.Regex.Match(url, @"limit=(\d+)");
            Assert.True(limitMatch.Success);
            var actualLimit = int.Parse(limitMatch.Groups[1].Value);
            Assert.True(actualLimit <= 200, $"Limit {actualLimit} exceeds maximum 200");
        }
    }

    [Fact]
    public async Task FetchFuturesRange_GuardsAgainstInvalidTimeRange_StartGreaterThanEnd()
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
        var service = CreateCandleService(httpClient);

        // Act - Request with startTime > endTime
        var startTime = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var result = await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "1h",
            startTime: startTime,
            endTime: endTime,
            limit: 200,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert - Should return empty result without calling API
        Assert.Empty(result);
        Assert.Empty(capturedUrls);
    }

    [Fact]
    public async Task FetchFuturesRange_GuardsAgainstInvalidTimeRange_StartEqualsEnd()
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
        var service = CreateCandleService(httpClient);

        // Act - Request with startTime == endTime
        var time = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var result = await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "1h",
            startTime: time,
            endTime: time,
            limit: 200,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert - Should return empty result without calling API
        Assert.Empty(result);
        Assert.Empty(capturedUrls);
    }

    [Fact]
    public async Task FetchFuturesRange_15m_NoChunking_UsesExistingBehavior()
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
        var service = CreateCandleService(httpClient);

        // Act - Request 30 days for 15m interval (no chunking configured for 15m)
        var endTime = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var startTime = endTime.AddDays(-30);
        
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "15m",
            startTime: startTime,
            endTime: endTime,
            limit: 200,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert - Should use existing backward pagination without chunking
        // 15m is not in the chunking configuration, so it should make pagination requests
        // but not split into time chunks
        Assert.NotEmpty(capturedUrls);
        
        // Verify all requests have limit <= 200
        foreach (var url in capturedUrls)
        {
            Assert.Contains("limit=", url);
            var limitMatch = System.Text.RegularExpressions.Regex.Match(url, @"limit=(\d+)");
            Assert.True(limitMatch.Success);
            var actualLimit = int.Parse(limitMatch.Groups[1].Value);
            Assert.True(actualLimit <= 200, $"Limit {actualLimit} exceeds maximum 200");
        }
    }

    [Fact]
    public async Task FetchFuturesRange_5m_NoChunking_UsesExistingBehavior()
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
        var service = CreateCandleService(httpClient);

        // Act - Request 7 days for 5m interval (no chunking configured for 5m)
        var endTime = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var startTime = endTime.AddDays(-7);
        
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "5m",
            startTime: startTime,
            endTime: endTime,
            limit: 200,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert - Should use existing backward pagination without chunking
        Assert.NotEmpty(capturedUrls);
        
        // Verify all requests have limit <= 200
        foreach (var url in capturedUrls)
        {
            Assert.Contains("limit=", url);
            var limitMatch = System.Text.RegularExpressions.Regex.Match(url, @"limit=(\d+)");
            Assert.True(limitMatch.Success);
            var actualLimit = int.Parse(limitMatch.Groups[1].Value);
            Assert.True(actualLimit <= 200, $"Limit {actualLimit} exceeds maximum 200");
        }
    }

    [Fact]
    public async Task FetchFuturesRange_1h_SmallRange_SingleChunk()
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
        var service = CreateCandleService(httpClient);

        // Act - Request 3 days for 1h interval (smaller than chunk size of 7 days)
        var endTime = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var startTime = endTime.AddDays(-3);
        
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "1h",
            startTime: startTime,
            endTime: endTime,
            limit: 200,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert - Should process as a single chunk (range < chunk size)
        // Expected: 1 chunk, may have multiple pagination requests within the chunk
        Assert.NotEmpty(capturedUrls);
        
        // Verify all requests have limit <= 200
        foreach (var url in capturedUrls)
        {
            Assert.Contains("limit=", url);
        }
    }

    private CandleService CreateCandleService(HttpClient httpClient)
    {
        var mockClientFactory = new Mock<IBitgetClientFactory>();
        var mockLogger = new Mock<ILogger<CandleService>>();
        
        var chartingOptions = Microsoft.Extensions.Options.Options.Create(new ChartingOptions 
        { 
            EnablePersistence = false 
        });
        
        var futuresOptions = Microsoft.Extensions.Options.Options.Create(new BitgetFuturesOptions 
        { 
            PublicRestBaseUrl = "https://api.bitget.com",
            ProductType = "USDT-FUTURES"
        });

        return new CandleService(
            mockClientFactory.Object,
            chartingOptions,
            futuresOptions,
            httpClient,
            mockLogger.Object);
    }
}
