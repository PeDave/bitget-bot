using Bitget.Net.Enums;
using Bitget.Net.Interfaces.Clients;
using BitgetLab.Core.Models;
using System.Collections.Concurrent;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for querying futures order history and trades using Bitget.Net SDK
/// </summary>
public interface IFuturesOrderHistoryService
{
    /// <summary>
    /// Gets futures closed orders for USDT and/or USDC futures
    /// </summary>
    /// <param name="includeUsdt">Include USDT futures orders</param>
    /// <param name="includeUsdc">Include USDC futures orders</param>
    /// <param name="symbol">Optional symbol filter</param>
    /// <param name="orderId">Optional order ID filter</param>
    /// <param name="clientOrderId">Optional client order ID filter</param>
    /// <param name="startTime">Optional start time filter</param>
    /// <param name="endTime">Optional end time filter</param>
    /// <param name="idLessThan">Optional ID filter for pagination</param>
    /// <param name="limit">Maximum number of results per product type (default 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of closed orders</returns>
    Task<IEnumerable<OrderDetailDto>> GetClosedOrdersAsync(
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? symbol = null,
        string? orderId = null,
        string? clientOrderId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detail for a specific futures order
    /// </summary>
    /// <param name="includeUsdt">Include USDT futures when searching</param>
    /// <param name="includeUsdc">Include USDC futures when searching</param>
    /// <param name="productType">Optional product type override (USDT-FUTURES or USDC-FUTURES)</param>
    /// <param name="symbol">Trading symbol (required)</param>
    /// <param name="orderId">Order ID (exactly one of orderId or clientOrderId required)</param>
    /// <param name="clientOrderId">Client order ID (exactly one of orderId or clientOrderId required)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order detail</returns>
    Task<OrderDetailDto> GetOrderDetailAsync(
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? productType = null,
        string? symbol = null,
        string? orderId = null,
        string? clientOrderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets futures user trades for USDT and/or USDC futures
    /// </summary>
    /// <param name="includeUsdt">Include USDT futures trades</param>
    /// <param name="includeUsdc">Include USDC futures trades</param>
    /// <param name="symbol">Optional symbol filter</param>
    /// <param name="orderId">Optional order ID filter</param>
    /// <param name="startTime">Optional start time filter</param>
    /// <param name="endTime">Optional end time filter</param>
    /// <param name="idLessThan">Optional ID filter for pagination</param>
    /// <param name="limit">Maximum number of results per product type (default 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of user trades</returns>
    Task<IEnumerable<UserTradeDto>> GetUserTradesAsync(
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? symbol = null,
        string? orderId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public class FuturesOrderHistoryService : IFuturesOrderHistoryService
{
    private readonly IBitgetClientFactory _clientFactory;

    public FuturesOrderHistoryService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<OrderDetailDto>> GetClosedOrdersAsync(
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? symbol = null,
        string? orderId = null,
        string? clientOrderId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var orders = new ConcurrentBag<OrderDetailDto>();
        var tasks = new List<Task>();

        // Query USDT Futures in parallel
        if (includeUsdt)
        {
            var usdtTask = GetClosedOrdersForProductTypeAsync(
                client, BitgetProductTypeV2.UsdtFutures, "USDT-FUTURES", "USDT",
                symbol, orderId, clientOrderId, startTime, endTime, idLessThan, limit, orders, cancellationToken);
            tasks.Add(usdtTask);
        }
        
        // Query USDC Futures in parallel
        if (includeUsdc)
        {
            var usdcTask = GetClosedOrdersForProductTypeAsync(
                client, BitgetProductTypeV2.UsdcFutures, "USDC-FUTURES", "USDC",
                symbol, orderId, clientOrderId, startTime, endTime, idLessThan, limit, orders, cancellationToken);
            tasks.Add(usdcTask);
        }
        
        // Wait for all tasks to complete
        await Task.WhenAll(tasks);
        
        return orders;
    }

    private async Task GetClosedOrdersForProductTypeAsync(
        IBitgetRestClient client,
        BitgetProductTypeV2 productType,
        string productTypeName,
        string marginAsset,
        string? symbol,
        string? orderId,
        string? clientOrderId,
        DateTime? startTime,
        DateTime? endTime,
        string? idLessThan,
        int limit,
        ConcurrentBag<OrderDetailDto> orders,
        CancellationToken cancellationToken)
    {
        var result = await client.FuturesApiV2.Trading.GetClosedOrdersAsync(
            productType: productType,
            symbol: symbol,
            orderId: orderId,
            clientOrderId: clientOrderId,
            startTime: startTime,
            endTime: endTime,
            idLessThan: idLessThan,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get {productTypeName} closed orders: {result.Error?.Message ?? "Unknown error"}");
        }

        if (result.Data?.Orders != null)
        {
            foreach (var o in result.Data.Orders)
            {
                orders.Add(new OrderDetailDto
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
                    Source = "futures",
                    ProductType = productTypeName,
                    MarginAsset = marginAsset
                });
            }
        }
    }

    public async Task<OrderDetailDto> GetOrderDetailAsync(
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? productType = null,
        string? symbol = null,
        string? orderId = null,
        string? clientOrderId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId) && string.IsNullOrWhiteSpace(clientOrderId))
        {
            throw new ArgumentException("Either orderId or clientOrderId must be provided");
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required");
        }

        using var client = _clientFactory.CreateRestClient();

        // If productType is specified, query only that product type
        if (!string.IsNullOrWhiteSpace(productType))
        {
            var pt = productType.ToUpperInvariant() switch
            {
                "USDT-FUTURES" => BitgetProductTypeV2.UsdtFutures,
                "USDC-FUTURES" => BitgetProductTypeV2.UsdcFutures,
                _ => throw new ArgumentException($"Invalid product type: {productType}")
            };
            
            var marginAsset = productType.ToUpperInvariant() == "USDT-FUTURES" ? "USDT" : "USDC";
            return await GetOrderDetailForProductTypeAsync(client, pt, productType, marginAsset, symbol, orderId, clientOrderId, cancellationToken);
        }

        // Otherwise, try USDT and USDC in parallel and return the first successful result
        var tasks = new List<Task<OrderDetailDto?>>();
        
        if (includeUsdt)
        {
            tasks.Add(TryGetOrderDetailForProductTypeAsync(
                client, BitgetProductTypeV2.UsdtFutures, "USDT-FUTURES", "USDT",
                symbol, orderId, clientOrderId, cancellationToken));
        }
        
        if (includeUsdc)
        {
            tasks.Add(TryGetOrderDetailForProductTypeAsync(
                client, BitgetProductTypeV2.UsdcFutures, "USDC-FUTURES", "USDC",
                symbol, orderId, clientOrderId, cancellationToken));
        }

        var results = await Task.WhenAll(tasks);
        var successfulResult = results.FirstOrDefault(r => r != null);
        
        if (successfulResult == null)
        {
            throw new BitgetApiException($"Order not found in any futures market");
        }

        return successfulResult;
    }

    private async Task<OrderDetailDto?> TryGetOrderDetailForProductTypeAsync(
        IBitgetRestClient client,
        BitgetProductTypeV2 productType,
        string productTypeName,
        string marginAsset,
        string symbol,
        string? orderId,
        string? clientOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await GetOrderDetailForProductTypeAsync(
                client, productType, productTypeName, marginAsset, symbol, orderId, clientOrderId, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<OrderDetailDto> GetOrderDetailForProductTypeAsync(
        IBitgetRestClient client,
        BitgetProductTypeV2 productType,
        string productTypeName,
        string marginAsset,
        string symbol,
        string? orderId,
        string? clientOrderId,
        CancellationToken cancellationToken)
    {
        var result = await client.FuturesApiV2.Trading.GetOrderAsync(
            productType: productType,
            symbol: symbol,
            orderId: orderId,
            clientOrderId: clientOrderId,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get {productTypeName} order detail: {result.Error?.Message ?? "Unknown error"}");
        }

        var o = result.Data;
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
            Source = "futures",
            ProductType = productTypeName,
            MarginAsset = marginAsset
        };
    }

    public async Task<IEnumerable<UserTradeDto>> GetUserTradesAsync(
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? symbol = null,
        string? orderId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? idLessThan = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var trades = new ConcurrentBag<UserTradeDto>();
        var tasks = new List<Task>();

        // Query USDT Futures in parallel
        if (includeUsdt)
        {
            var usdtTask = GetUserTradesForProductTypeAsync(
                client, BitgetProductTypeV2.UsdtFutures, "USDT-FUTURES", "USDT",
                symbol, orderId, startTime, endTime, idLessThan, limit, trades, cancellationToken);
            tasks.Add(usdtTask);
        }
        
        // Query USDC Futures in parallel
        if (includeUsdc)
        {
            var usdcTask = GetUserTradesForProductTypeAsync(
                client, BitgetProductTypeV2.UsdcFutures, "USDC-FUTURES", "USDC",
                symbol, orderId, startTime, endTime, idLessThan, limit, trades, cancellationToken);
            tasks.Add(usdcTask);
        }
        
        // Wait for all tasks to complete
        await Task.WhenAll(tasks);
        
        return trades;
    }

    private async Task GetUserTradesForProductTypeAsync(
        IBitgetRestClient client,
        BitgetProductTypeV2 productType,
        string productTypeName,
        string marginAsset,
        string? symbol,
        string? orderId,
        DateTime? startTime,
        DateTime? endTime,
        string? idLessThan,
        int limit,
        ConcurrentBag<UserTradeDto> trades,
        CancellationToken cancellationToken)
    {
        var result = await client.FuturesApiV2.Trading.GetUserTradesAsync(
            productType: productType,
            symbol: symbol,
            orderId: orderId,
            startTime: startTime,
            endTime: endTime,
            idLessThan: idLessThan,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get {productTypeName} user trades: {result.Error?.Message ?? "Unknown error"}");
        }

        if (result.Data?.Trades != null)
        {
            foreach (var t in result.Data.Trades)
            {
                // For futures, fees is an array - we need to aggregate
                var feeAsset = string.Empty;
                var fee = 0m;
                var totalDeduction = 0m;

                if (t.Fees != null && t.Fees.Length > 0)
                {
                    // Use the first fee entry for primary fee info
                    feeAsset = t.Fees[0].FeeAsset;
                    fee = t.Fees[0].TotalFee;
                    
                    // Sum total deduction from all fee entries
                    totalDeduction = t.Fees.Sum(f => f.TotalDeductionFee ?? 0m);
                }

                trades.Add(new UserTradeDto
                {
                    TradeId = t.TradeId,
                    OrderId = t.OrderId,
                    ClientOrderId = null, // Futures trades don't have client order ID
                    Symbol = t.Symbol,
                    Side = t.Side.ToString(),
                    Price = t.Price,
                    Quantity = t.Quantity,
                    TradeTime = t.CreateTime,
                    FeeAsset = feeAsset,
                    Fee = fee,
                    FeeDeduction = totalDeduction, // Use totalDeduction for both fields as per Bitget.Net model
                    FeeTotalDeduction = totalDeduction,
                    Source = "futures",
                    ProductType = productTypeName,
                    MarginAsset = marginAsset
                });
            }
        }
    }
}
