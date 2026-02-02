using Bitget.Net.Enums;
using Bitget.Net.Enums.V2;
using Bitget.Net.Interfaces.Clients;
using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for retrieving open order information using Bitget.Net SDK
/// </summary>
public interface IOpenOrderService
{
    /// <summary>
    /// Gets spot open orders
    /// </summary>
    /// <param name="symbol">Optional symbol filter</param>
    /// <param name="limit">Optional limit on number of results</param>
    /// <param name="idLessThan">Optional pagination cursor</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of open spot orders</returns>
    Task<IEnumerable<OpenOrderDto>> GetSpotOpenOrdersAsync(
        string? symbol = null,
        int? limit = null,
        string? idLessThan = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets futures open orders for USDT and/or USDC futures
    /// </summary>
    /// <param name="symbol">Optional symbol filter</param>
    /// <param name="includeUsdt">Include USDT futures orders</param>
    /// <param name="includeUsdc">Include USDC futures orders</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="limit">Optional limit on number of results per product type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of open futures orders</returns>
    Task<IEnumerable<OpenOrderDto>> GetFuturesOpenOrdersAsync(
        string? symbol = null,
        bool includeUsdt = true,
        bool includeUsdc = true,
        OrderStatus? status = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
}

public class OpenOrderService : IOpenOrderService
{
    private readonly IBitgetClientFactory _clientFactory;

    public OpenOrderService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<OpenOrderDto>> GetSpotOpenOrdersAsync(
        string? symbol = null,
        int? limit = null,
        string? idLessThan = null,
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
            QuantityFilled = o.QuantityFilled ?? 0m,
            CreateTime = o.CreateTime,
            UpdateTime = o.UpdateTime ?? o.CreateTime,
            Source = "spot",
            ProductType = null,
            MarginAsset = null
        }).ToList();
    }

    public async Task<IEnumerable<OpenOrderDto>> GetFuturesOpenOrdersAsync(
        string? symbol = null,
        bool includeUsdt = true,
        bool includeUsdc = true,
        OrderStatus? status = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<IEnumerable<OpenOrderDto>>>();

        // Query USDT Futures in parallel
        if (includeUsdt)
        {
            tasks.Add(GetUsdtFuturesOrdersAsync(symbol, status, limit, cancellationToken));
        }
        
        // Query USDC Futures in parallel
        if (includeUsdc)
        {
            tasks.Add(GetUsdcFuturesOrdersAsync(symbol, status, limit, cancellationToken));
        }
        
        // Wait for all tasks to complete and flatten results
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }

    private async Task<IEnumerable<OpenOrderDto>> GetUsdtFuturesOrdersAsync(
        string? symbol,
        OrderStatus? status,
        int? limit,
        CancellationToken cancellationToken)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var result = await client.FuturesApiV2.Trading.GetOpenOrdersAsync(
            BitgetProductTypeV2.UsdtFutures,
            symbol: symbol,
            status: status,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get USDT futures open orders: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Orders.Select(o => new OpenOrderDto
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
            UpdateTime = o.UpdateTime ?? o.CreateTime,
            Source = "futures",
            ProductType = "USDT-FUTURES",
            MarginAsset = "USDT"
        }).ToList();
    }

    private async Task<IEnumerable<OpenOrderDto>> GetUsdcFuturesOrdersAsync(
        string? symbol,
        OrderStatus? status,
        int? limit,
        CancellationToken cancellationToken)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var result = await client.FuturesApiV2.Trading.GetOpenOrdersAsync(
            BitgetProductTypeV2.UsdcFutures,
            symbol: symbol,
            status: status,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get USDC futures open orders: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Orders.Select(o => new OpenOrderDto
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
            UpdateTime = o.UpdateTime ?? o.CreateTime,
            Source = "futures",
            ProductType = "USDC-FUTURES",
            MarginAsset = "USDC"
        }).ToList();
    }
}
