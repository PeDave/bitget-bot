using BitgetLab.Core.Models;
using Bitget.Net.Interfaces.Clients;
using Bitget.Net.Enums;
using System.Collections.Concurrent;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for retrieving futures order information using Bitget.Net SDK
/// </summary>
public interface IFuturesOrderService
{
    /// <summary>
    /// Gets open futures orders for USDT and/or USDC futures
    /// </summary>
    /// <param name="productType">Product type (default: USDT-FUTURES)</param>
    /// <param name="symbol">Optional symbol filter (e.g., BTCUSDT)</param>
    /// <param name="includeUsdt">Include USDT futures orders (default: true)</param>
    /// <param name="includeUsdc">Include USDC futures orders (default: true)</param>
    /// <param name="marginAsset">Optional margin asset override</param>
    /// <param name="limit">Optional limit for number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of open futures orders</returns>
    Task<IEnumerable<FuturesOrderDto>> GetOpenOrdersAsync(
        string? productType = null,
        string? symbol = null,
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? marginAsset = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
}

public class FuturesOrderService : IFuturesOrderService
{
    private readonly IBitgetClientFactory _clientFactory;

    public FuturesOrderService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<FuturesOrderDto>> GetOpenOrdersAsync(
        string? productType = null,
        string? symbol = null,
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? marginAsset = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var orders = new ConcurrentBag<FuturesOrderDto>();
        var tasks = new List<Task>();

        // Query USDT Futures in parallel
        if (includeUsdt)
        {
            var usdtTask = GetUsdtOrdersAsync(client, symbol, limit, orders, cancellationToken);
            tasks.Add(usdtTask);
        }
        
        // Query USDC Futures in parallel
        if (includeUsdc)
        {
            var usdcTask = GetUsdcOrdersAsync(client, symbol, limit, orders, cancellationToken);
            tasks.Add(usdcTask);
        }
        
        // Wait for all tasks to complete
        await Task.WhenAll(tasks);
        
        return orders;
    }

    private async Task GetUsdtOrdersAsync(
        IBitgetRestClient client,
        string? symbol,
        int? limit,
        ConcurrentBag<FuturesOrderDto> orders,
        CancellationToken cancellationToken)
    {
        var result = await client.FuturesApiV2.Trading.GetOpenOrdersAsync(
            BitgetProductTypeV2.UsdtFutures,
            symbol: symbol,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get USDT futures open orders: {result.Error?.Message ?? "Unknown error"}");
        }

        foreach (var o in result.Data.Orders)
        {
            orders.Add(new FuturesOrderDto
            {
                OrderId = o.OrderId,
                ClientOrderId = o.ClientOrderId,
                Symbol = o.Symbol,
                Side = o.Side.ToString(),
                OrderType = o.OrderType.ToString(),
                Price = o.Price,
                Quantity = o.Quantity,
                QuantityFilled = o.QuantityFilled,
                Status = o.Status.ToString(),
                ProductType = "USDT-FUTURES",
                MarginAsset = "USDT",
                CreateTime = o.CreateTime,
                UpdateTime = o.UpdateTime
            });
        }
    }

    private async Task GetUsdcOrdersAsync(
        IBitgetRestClient client,
        string? symbol,
        int? limit,
        ConcurrentBag<FuturesOrderDto> orders,
        CancellationToken cancellationToken)
    {
        var result = await client.FuturesApiV2.Trading.GetOpenOrdersAsync(
            BitgetProductTypeV2.UsdcFutures,
            symbol: symbol,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get USDC futures open orders: {result.Error?.Message ?? "Unknown error"}");
        }

        foreach (var o in result.Data.Orders)
        {
            orders.Add(new FuturesOrderDto
            {
                OrderId = o.OrderId,
                ClientOrderId = o.ClientOrderId,
                Symbol = o.Symbol,
                Side = o.Side.ToString(),
                OrderType = o.OrderType.ToString(),
                Price = o.Price,
                Quantity = o.Quantity,
                QuantityFilled = o.QuantityFilled,
                Status = o.Status.ToString(),
                ProductType = "USDC-FUTURES",
                MarginAsset = "USDC",
                CreateTime = o.CreateTime,
                UpdateTime = o.UpdateTime
            });
        }
    }
}
