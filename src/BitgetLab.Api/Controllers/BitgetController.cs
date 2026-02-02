using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using BitgetLab.Core.Services.Bitget;

namespace BitgetLab.Api.Controllers;

[ApiController]
[Route("api/bitget")]
public class BitgetController : ControllerBase
{
    private readonly IMarketDataService _marketDataService;
    private readonly ITradingService _tradingService;
    private readonly IBitgetClientFactory _clientFactory;
    private readonly ILogger<BitgetController> _logger;

    public BitgetController(
        IMarketDataService marketDataService,
        ITradingService tradingService,
        IBitgetClientFactory clientFactory,
        ILogger<BitgetController> logger)
    {
        _marketDataService = marketDataService;
        _tradingService = tradingService;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    [HttpGet("symbols")]
    [SwaggerOperation(
        Summary = "Get all trading symbols",
        Description = "Retrieves a list of all available spot trading symbols from Bitget. Returns first 100 symbols for performance.",
        Tags = new[] { "Market Data" }
    )]
    [SwaggerResponse(200, "Successfully retrieved symbols")]
    [SwaggerResponse(502, "Bitget API error")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<IActionResult> GetSymbols(CancellationToken cancellationToken)
    {
        try
        {
            var symbols = await _marketDataService.GetSymbolsAsync(cancellationToken);
            var symbolsList = symbols.ToList();
            
            return Ok(new
            {
                success = true,
                symbols = symbolsList.Take(100).ToList(), // Limit response size for performance
                totalCount = symbolsList.Count,
                displayedCount = Math.Min(100, symbolsList.Count)
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting symbols");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve symbols from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get symbols");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("market/ticker")]
    [SwaggerOperation(
        Summary = "Get ticker data for a symbol",
        Description = "Retrieves current ticker data (price, volume, etc.) for a specific trading symbol.",
        Tags = new[] { "Market Data" }
    )]
    [SwaggerResponse(200, "Successfully retrieved ticker data")]
    [SwaggerResponse(400, "Symbol parameter is required")]
    [SwaggerResponse(502, "Bitget API error")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<IActionResult> GetTicker([FromQuery, SwaggerParameter("Trading symbol", Required = true)] string symbol, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new
            {
                success = false,
                error = "Symbol parameter is required"
            });
        }

        try
        {
            var ticker = await _marketDataService.GetTickerAsync(symbol, cancellationToken);
            return Ok(new
            {
                success = true,
                data = ticker
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting ticker for {Symbol}", symbol);
            return StatusCode(502, new
            {
                success = false,
                error = $"Failed to retrieve ticker for {symbol} from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ticker for {Symbol}", symbol);
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpPost("orders")]
    [SwaggerOperation(
        Summary = "Place a trading order",
        Description = "Places a spot trading order on Bitget. Requires Trade mode to be enabled in configuration. " +
                     "Enum values (Side, Type) are case-insensitive: 'buy'/'Buy', 'sell'/'Sell', 'market'/'Market', 'limit'/'Limit' are all valid.",
        Tags = new[] { "Trading" }
    )]
    [SwaggerResponse(200, "Order placed successfully", typeof(object))]
    [SwaggerResponse(400, "Invalid request - missing or invalid parameters")]
    [SwaggerResponse(403, "Trading not allowed - API is in ReadOnly mode")]
    [SwaggerResponse(502, "Bitget API error")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<IActionResult> PlaceOrder([FromBody, SwaggerRequestBody("Order details", Required = true)] OrderRequest orderRequest, CancellationToken cancellationToken)
    {
        // Check if trading is allowed
        if (!_clientFactory.IsTradeAllowed())
        {
            return StatusCode(403, new
            {
                success = false,
                error = "Trading is not allowed in ReadOnly mode",
                message = "Please configure the API in Trade mode to place orders"
            });
        }

        if (orderRequest == null || string.IsNullOrWhiteSpace(orderRequest.Symbol))
        {
            return BadRequest(new
            {
                success = false,
                error = "Invalid order request. Symbol is required."
            });
        }

        try
        {
            var result = await _tradingService.PlaceOrderAsync(orderRequest, cancellationToken);
            return Ok(new
            {
                success = true,
                data = result
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ReadOnly"))
        {
            return StatusCode(403, new
            {
                success = false,
                error = ex.Message
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while placing order for {Symbol}", orderRequest.Symbol);
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to place order on Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place order for {Symbol}", orderRequest.Symbol);
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }
}
