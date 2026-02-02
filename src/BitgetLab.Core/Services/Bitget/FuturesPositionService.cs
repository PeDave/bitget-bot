using Bitget.Net.Enums;
using Bitget.Net.Interfaces.Clients;
using BitgetLab.Core.Models;
using System.Collections.Concurrent;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for retrieving futures position information using Bitget.Net SDK
/// </summary>
public interface IFuturesPositionService
{
    /// <summary>
    /// Gets futures positions for USDT and/or USDC futures
    /// </summary>
    /// <param name="includeUsdt">Include USDT futures positions</param>
    /// <param name="includeUsdc">Include USDC futures positions</param>
    /// <param name="usdtMarginAsset">Optional margin asset override for USDT futures (defaults to "USDT")</param>
    /// <param name="usdcMarginAsset">Optional margin asset override for USDC futures (defaults to "USDC")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of futures positions</returns>
    Task<IEnumerable<FuturesPositionDto>> GetFuturesPositionsAsync(
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? usdtMarginAsset = null,
        string? usdcMarginAsset = null,
        CancellationToken cancellationToken = default);
}

public class FuturesPositionService : IFuturesPositionService
{
    private readonly IBitgetClientFactory _clientFactory;

    public FuturesPositionService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<FuturesPositionDto>> GetFuturesPositionsAsync(
        bool includeUsdt = true,
        bool includeUsdc = true,
        string? usdtMarginAsset = null,
        string? usdcMarginAsset = null,
        CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var positions = new ConcurrentBag<FuturesPositionDto>();
        var tasks = new List<Task>();

        // Query USDT Futures in parallel
        if (includeUsdt)
        {
            var usdtTask = GetUsdtPositionsAsync(client, usdtMarginAsset ?? "USDT", positions, cancellationToken);
            tasks.Add(usdtTask);
        }
        
        // Query USDC Futures in parallel
        if (includeUsdc)
        {
            var usdcTask = GetUsdcPositionsAsync(client, usdcMarginAsset ?? "USDC", positions, cancellationToken);
            tasks.Add(usdcTask);
        }
        
        // Wait for all tasks to complete
        await Task.WhenAll(tasks);
        
        return positions;
    }

    private async Task GetUsdtPositionsAsync(
        IBitgetRestClient client,
        string marginAsset,
        ConcurrentBag<FuturesPositionDto> positions,
        CancellationToken cancellationToken)
    {
        var result = await client.FuturesApiV2.Trading.GetPositionsAsync(
            BitgetProductTypeV2.UsdtFutures,
            marginAsset,
            cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get USDT futures positions: {result.Error?.Message ?? "Unknown error"}");
        }

        foreach (var p in result.Data)
        {
            positions.Add(new FuturesPositionDto
            {
                Symbol = p.Symbol,
                PositionSide = p.PositionSide.ToString(),
                Total = p.Total,
                Available = p.Available,
                AverageOpenPrice = p.AverageOpenPrice,
                UnrealizedPnl = p.UnrealizedProfitAndLoss,
                Leverage = p.Leverage,
                LiquidationPrice = p.LiquidationPrice,
                UpdateTime = p.UpdateTime,
                ProductType = "USDT-FUTURES",
                MarginAsset = marginAsset
            });
        }
    }

    private async Task GetUsdcPositionsAsync(
        IBitgetRestClient client,
        string marginAsset,
        ConcurrentBag<FuturesPositionDto> positions,
        CancellationToken cancellationToken)
    {
        var result = await client.FuturesApiV2.Trading.GetPositionsAsync(
            BitgetProductTypeV2.UsdcFutures,
            marginAsset,
            cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get USDC futures positions: {result.Error?.Message ?? "Unknown error"}");
        }

        foreach (var p in result.Data)
        {
            positions.Add(new FuturesPositionDto
            {
                Symbol = p.Symbol,
                PositionSide = p.PositionSide.ToString(),
                Total = p.Total,
                Available = p.Available,
                AverageOpenPrice = p.AverageOpenPrice,
                UnrealizedPnl = p.UnrealizedProfitAndLoss,
                Leverage = p.Leverage,
                LiquidationPrice = p.LiquidationPrice,
                UpdateTime = p.UpdateTime,
                ProductType = "USDC-FUTURES",
                MarginAsset = marginAsset
            });
        }
    }
}
