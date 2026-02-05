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
/// Tests to verify that Bitget futures history-candles API calls respect the limit constraint of 200.
/// This addresses the issue where limit values > 200 result in API errors 40053 or 40017.
/// </summary>
public class FuturesLimitConstraintTests
{
    private const int MAX_FUTURES_HISTORY_LIMIT = 200;

    [Theory]
    [InlineData(100, 100)]   // Below limit - should pass through
    [InlineData(200, 200)]   // At limit - should pass through
    [InlineData(300, 200)]   // Above limit - should be clamped to 200
    [InlineData(1000, 200)]  // Far above limit - should be clamped to 200
    [InlineData(500, 200)]   // Above limit - should be clamped to 200
    public async Task FetchFromPublicHistoryApiAsync_ClampsLimitTo200(int requestedLimit, int expectedLimit)
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var capturedUrl = string.Empty;
        
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                capturedUrl = request.RequestUri?.ToString() ?? string.Empty;
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
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "1h",
            startTime: null,
            endTime: null,
            limit: requestedLimit,
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.NotEmpty(capturedUrl);
        Assert.Contains($"limit={expectedLimit}", capturedUrl);
        
        // Verify the limit in the URL does not exceed 200
        var limitMatch = System.Text.RegularExpressions.Regex.Match(capturedUrl, @"limit=(\d+)");
        Assert.True(limitMatch.Success);
        var actualLimit = int.Parse(limitMatch.Groups[1].Value);
        Assert.True(actualLimit <= MAX_FUTURES_HISTORY_LIMIT, 
            $"Limit {actualLimit} exceeds maximum allowed {MAX_FUTURES_HISTORY_LIMIT}");
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(200, 200)]
    [InlineData(500, 200)]
    [InlineData(1000, 200)]
    public void ClampLimitTo200_ReturnsCorrectValue(int input, int expected)
    {
        // Act
        var result = Math.Min(input, MAX_FUTURES_HISTORY_LIMIT);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task FetchFuturesRangeWithBackwardPagination_ClampsPerRequestLimitTo200()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var capturedUrls = new List<string>();
        
        // Mock to return empty data after first call to avoid infinite pagination
        var callCount = 0;
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                capturedUrls.Add(request.RequestUri?.ToString() ?? string.Empty);
                callCount++;
            })
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(callCount == 1 ? @"{
                    ""code"": ""00000"",
                    ""msg"": ""success"",
                    ""data"": [
                        [""1735689600000"", ""93570.6"", ""93800.2"", ""93500.1"", ""93750.5"", ""1234.56"", ""115678901.23""]
                    ]
                }" : @"{
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

        // Act - Request with limit=1000, but should be clamped to 200 internally
        var startTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        
        await service.GetCandlesAsync(
            symbol: "BTCUSDT",
            interval: "1h",
            startTime: startTime,
            endTime: endTime,
            limit: 1000,  // Request large limit
            market: MarketType.Futures,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.NotEmpty(capturedUrls);
        
        // Verify all requests have limit <= 200
        foreach (var url in capturedUrls)
        {
            Assert.Contains("limit=", url);
            var limitMatch = System.Text.RegularExpressions.Regex.Match(url, @"limit=(\d+)");
            Assert.True(limitMatch.Success);
            var actualLimit = int.Parse(limitMatch.Groups[1].Value);
            Assert.True(actualLimit <= MAX_FUTURES_HISTORY_LIMIT, 
                $"Limit {actualLimit} in URL exceeds maximum allowed {MAX_FUTURES_HISTORY_LIMIT}. URL: {url}");
        }
    }

    [Fact]
    public void MaxFuturesHistoryLimit_IsSetTo200()
    {
        // This test documents the constraint
        Assert.Equal(200, MAX_FUTURES_HISTORY_LIMIT);
    }
}
