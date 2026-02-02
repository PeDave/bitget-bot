namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for executing trades using Bitget.Net SDK
/// </summary>
public interface ITradingService
{
    /// <summary>
    /// Places an order on Bitget. Only allowed in Trade mode.
    /// </summary>
    Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken cancellationToken = default);
}

public class TradingService : ITradingService
{
    private readonly IBitgetClientFactory _clientFactory;

    public TradingService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken cancellationToken = default)
    {
        // Validate trade mode
        if (!_clientFactory.IsTradeAllowed())
        {
            throw new InvalidOperationException("Trade operations are not allowed in ReadOnly mode");
        }

        using var client = _clientFactory.CreateRestClient();
        
        // Place spot order
        var result = await client.SpotApiV2.Trading.PlaceOrderAsync(
            symbol: request.Symbol,
            side: request.Side == OrderSide.Buy ? global::Bitget.Net.Enums.V2.OrderSide.Buy : global::Bitget.Net.Enums.V2.OrderSide.Sell,
            type: request.Type == OrderType.Limit ? global::Bitget.Net.Enums.V2.OrderType.Limit : global::Bitget.Net.Enums.V2.OrderType.Market,
            quantity: request.Quantity,
            timeInForce: global::Bitget.Net.Enums.V2.TimeInForce.GoodTillCanceled,
            price: request.Price,
            ct: cancellationToken
        );

        if (!result.Success)
        {
            throw new Exception($"Failed to place order: {result.Error?.Message ?? "Unknown error"}");
        }

        return new OrderResult
        {
            OrderId = result.Data.OrderId,
            Symbol = request.Symbol,
            Status = "Placed",
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Order request model
/// </summary>
public class OrderRequest
{
    public string Symbol { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public OrderType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
}

/// <summary>
/// Order result model
/// </summary>
public class OrderResult
{
    public string OrderId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Order side enum
/// </summary>
public enum OrderSide
{
    Buy,
    Sell
}

/// <summary>
/// Order type enum
/// </summary>
public enum OrderType
{
    Market,
    Limit
}
