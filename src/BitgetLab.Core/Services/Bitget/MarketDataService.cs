namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// TODO: This service will use Bitget.Net SDK for market data
/// Placeholder for integration when vendor/Bitget.Net is added
/// </summary>
public interface IMarketDataService
{
    // TODO: Implement when Bitget.Net SDK is vendored
    // Task<IEnumerable<string>> GetSymbolsAsync(CancellationToken cancellationToken = default);
    // Task<TickerData> GetTickerAsync(string symbol, CancellationToken cancellationToken = default);
}

public class MarketDataService : IMarketDataService
{
    // TODO: Inject Bitget client when vendor SDK is available
    // private readonly IBitgetClient _client;
    
    // public MarketDataService(IBitgetClient client)
    // {
    //     _client = client;
    // }
}
