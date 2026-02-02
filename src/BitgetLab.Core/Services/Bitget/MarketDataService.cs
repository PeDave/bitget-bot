namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for retrieving market data using Bitget.Net SDK
/// </summary>
public interface IMarketDataService
{
    /// <summary>
    /// Gets all available trading symbols
    /// </summary>
    Task<IEnumerable<string>> GetSymbolsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets ticker data for a specific symbol
    /// </summary>
    Task<TickerData> GetTickerAsync(string symbol, CancellationToken cancellationToken = default);
}

public class MarketDataService : IMarketDataService
{
    private readonly IBitgetClientFactory _clientFactory;

    public MarketDataService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<string>> GetSymbolsAsync(CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        // Get spot symbols from Bitget
        var result = await client.SpotApiV2.ExchangeData.GetSymbolsAsync(ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new Exception($"Failed to get symbols: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Select(s => s.Symbol).ToList();
    }

    public async Task<TickerData> GetTickerAsync(string symbol, CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        // Get tickers for specific symbol
        var result = await client.SpotApiV2.ExchangeData.GetTickersAsync(symbol: symbol, ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new Exception($"Failed to get ticker for {symbol}: {result.Error?.Message ?? "Unknown error"}");
        }

        var ticker = result.Data.FirstOrDefault();
        if (ticker == null)
        {
            throw new Exception($"No ticker data found for {symbol}");
        }

        return new TickerData
        {
            Symbol = ticker.Symbol,
            LastPrice = ticker.LastPrice,
            High24h = ticker.HighPrice,
            Low24h = ticker.LowPrice,
            Volume24h = ticker.Volume,
            Timestamp = ticker.Timestamp
        };
    }
}

/// <summary>
/// Ticker data model
/// </summary>
public class TickerData
{
    public string Symbol { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public decimal High24h { get; set; }
    public decimal Low24h { get; set; }
    public decimal Volume24h { get; set; }
    public DateTime Timestamp { get; set; }
}
