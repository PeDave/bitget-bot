using Bitget.Net.Enums;
using Bitget.Net.Enums.V2;
using Bitget.Net.Interfaces.Clients;
using BitgetLab.Core.Models;
using System.Collections.Concurrent;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for querying futures open orders using Bitget.Net SDK
/// </summary>
public interface IFuturesOrderQueryService
{
    /// <summary>
    /// Gets futures open orders for USDT and/or USDC futures
    /// </summary>
    /// <param name="includeUsdt">Include USDT futures orders</param>
    /// <param name="includeUsdc">Include USDC futures orders</param>
    /// <param name="symbol">Optional symbol filter</param>
    /// <param name="status">Optional status filter (e.g., "live", "partially_filled")</param>
    /// <param name="idLessThan">Optional ID filter for pagination</param>
    /// <param name="limit">Maximum number of results per product type (default 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of open orders</returns>
    Task<IEnumerable<OpenOrderDto>> GetOpenOrdersAsync(
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? symbol = null,
        string? status = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public class FuturesOrderQueryService : IFuturesOrderQueryService
{
    private readonly IBitgetClientFactory _clientFactory;

    public FuturesOrderQueryService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<OpenOrderDto>> GetOpenOrdersAsync(
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? symbol = null,
        string? status = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var orders = new ConcurrentBag<OpenOrderDto>();
        var tasks = new List<Task>();

        // Parse status string to OrderStatus enum if provided
        OrderStatus? orderStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
            {
                orderStatus = parsedStatus;
            }
        }

        // Query USDT Futures in parallel
        if (includeUsdt)
        {
            var usdtTask = GetUsdtOrdersAsync(client, symbol, orderStatus, idLessThan, limit, orders, cancellationToken);
            tasks.Add(usdtTask);
        }
        
        // Query USDC Futures in parallel
        if (includeUsdc)
        {
            var usdcTask = GetUsdcOrdersAsync(client, symbol, orderStatus, idLessThan, limit, orders, cancellationToken);
            tasks.Add(usdcTask);
        }
        
        // Wait for all tasks to complete
        await Task.WhenAll(tasks);
        
        return orders;
    }

    private async Task GetUsdtOrdersAsync(
        IBitgetRestClient client,
        string? symbol,
        OrderStatus? status,
        string? idLessThan,
        int limit,
        ConcurrentBag<OpenOrderDto> orders,
        CancellationToken cancellationToken)
    {
        var result = await client.FuturesApiV2.Trading.GetOpenOrdersAsync(
            productType: BitgetProductTypeV2.UsdtFutures,
            symbol: symbol,
            orderId: null,
            clientOrderId: null,
            status: status,
            idLessThan: idLessThan,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get USDT futures open orders: {result.Error?.Message ?? "Unknown error"}");
        }

        if (result.Data.Orders != null)
        {
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
                    UpdateTime = o.UpdateTime,
                    Source = "futures",
                    ProductType = "USDT-FUTURES",
                    MarginAsset = "USDT"
                });
            }
        }
    }

    private async Task GetUsdcOrdersAsync(
        IBitgetRestClient client,
        string? symbol,
        OrderStatus? status,
        string? idLessThan,
        int limit,
        ConcurrentBag<OpenOrderDto> orders,
        CancellationToken cancellationToken)
    {
        var result = await client.FuturesApiV2.Trading.GetOpenOrdersAsync(
            productType: BitgetProductTypeV2.UsdcFutures,
            symbol: symbol,
            orderId: null,
            clientOrderId: null,
            status: status,
            idLessThan: idLessThan,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get USDC futures open orders: {result.Error?.Message ?? "Unknown error"}");
        }

        if (result.Data.Orders != null)
        {
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
                    UpdateTime = o.UpdateTime,
                    Source = "futures",
                    ProductType = "USDC-FUTURES",
                    MarginAsset = "USDC"
                });
            }
        }
    }
}
