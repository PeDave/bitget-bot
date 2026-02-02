using Bitget.Net.Enums;
using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for querying copy trading orders using Bitget.Net SDK
/// </summary>
public interface ICopyTradingService
{
    /// <summary>
    /// Gets current copy trading orders
    /// </summary>
    /// <param name="productType">Product type (USDT-FUTURES or USDC-FUTURES)</param>
    /// <param name="limit">Maximum number of results (default 20)</param>
    /// <param name="symbol">Optional symbol filter</param>
    /// <param name="traderId">Optional trader ID filter</param>
    /// <param name="idLessThan">Optional ID filter for pagination</param>
    /// <param name="idGreaterThan">Optional ID filter for pagination</param>
    /// <param name="startTime">Optional start time filter</param>
    /// <param name="endTime">Optional end time filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of current copy trading orders</returns>
    Task<IEnumerable<CopyTradingCurrentOrderDto>> GetCurrentOrdersAsync(
        string productType = "USDT-FUTURES",
        int limit = 20,
        string? symbol = null,
        string? traderId = null,
        string? idLessThan = null,
        string? idGreaterThan = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default);
}

public class CopyTradingService : ICopyTradingService
{
    private readonly IBitgetClientFactory _clientFactory;

    public CopyTradingService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<CopyTradingCurrentOrderDto>> GetCurrentOrdersAsync(
        string productType = "USDT-FUTURES",
        int limit = 20,
        string? symbol = null,
        string? traderId = null,
        string? idLessThan = null,
        string? idGreaterThan = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        // Map product type string to enum
        var bitgetProductType = productType.ToUpperInvariant() switch
        {
            "USDT-FUTURES" => BitgetProductTypeV2.UsdtFutures,
            "USDC-FUTURES" => BitgetProductTypeV2.UsdcFutures,
            _ => BitgetProductTypeV2.UsdtFutures
        };

        var result = await client.CopyTradingFuturesV2.Follower.GetCurrentOrdersAsync(
            productType: bitgetProductType,
            idLessThan: idLessThan,
            idGreaterThan: idGreaterThan,
            startTime: startTime,
            endTime: endTime,
            limit: limit,
            symbol: symbol,
            traderId: traderId,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get copy trading current orders: {result.Error?.Message ?? "Unknown error"}");
        }

        // Handle the TrackingList properly - it may be null
        if (result.Data.TrackingList == null)
        {
            return new List<CopyTradingCurrentOrderDto>();
        }
        
        return result.Data.TrackingList.Select(o => new CopyTradingCurrentOrderDto
        {
            TrackingNo = o.TrackingNo,
            Symbol = o.Symbol,
            TraderId = o.TraderId,
            TraderName = o.TraderName,
            OpenOrderId = o.OpenOrderId,
            CloseOrderId = o.CloseOrderId,
            PositionSide = o.PositionSide.ToString(),
            OpenLeverage = o.OpenLeverage,
            OpenAveragePrice = o.OpenAveragePrice,
            OpenSize = o.OpenSize,
            OpenFee = o.OpenFee,
            OpenMarginSize = o.OpenMarginSize,
            OpenTime = o.OpenTime,
            CloseAveragePrice = o.CloseAveragePrice,
            CloseSize = o.CloseSize,
            CloseTime = o.CloseTime,
            ProductType = productType
        }).ToList();
    }
}
