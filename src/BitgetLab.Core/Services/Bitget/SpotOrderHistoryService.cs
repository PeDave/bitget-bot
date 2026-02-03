using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for querying spot order history and trades using Bitget.Net SDK
/// </summary>
public interface ISpotOrderHistoryService
{
    /// <summary>
    /// Gets spot closed orders
    /// </summary>
    /// <param name="symbol">Optional symbol filter</param>
    /// <param name="orderId">Optional order ID filter</param>
    /// <param name="startTime">Optional start time filter</param>
    /// <param name="endTime">Optional end time filter</param>
    /// <param name="idLessThan">Optional ID filter for pagination</param>
    /// <param name="limit">Maximum number of results (default 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of closed orders</returns>
    Task<IEnumerable<OrderDetailDto>> GetClosedOrdersAsync(
        string? symbol = null,
        string? orderId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detail for a specific spot order
    /// </summary>
    /// <param name="symbol">Trading symbol (required)</param>
    /// <param name="orderId">Order ID (exactly one of orderId or clientOrderId required)</param>
    /// <param name="clientOrderId">Client order ID (exactly one of orderId or clientOrderId required)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order detail</returns>
    Task<OrderDetailDto> GetOrderDetailAsync(
        string symbol,
        string? orderId = null,
        string? clientOrderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets spot user trades
    /// </summary>
    /// <param name="symbol">Optional symbol filter</param>
    /// <param name="orderId">Optional order ID filter</param>
    /// <param name="startTime">Optional start time filter</param>
    /// <param name="endTime">Optional end time filter</param>
    /// <param name="idLessThan">Optional ID filter for pagination</param>
    /// <param name="limit">Maximum number of results (default 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of user trades</returns>
    Task<IEnumerable<UserTradeDto>> GetUserTradesAsync(
        string? symbol = null,
        string? orderId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public class SpotOrderHistoryService : ISpotOrderHistoryService
{
    private readonly IBitgetClientFactory _clientFactory;

    public SpotOrderHistoryService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<OrderDetailDto>> GetClosedOrdersAsync(
        string? symbol = null,
        string? orderId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var result = await client.SpotApiV2.Trading.GetClosedOrdersAsync(
            symbol: symbol,
            orderId: orderId,
            startTime: startTime,
            endTime: endTime,
            idLessThan: idLessThan,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get spot closed orders: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Select(o => new OrderDetailDto
        {
            OrderId = o.OrderId,
            ClientOrderId = o.ClientOrderId,
            Symbol = o.Symbol,
            Side = o.Side.ToString(),
            Type = o.OrderType.ToString(),
            Status = o.Status.ToString(),
            Price = o.Price,
            Quantity = o.Quantity,
            QuantityFilled = o.QuantityFilled,
            AveragePrice = o.AveragePrice,
            CreateTime = o.CreateTime,
            UpdateTime = o.UpdateTime,
            Source = "spot",
            ProductType = null,
            MarginAsset = null
        }).ToList();
    }

    public async Task<OrderDetailDto> GetOrderDetailAsync(
        string symbol,
        string? orderId = null,
        string? clientOrderId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId) && string.IsNullOrWhiteSpace(clientOrderId))
        {
            throw new ArgumentException("Either orderId or clientOrderId must be provided");
        }

        using var client = _clientFactory.CreateRestClient();
        
        var result = await client.SpotApiV2.Trading.GetOrderAsync(
            orderId: orderId,
            clientOrderId: clientOrderId,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get spot order detail: {result.Error?.Message ?? "Unknown error"}");
        }

        // GetOrderAsync returns an array, take the first one
        var o = result.Data.FirstOrDefault();
        if (o == null)
        {
            throw new BitgetApiException("Order not found");
        }

        return new OrderDetailDto
        {
            OrderId = o.OrderId,
            ClientOrderId = o.ClientOrderId,
            Symbol = o.Symbol,
            Side = o.Side.ToString(),
            Type = o.OrderType.ToString(),
            Status = o.Status.ToString(),
            Price = o.Price,
            Quantity = o.Quantity,
            QuantityFilled = o.QuantityFilled,
            AveragePrice = o.AveragePrice,
            CreateTime = o.CreateTime,
            UpdateTime = o.UpdateTime,
            Source = "spot",
            ProductType = null,
            MarginAsset = null
        };
    }

    public async Task<IEnumerable<UserTradeDto>> GetUserTradesAsync(
        string? symbol = null,
        string? orderId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var result = await client.SpotApiV2.Trading.GetUserTradesAsync(
            symbol: symbol,
            orderId: orderId,
            startTime: startTime,
            endTime: endTime,
            idLessThan: idLessThan,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get spot user trades: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Select(t => new UserTradeDto
        {
            TradeId = t.TradeId,
            OrderId = t.OrderId,
            ClientOrderId = null, // Spot trades don't have client order ID
            Symbol = t.Symbol,
            Side = t.Side.ToString(),
            Price = t.Price,
            Quantity = t.Quantity,
            TradeTime = t.CreateTime,
            FeeAsset = t.Fees?.FeeAsset ?? string.Empty,
            Fee = t.Fees?.TotalFee ?? 0m,
            FeeDeduction = null,
            FeeTotalDeduction = null,
            Source = "spot",
            ProductType = null,
            MarginAsset = null
        }).ToList();
    }
}
