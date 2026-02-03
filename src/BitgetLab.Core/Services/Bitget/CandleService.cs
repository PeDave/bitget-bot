using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for retrieving historical candle data using Bitget.Net SDK
/// </summary>
public interface ICandleService
{
    /// <summary>
    /// Gets historical candle data for a symbol
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g., BTCUSDT)</param>
    /// <param name="interval">Candle interval (e.g., 1m, 5m, 15m, 1h, 4h, 1d)</param>
    /// <param name="startTime">Optional start time filter</param>
    /// <param name="endTime">Optional end time filter</param>
    /// <param name="limit">Maximum number of candles to return (default 100, max 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of candles</returns>
    Task<IEnumerable<CandleDto>> GetCandlesAsync(
        string symbol,
        string interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public class CandleService : ICandleService
{
    private readonly IBitgetClientFactory _clientFactory;

    public CandleService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<CandleDto>> GetCandlesAsync(
        string symbol,
        string interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required", nameof(symbol));
        }

        if (string.IsNullOrWhiteSpace(interval))
        {
            throw new ArgumentException("Interval is required", nameof(interval));
        }

        // Limit to max 1000 candles
        if (limit > 1000)
        {
            limit = 1000;
        }

        using var client = _clientFactory.CreateRestClient();
        
        // Get klines from Bitget Spot API
        var result = await client.SpotApiV2.ExchangeData.GetKlinesAsync(
            symbol: symbol,
            interval: ParseInterval(interval),
            startTime: startTime,
            endTime: endTime,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get candles for {symbol}: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Select(k => new CandleDto
        {
            OpenTime = k.OpenTime,
            Open = k.OpenPrice,
            High = k.HighPrice,
            Low = k.LowPrice,
            Close = k.ClosePrice,
            Volume = k.Volume,
            QuoteVolume = k.QuoteVolume
        }).ToList();
    }

    private global::Bitget.Net.Enums.V2.KlineInterval ParseInterval(string interval)
    {
        // Map string intervals to Bitget.Net enum
        // Note: Use lowercase for most intervals, but preserve case-sensitivity for month vs minute distinction
        return interval switch
        {
            "1m" => global::Bitget.Net.Enums.V2.KlineInterval.OneMinute,
            "5m" => global::Bitget.Net.Enums.V2.KlineInterval.FiveMinutes,
            "15m" => global::Bitget.Net.Enums.V2.KlineInterval.FifteenMinutes,
            "30m" => global::Bitget.Net.Enums.V2.KlineInterval.ThirtyMinutes,
            "1h" => global::Bitget.Net.Enums.V2.KlineInterval.OneHour,
            "4h" => global::Bitget.Net.Enums.V2.KlineInterval.FourHours,
            "6h" => global::Bitget.Net.Enums.V2.KlineInterval.SixHours,
            "12h" => global::Bitget.Net.Enums.V2.KlineInterval.TwelveHours,
            "1d" => global::Bitget.Net.Enums.V2.KlineInterval.OneDay,
            "3d" => global::Bitget.Net.Enums.V2.KlineInterval.ThreeDays,
            "1w" => global::Bitget.Net.Enums.V2.KlineInterval.OneWeek,
            "1mo" or "1month" => global::Bitget.Net.Enums.V2.KlineInterval.OneMonth,
            _ => throw new ArgumentException($"Invalid interval: {interval}. Valid values: 1m, 5m, 15m, 30m, 1h, 4h, 6h, 12h, 1d, 3d, 1w, 1mo (or 1month)")
        };
    }
}
