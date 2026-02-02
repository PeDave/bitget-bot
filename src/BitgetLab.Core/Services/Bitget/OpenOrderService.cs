using Bitget.Net.Enums;
using Bitget.Net.Enums.V2;
using Bitget.Net.Interfaces.Clients;
using BitgetLab.Core.Models;
using System.Collections.Concurrent;

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
        using var client = _clientFactory.CreateRestClient();
        
        var orders = new ConcurrentBag<OpenOrderDto>();
        var tasks = new List<Task>();

        // Query USDT Futures in parallel
        if (includeUsdt)
        {
            var usdtTask = GetUsdtFuturesOrdersAsync(
                client, symbol, status, limit, orders, cancellationToken);
            tasks.Add(usdtTask);
        }
        
        // Query USDC Futures in parallel
        if (includeUsdc)
        {
            var usdcTask = GetUsdcFuturesOrdersAsync(
                client, symbol, status, limit, orders, cancellationToken);
            tasks.Add(usdcTask);
        }
        
        // Wait for all tasks to complete
        await Task.WhenAll(tasks);
        
        return orders;
    }

    private async Task GetUsdtFuturesOrdersAsync(
        IBitgetRestClient client,
        string? symbol,
        OrderStatus? status,
        int? limit,
        ConcurrentBag<OpenOrderDto> orders,
        CancellationToken cancellationToken)
    {
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

        foreach (var o in result.Data.Orders)
        {
            orders.Add(new OpenOrderDto
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
            });
        }
    }

    private async Task GetUsdcFuturesOrdersAsync(
        IBitgetRestClient client,
        string? symbol,
        OrderStatus? status,
        int? limit,
        ConcurrentBag<OpenOrderDto> orders,
        CancellationToken cancellationToken)
    {
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

        foreach (var o in result.Data.Orders)
        {
            orders.Add(new OpenOrderDto
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
            });
        }
    }
}
