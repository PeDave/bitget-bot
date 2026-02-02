using Bitget.Net.Clients;
using Bitget.Net.Enums.V2;
using Microsoft.Extensions.Options;
using BitgetLab.Core.Models;
using BitgetLab.Core.Options;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for placing trades on Bitget
/// </summary>
public interface ITradingService
{
    /// <summary>
    /// Place an order on Bitget
    /// </summary>
    Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken cancellationToken = default);
}

public class TradingService : ITradingService
{
    private readonly BitgetRestClient _client;
    private readonly BitgetOptions _options;
    
    public TradingService(IBitgetClientFactory factory, IOptions<BitgetOptions> options)
    {
        _client = factory.CreateClientForCurrentMode();
        _options = options.Value;
    }

    public async Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken cancellationToken = default)
    {
        // Safety check: prevent trading in ReadOnly mode
        if (_options.Mode?.ToLowerInvariant() != "trade")
        {
            throw new InvalidOperationException("Cannot place orders in ReadOnly mode. Set Bitget:Mode to 'Trade' in configuration.");
        }

        // Validate request
        if (string.IsNullOrWhiteSpace(request.Symbol))
        {
            throw new ArgumentException("Symbol is required", nameof(request));
        }

        if (request.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be positive", nameof(request));
        }

        // Map side
        OrderSide side = request.Side?.ToLowerInvariant() switch
        {
            "buy" => OrderSide.Buy,
            "sell" => OrderSide.Sell,
            _ => throw new ArgumentException($"Invalid order side: {request.Side}. Must be 'Buy' or 'Sell'", nameof(request))
        };

        try
        {
            // Place order - using market order if no price specified, otherwise limit order
            if (request.Price.HasValue && request.Price.Value > 0)
            {
                // Limit order
                var result = await _client.SpotApiV2.Trading.PlaceOrderAsync(
                    symbol: request.Symbol,
                    side: side,
                    type: OrderType.Limit,
                    quantity: request.Quantity,
                    timeInForce: TimeInForce.GoodTillCanceled,
                    price: request.Price.Value,
                    ct: cancellationToken
                );

                if (!result.Success)
                {
                    return new OrderResult
                    {
                        OrderId = string.Empty,
                        Symbol = request.Symbol,
                        Status = "Failed",
                        ErrorMessage = result.Error?.Message ?? "Unknown error"
                    };
                }

                return new OrderResult
                {
                    OrderId = result.Data.OrderId,
                    Symbol = request.Symbol,
                    Status = "Success"
                };
            }
            else
            {
                // Market order - use ImmediateOrCancel for market orders
                var result = await _client.SpotApiV2.Trading.PlaceOrderAsync(
                    symbol: request.Symbol,
                    side: side,
                    type: OrderType.Market,
                    quantity: request.Quantity,
                    timeInForce: TimeInForce.ImmediateOrCancel,
                    ct: cancellationToken
                );

                if (!result.Success)
                {
                    return new OrderResult
                    {
                        OrderId = string.Empty,
                        Symbol = request.Symbol,
                        Status = "Failed",
                        ErrorMessage = result.Error?.Message ?? "Unknown error"
                    };
                }

                return new OrderResult
                {
                    OrderId = result.Data.OrderId,
                    Symbol = request.Symbol,
                    Status = "Success"
                };
            }
        }
        catch (Exception ex)
        {
            return new OrderResult
            {
                OrderId = string.Empty,
                Symbol = request.Symbol,
                Status = "Failed",
                ErrorMessage = ex.Message
            };
        }
    }
}
