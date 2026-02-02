using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for querying spot open orders using Bitget.Net SDK
/// </summary>
public interface ISpotOrderQueryService
{
    /// <summary>
    /// Gets spot open orders
    /// </summary>
    /// <param name="symbol">Optional symbol filter</param>
    /// <param name="idLessThan">Optional ID filter for pagination</param>
    /// <param name="limit">Maximum number of results (default 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of open orders</returns>
    Task<IEnumerable<OpenOrderDto>> GetOpenOrdersAsync(
        string? symbol = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public class SpotOrderQueryService : ISpotOrderQueryService
{
    private readonly IBitgetClientFactory _clientFactory;

    public SpotOrderQueryService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<OpenOrderDto>> GetOpenOrdersAsync(
        string? symbol = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var result = await client.SpotApiV2.Trading.GetOpenOrdersAsync(
            symbol: symbol,
            idLessThan: idLessThan,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get spot open orders: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Select(o => new OpenOrderDto
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
            CreateTime = o.CreateTime,
            UpdateTime = o.UpdateTime,
            Source = "spot",
            ProductType = null,
            MarginAsset = null
        }).ToList();
    }
}
