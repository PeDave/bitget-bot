using BitgetLab.Core.Models;
using Bitget.Net.Interfaces.Clients;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for retrieving spot order information using Bitget.Net SDK
/// </summary>
public interface ISpotOrderService
{
    /// <summary>
    /// Gets open spot orders
    /// </summary>
    /// <param name="symbol">Optional symbol filter (e.g., BTCUSDT)</param>
    /// <param name="limit">Optional limit for number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of open spot orders</returns>
    Task<IEnumerable<SpotOrderDto>> GetOpenOrdersAsync(
        string? symbol = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
}

public class SpotOrderService : ISpotOrderService
{
    private readonly IBitgetClientFactory _clientFactory;

    public SpotOrderService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<SpotOrderDto>> GetOpenOrdersAsync(
        string? symbol = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var result = await client.SpotApiV2.Trading.GetOpenOrdersAsync(
            symbol: symbol,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get spot open orders: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Select(o => new SpotOrderDto
        {
            OrderId = o.OrderId,
            ClientOrderId = o.ClientOrderId,
            Symbol = o.Symbol,
            Side = o.Side.ToString(),
            OrderType = o.OrderType.ToString(),
            Price = o.Price,
            Quantity = o.Quantity,
            QuantityFilled = o.QuantityFilled ?? 0,
            Status = o.Status.ToString(),
            CreateTime = o.CreateTime,
            UpdateTime = o.UpdateTime ?? DateTime.UtcNow
        }).ToList();
    }
}
