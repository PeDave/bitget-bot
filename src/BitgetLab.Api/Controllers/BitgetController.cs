using Microsoft.AspNetCore.Mvc;
using BitgetLab.Core.Services.Bitget;

namespace BitgetLab.Api.Controllers;

[ApiController]
[Route("api/bitget")]
public class BitgetController : ControllerBase
{
    private readonly IMarketDataService _marketDataService;
    private readonly ITradingService _tradingService;
    private readonly IAccountBalanceService _accountBalanceService;
    private readonly IFuturesPositionService _futuresPositionService;
    private readonly IAccountValuationService _accountValuationService;
    private readonly ISpotOrderService _spotOrderService;
    private readonly IFuturesOrderService _futuresOrderService;
    private readonly ICopyTradingService _copyTradingService;
    private readonly IBitgetClientFactory _clientFactory;
    private readonly ILogger<BitgetController> _logger;

    public BitgetController(
        IMarketDataService marketDataService,
        ITradingService tradingService,
        IAccountBalanceService accountBalanceService,
        IFuturesPositionService futuresPositionService,
        IAccountValuationService accountValuationService,
        ISpotOrderService spotOrderService,
        IFuturesOrderService futuresOrderService,
        ICopyTradingService copyTradingService,
        IBitgetClientFactory clientFactory,
        ILogger<BitgetController> logger)
    {
        _marketDataService = marketDataService;
        _tradingService = tradingService;
        _accountBalanceService = accountBalanceService;
        _futuresPositionService = futuresPositionService;
        _accountValuationService = accountValuationService;
        _spotOrderService = spotOrderService;
        _futuresOrderService = futuresOrderService;
        _copyTradingService = copyTradingService;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    [HttpGet("symbols")]
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
    public async Task<IActionResult> GetTicker([FromQuery] string symbol, CancellationToken cancellationToken)
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
    public async Task<IActionResult> PlaceOrder([FromBody] OrderRequest orderRequest, CancellationToken cancellationToken)
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

    [HttpGet("spot/balances")]
    public async Task<IActionResult> GetSpotBalances(
        [FromQuery] decimal? minValue = null,
        [FromQuery] bool nonZeroOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var balances = await _accountBalanceService.GetSpotBalancesAsync(cancellationToken);
            
            // Apply filters
            if (nonZeroOnly || minValue.HasValue)
            {
                var threshold = minValue ?? 0m;
                balances = balances.Where(b => b.Total > threshold);
            }
            
            var balanceList = balances.ToList();
            
            return Ok(new
            {
                success = true,
                data = balanceList,
                count = balanceList.Count
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting spot balances");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve spot balances from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get spot balances");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("futures/balances")]
    public async Task<IActionResult> GetFuturesBalances(
        [FromQuery] decimal? minValue = null,
        [FromQuery] bool nonZeroOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var balances = await _accountBalanceService.GetFuturesBalancesAsync(cancellationToken);
            
            // Apply filters
            if (nonZeroOnly || minValue.HasValue)
            {
                var threshold = minValue ?? 0m;
                balances = balances.Where(b => b.Total > threshold);
            }
            
            var balanceList = balances.ToList();
            
            return Ok(new
            {
                success = true,
                data = balanceList,
                count = balanceList.Count
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting futures balances");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve futures balances from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get futures balances");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("futures/positions")]
    public async Task<IActionResult> GetFuturesPositions(
        [FromQuery] bool nonZeroOnly = false,
        [FromQuery] bool includeUsdt = true,
        [FromQuery] bool includeUsdc = true,
        [FromQuery] string? usdtMarginAsset = null,
        [FromQuery] string? usdcMarginAsset = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var positions = await _futuresPositionService.GetFuturesPositionsAsync(
                includeUsdt,
                includeUsdc,
                usdtMarginAsset,
                usdcMarginAsset,
                cancellationToken);
            
            // Apply filter
            if (nonZeroOnly)
            {
                positions = positions.Where(p => p.Total != 0);
            }
            
            var positionList = positions.ToList();
            
            return Ok(new
            {
                success = true,
                data = positionList,
                count = positionList.Count
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting futures positions");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve futures positions from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get futures positions");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("account/valuation")]
    public async Task<IActionResult> GetAccountValuation(CancellationToken cancellationToken)
    {
        try
        {
            var valuations = await _accountValuationService.GetAccountValuationAsync(cancellationToken);
            var valuationList = valuations.ToList();
            
            return Ok(new
            {
                success = true,
                data = valuationList,
                count = valuationList.Count
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting account valuation");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve account valuation from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get account valuation");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("spot/orders/open")]
    public async Task<IActionResult> GetSpotOpenOrders(
        [FromQuery] string? symbol = null,
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orders = await _spotOrderService.GetOpenOrdersAsync(symbol, limit, cancellationToken);
            var orderList = orders.ToList();
            
            return Ok(new
            {
                success = true,
                data = orderList,
                count = orderList.Count
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting spot open orders");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve spot open orders from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get spot open orders");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("futures/orders/open")]
    public async Task<IActionResult> GetFuturesOpenOrders(
        [FromQuery] string? productType = null,
        [FromQuery] string? symbol = null,
        [FromQuery] bool includeUsdt = true,
        [FromQuery] bool includeUsdc = true,
        [FromQuery] string? marginAsset = null,
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orders = await _futuresOrderService.GetOpenOrdersAsync(
                productType,
                symbol,
                includeUsdt,
                includeUsdc,
                marginAsset,
                limit,
                cancellationToken);
            var orderList = orders.ToList();
            
            return Ok(new
            {
                success = true,
                data = orderList,
                count = orderList.Count
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting futures open orders");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve futures open orders from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get futures open orders");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("copytrading/current-orders")]
    public async Task<IActionResult> GetCopyTradingCurrentOrders(
        [FromQuery] string? productType = null,
        [FromQuery] string? symbol = null,
        [FromQuery] string? traderId = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orders = await _copyTradingService.GetCurrentOrdersAsync(
                productType,
                symbol,
                traderId,
                limit,
                cancellationToken);
            var orderList = orders.ToList();
            
            return Ok(new
            {
                success = true,
                data = orderList,
                count = orderList.Count
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting copy trading current orders");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve copy trading current orders from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get copy trading current orders");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }
}
