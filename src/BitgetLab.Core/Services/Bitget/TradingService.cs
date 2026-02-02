namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// TODO: This service will use Bitget.Net SDK for futures trading
/// Placeholder for integration when vendor/Bitget.Net is added
/// </summary>
public interface ITradingService
{
    // TODO: Implement when Bitget.Net SDK is vendored
    // Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken cancellationToken = default);
}

public class TradingService : ITradingService
{
    // TODO: Inject Bitget client when vendor SDK is available
    // private readonly IBitgetClient _client;
    
    // public TradingService(IBitgetClient client)
    // {
    //     _client = client;
    // }
}
