using Bitget.Net.Clients;
using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for accessing Bitget market data
/// </summary>
public interface IMarketDataService
{
    /// <summary>
    /// Get all available trading symbols
    /// </summary>
    Task<IEnumerable<string>> GetSymbolsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get ticker data for a specific symbol
    /// </summary>
    Task<TickerData> GetTickerAsync(string symbol, CancellationToken cancellationToken = default);
}

public class MarketDataService : IMarketDataService
{
    private readonly BitgetRestClient _client;
    
    public MarketDataService(IBitgetClientFactory factory)
    {
        _client = factory.CreateClientForCurrentMode();
    }

    public async Task<IEnumerable<string>> GetSymbolsAsync(CancellationToken cancellationToken = default)
    {
        // Get spot trading symbols from Bitget
        var result = await _client.SpotApiV2.ExchangeData.GetSymbolsAsync(ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to get symbols: {result.Error?.Message}");
        }
        
        return result.Data.Select(s => s.Symbol);
    }

    public async Task<TickerData> GetTickerAsync(string symbol, CancellationToken cancellationToken = default)
    {
        // Get ticker for a specific symbol
        var result = await _client.SpotApiV2.ExchangeData.GetTickersAsync(symbol, ct: cancellationToken);
        
        if (!result.Success || !result.Data.Any())
        {
            throw new InvalidOperationException($"Failed to get ticker for {symbol}: {result.Error?.Message}");
        }
        
        var ticker = result.Data.First();
        
        return new TickerData
        {
            Symbol = ticker.Symbol,
            LastPrice = ticker.LastPrice,
            Volume = ticker.Volume,
            Timestamp = ticker.Timestamp,
            High24h = ticker.HighPrice,
            Low24h = ticker.LowPrice,
            Change24h = ticker.ChangePercentage24H ?? 0
        };
    }
}
