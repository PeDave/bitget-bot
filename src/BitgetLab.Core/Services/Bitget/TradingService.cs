using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
            throw new BitgetApiException($"Failed to place order: {result.Error?.Message ?? "Unknown error"}");
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
    /// <summary>
    /// Trading pair symbol (e.g., BTCUSDT, ETHUSDT)
    /// </summary>
    [Required]
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// Order side. Supported values: Buy, Sell (case-insensitive)
    /// </summary>
    [Required]
    public OrderSide Side { get; set; }
    
    /// <summary>
    /// Order type. Supported values: Market, Limit (case-insensitive)
    /// </summary>
    [Required]
    public OrderType Type { get; set; }
    
    /// <summary>
    /// Order quantity/amount
    /// </summary>
    [Required]
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// Order price (required for Limit orders, optional for Market orders)
    /// </summary>
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
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderSide
{
    /// <summary>Buy order</summary>
    Buy,
    /// <summary>Sell order</summary>
    Sell
}

/// <summary>
/// Order type enum
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderType
{
    /// <summary>Market order (executed at current market price)</summary>
    Market,
    /// <summary>Limit order (executed at specified price or better)</summary>
    Limit
}
