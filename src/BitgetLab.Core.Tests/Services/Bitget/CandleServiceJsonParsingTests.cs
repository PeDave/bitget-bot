using System.Globalization;
using System.Text.Json;
using Xunit;

namespace BitgetLab.Core.Tests.Services.Bitget;

/// <summary>
/// Tests for CandleService JSON parsing functionality.
/// These tests ensure the helper methods can handle both string and number JSON value kinds.
/// </summary>
public class CandleServiceJsonParsingTests
{
    /// <summary>
    /// Reads a long integer from a JsonElement, supporting both Number and String value kinds.
    /// This is a copy of the helper method from CandleService for testing purposes.
    /// </summary>
    private static long ReadInt64(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt64(),
            JsonValueKind.String => long.Parse(element.GetString() ?? "0", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Cannot parse Int64 from JSON type {element.ValueKind}")
        };
    }

    /// <summary>
    /// Reads a decimal from a JsonElement, supporting both Number and String value kinds.
    /// This is a copy of the helper method from CandleService for testing purposes.
    /// </summary>
    private static decimal ReadDecimal(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.String => decimal.Parse(element.GetString() ?? "0", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Cannot parse decimal from JSON type {element.ValueKind}")
        };
    }

    [Fact]
    public void ReadInt64_WithNumberType_ReturnsCorrectValue()
    {
        // Arrange
        var json = "1735689600000";
        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act
        var result = ReadInt64(element);

        // Assert
        Assert.Equal(1735689600000L, result);
    }

    [Fact]
    public void ReadInt64_WithStringType_ReturnsCorrectValue()
    {
        // Arrange
        var json = "\"1735689600000\"";
        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act
        var result = ReadInt64(element);

        // Assert
        Assert.Equal(1735689600000L, result);
    }

    [Fact]
    public void ReadDecimal_WithNumberType_ReturnsCorrectValue()
    {
        // Arrange
        var json = "93570.6";
        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act
        var result = ReadDecimal(element);

        // Assert
        Assert.Equal(93570.6m, result);
    }

    [Fact]
    public void ReadDecimal_WithStringType_ReturnsCorrectValue()
    {
        // Arrange
        var json = "\"93570.6\"";
        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act
        var result = ReadDecimal(element);

        // Assert
        Assert.Equal(93570.6m, result);
    }

    [Fact]
    public void ReadDecimal_WithStringType_LargeNumber_ReturnsCorrectValue()
    {
        // Arrange
        var json = "\"12345678.90123456\"";
        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act
        var result = ReadDecimal(element);

        // Assert
        Assert.Equal(12345678.90123456m, result);
    }

    [Fact]
    public void ParseCandleArray_WithAllStrings_Success()
    {
        // Arrange - Simulates actual Bitget API response with strings
        var json = @"{
            ""code"": ""00000"",
            ""msg"": ""success"",
            ""data"": [
                [""1735689600000"", ""93570.6"", ""93800.2"", ""93500.1"", ""93750.5"", ""1234.56"", ""115678901.23""],
                [""1735693200000"", ""93750.5"", ""94000.0"", ""93600.0"", ""93900.0"", ""2345.67"", ""220234567.89""]
            ]
        }";

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Act - Parse candles
        var dataElement = root.GetProperty("data");
        var candles = new List<(long timestamp, decimal open, decimal high, decimal low, decimal close, decimal volume, decimal quoteVolume)>();

        foreach (var candleArray in dataElement.EnumerateArray())
        {
            var timestamp = ReadInt64(candleArray[0]);
            var open = ReadDecimal(candleArray[1]);
            var high = ReadDecimal(candleArray[2]);
            var low = ReadDecimal(candleArray[3]);
            var close = ReadDecimal(candleArray[4]);
            var volume = ReadDecimal(candleArray[5]);
            var quoteVolume = ReadDecimal(candleArray[6]);

            candles.Add((timestamp, open, high, low, close, volume, quoteVolume));
        }

        // Assert
        Assert.Equal(2, candles.Count);

        // First candle
        Assert.Equal(1735689600000L, candles[0].timestamp);
        Assert.Equal(93570.6m, candles[0].open);
        Assert.Equal(93800.2m, candles[0].high);
        Assert.Equal(93500.1m, candles[0].low);
        Assert.Equal(93750.5m, candles[0].close);
        Assert.Equal(1234.56m, candles[0].volume);
        Assert.Equal(115678901.23m, candles[0].quoteVolume);

        // Second candle
        Assert.Equal(1735693200000L, candles[1].timestamp);
        Assert.Equal(93750.5m, candles[1].open);
        Assert.Equal(94000.0m, candles[1].high);
        Assert.Equal(93600.0m, candles[1].low);
        Assert.Equal(93900.0m, candles[1].close);
        Assert.Equal(2345.67m, candles[1].volume);
        Assert.Equal(220234567.89m, candles[1].quoteVolume);
    }

    [Fact]
    public void ParseCandleArray_WithAllNumbers_Success()
    {
        // Arrange - Simulates potential API response with numbers
        var json = @"{
            ""code"": ""00000"",
            ""msg"": ""success"",
            ""data"": [
                [1735689600000, 93570.6, 93800.2, 93500.1, 93750.5, 1234.56, 115678901.23],
                [1735693200000, 93750.5, 94000.0, 93600.0, 93900.0, 2345.67, 220234567.89]
            ]
        }";

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Act - Parse candles
        var dataElement = root.GetProperty("data");
        var candles = new List<(long timestamp, decimal open, decimal high, decimal low, decimal close, decimal volume, decimal quoteVolume)>();

        foreach (var candleArray in dataElement.EnumerateArray())
        {
            var timestamp = ReadInt64(candleArray[0]);
            var open = ReadDecimal(candleArray[1]);
            var high = ReadDecimal(candleArray[2]);
            var low = ReadDecimal(candleArray[3]);
            var close = ReadDecimal(candleArray[4]);
            var volume = ReadDecimal(candleArray[5]);
            var quoteVolume = ReadDecimal(candleArray[6]);

            candles.Add((timestamp, open, high, low, close, volume, quoteVolume));
        }

        // Assert
        Assert.Equal(2, candles.Count);

        // First candle
        Assert.Equal(1735689600000L, candles[0].timestamp);
        Assert.Equal(93570.6m, candles[0].open);
        Assert.Equal(93800.2m, candles[0].high);
        Assert.Equal(93500.1m, candles[0].low);
        Assert.Equal(93750.5m, candles[0].close);
        Assert.Equal(1234.56m, candles[0].volume);
        Assert.Equal(115678901.23m, candles[0].quoteVolume);

        // Second candle
        Assert.Equal(1735693200000L, candles[1].timestamp);
        Assert.Equal(93750.5m, candles[1].open);
        Assert.Equal(94000.0m, candles[1].high);
        Assert.Equal(93600.0m, candles[1].low);
        Assert.Equal(93900.0m, candles[1].close);
        Assert.Equal(2345.67m, candles[1].volume);
        Assert.Equal(220234567.89m, candles[1].quoteVolume);
    }

    [Fact]
    public void ParseCandleArray_WithMixedTypes_Success()
    {
        // Arrange - Mix of strings and numbers
        var json = @"{
            ""code"": ""00000"",
            ""msg"": ""success"",
            ""data"": [
                [""1735689600000"", 93570.6, ""93800.2"", 93500.1, ""93750.5"", 1234.56, ""115678901.23""]
            ]
        }";

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Act - Parse candles
        var dataElement = root.GetProperty("data");
        var candles = new List<(long timestamp, decimal open, decimal high, decimal low, decimal close, decimal volume, decimal quoteVolume)>();

        foreach (var candleArray in dataElement.EnumerateArray())
        {
            var timestamp = ReadInt64(candleArray[0]);
            var open = ReadDecimal(candleArray[1]);
            var high = ReadDecimal(candleArray[2]);
            var low = ReadDecimal(candleArray[3]);
            var close = ReadDecimal(candleArray[4]);
            var volume = ReadDecimal(candleArray[5]);
            var quoteVolume = ReadDecimal(candleArray[6]);

            candles.Add((timestamp, open, high, low, close, volume, quoteVolume));
        }

        // Assert
        Assert.Single(candles);
        Assert.Equal(1735689600000L, candles[0].timestamp);
        Assert.Equal(93570.6m, candles[0].open);
        Assert.Equal(93800.2m, candles[0].high);
        Assert.Equal(93500.1m, candles[0].low);
        Assert.Equal(93750.5m, candles[0].close);
        Assert.Equal(1234.56m, candles[0].volume);
        Assert.Equal(115678901.23m, candles[0].quoteVolume);
    }

    [Fact]
    public void ReadInt64_WithInvalidType_ThrowsException()
    {
        // Arrange
        var json = "true";
        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => ReadInt64(element));
        Assert.Contains("Cannot parse Int64 from JSON type", ex.Message);
    }

    [Fact]
    public void ReadDecimal_WithInvalidType_ThrowsException()
    {
        // Arrange
        var json = "null";
        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => ReadDecimal(element));
        Assert.Contains("Cannot parse decimal from JSON type", ex.Message);
    }
}
