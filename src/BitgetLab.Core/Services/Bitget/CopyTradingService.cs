using BitgetLab.Core.Models;
using Bitget.Net.Interfaces.Clients;
using Bitget.Net.Enums;
using Bitget.Net.Objects.Models.V2;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for retrieving copy trading information using Bitget.Net SDK
/// </summary>
public interface ICopyTradingService
{
    /// <summary>
    /// Gets current copy trading orders
    /// </summary>
    /// <param name="productType">Product type (default: USDT-FUTURES)</param>
    /// <param name="symbol">Optional symbol filter</param>
    /// <param name="traderId">Optional trader ID filter</param>
    /// <param name="limit">Optional limit for number of results (default: 20, max: 50)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of current copy trading orders</returns>
    Task<IEnumerable<CopyTradingOrderDto>> GetCurrentOrdersAsync(
        string? productType = null,
        string? symbol = null,
        string? traderId = null,
        int limit = 20,
        CancellationToken cancellationToken = default);
}

public class CopyTradingService : ICopyTradingService
{
    private readonly IBitgetClientFactory _clientFactory;

    public CopyTradingService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<CopyTradingOrderDto>> GetCurrentOrdersAsync(
        string? productType = null,
        string? symbol = null,
        string? traderId = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        // Default to USDT-FUTURES if not specified
        var bitgetProductType = BitgetProductTypeV2.UsdtFutures;
        if (!string.IsNullOrEmpty(productType))
        {
            if (productType.Equals("USDC-FUTURES", StringComparison.OrdinalIgnoreCase))
            {
                bitgetProductType = BitgetProductTypeV2.UsdcFutures;
            }
        }
        
        var result = await client.CopyTradingFuturesV2.Follower.GetCurrentOrdersAsync(
            bitgetProductType,
            symbol: symbol,
            traderId: traderId,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get copy trading current orders: {result.Error?.Message ?? "Unknown error"}");
        }

        var orders = new List<CopyTradingOrderDto>();
        
        foreach (var trackingItem in result.Data.TrackingList ?? Array.Empty<BitgetCopyTradingCurrentOrdersTrackingItem>())
        {
            orders.Add(new CopyTradingOrderDto
            {
                OrderId = trackingItem.OpenOrderId,
                Symbol = trackingItem.Symbol,
                TraderId = trackingItem.TraderId,
                TraderName = trackingItem.TraderName,
                Side = trackingItem.PositionSide.ToString(),
                OrderType = "Market", // CopyTrading typically uses market orders
                Quantity = trackingItem.OpenSize,
                Price = trackingItem.OpenAveragePrice,
                ProductType = bitgetProductType == BitgetProductTypeV2.UsdtFutures ? "USDT-FUTURES" : "USDC-FUTURES",
                CreateTime = trackingItem.OpenTime
            });
        }
        
        return orders;
    }
}
