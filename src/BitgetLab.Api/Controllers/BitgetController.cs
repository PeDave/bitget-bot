using Microsoft.AspNetCore.Mvc;
using BitgetLab.Core.Services.Bitget;
using BitgetLab.Core.Models;

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
    private readonly ISpotOrderQueryService _spotOrderQueryService;
    private readonly IFuturesOrderQueryService _futuresOrderQueryService;
    private readonly ISpotOrderHistoryService _spotOrderHistoryService;
    private readonly IFuturesOrderHistoryService _futuresOrderHistoryService;
    private readonly ICopyTradingService _copyTradingService;
    private readonly IBitgetClientFactory _clientFactory;
    private readonly ICandleService _candleService;
    private readonly IIndicatorService _indicatorService;
    private readonly IWebSocketSubscriptionService _subscriptionService;
    private readonly ILogger<BitgetController> _logger;

    public BitgetController(
        IMarketDataService marketDataService,
        ITradingService tradingService,
        IAccountBalanceService accountBalanceService,
        IFuturesPositionService futuresPositionService,
        IAccountValuationService accountValuationService,
        ISpotOrderQueryService spotOrderQueryService,
        IFuturesOrderQueryService futuresOrderQueryService,
        ISpotOrderHistoryService spotOrderHistoryService,
        IFuturesOrderHistoryService futuresOrderHistoryService,
        ICopyTradingService copyTradingService,
        IBitgetClientFactory clientFactory,
        ICandleService candleService,
        IIndicatorService indicatorService,
        IWebSocketSubscriptionService subscriptionService,
        ILogger<BitgetController> logger)
    {
        _marketDataService = marketDataService;
        _tradingService = tradingService;
        _accountBalanceService = accountBalanceService;
        _futuresPositionService = futuresPositionService;
        _accountValuationService = accountValuationService;
        _spotOrderQueryService = spotOrderQueryService;
        _futuresOrderQueryService = futuresOrderQueryService;
        _spotOrderHistoryService = spotOrderHistoryService;
        _futuresOrderHistoryService = futuresOrderHistoryService;
        _copyTradingService = copyTradingService;
        _clientFactory = clientFactory;
        _candleService = candleService;
        _indicatorService = indicatorService;
        _subscriptionService = subscriptionService;
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
        [FromQuery] string? idLessThan = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orders = await _spotOrderQueryService.GetOpenOrdersAsync(symbol, idLessThan, limit, cancellationToken);
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
        [FromQuery] bool includeUsdt = true,
        [FromQuery] bool includeUsdc = true,
        [FromQuery] string? symbol = null,
        [FromQuery] string? status = null,
        [FromQuery] string? idLessThan = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orders = await _futuresOrderQueryService.GetOpenOrdersAsync(
                includeUsdt,
                includeUsdc,
                symbol,
                status,
                idLessThan,
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
        [FromQuery] string productType = "USDT-FUTURES",
        [FromQuery] int limit = 20,
        [FromQuery] string? symbol = null,
        [FromQuery] string? traderId = null,
        [FromQuery] string? idLessThan = null,
        [FromQuery] string? idGreaterThan = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orders = await _copyTradingService.GetCurrentOrdersAsync(
                productType,
                limit,
                symbol,
                traderId,
                idLessThan,
                idGreaterThan,
                startTime,
                endTime,
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

    [HttpGet("spot/orders/closed")]
    public async Task<IActionResult> GetSpotClosedOrders(
        [FromQuery] string? symbol = null,
        [FromQuery] string? orderId = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] string? idLessThan = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orders = await _spotOrderHistoryService.GetClosedOrdersAsync(
                symbol, orderId, startTime, endTime, idLessThan, limit, cancellationToken);
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
            _logger.LogError(ex, "Bitget API error while getting spot closed orders");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve spot closed orders from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get spot closed orders");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("spot/orders/detail")]
    public async Task<IActionResult> GetSpotOrderDetail(
        [FromQuery] string symbol,
        [FromQuery] string? orderId = null,
        [FromQuery] string? clientOrderId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new
            {
                success = false,
                error = "Symbol parameter is required"
            });
        }

        // XOR validation: require exactly one of orderId or clientOrderId
        var hasOrderId = !string.IsNullOrWhiteSpace(orderId);
        var hasClientOrderId = !string.IsNullOrWhiteSpace(clientOrderId);

        if (!hasOrderId && !hasClientOrderId)
        {
            return BadRequest(new
            {
                success = false,
                error = "Exactly one of orderId or clientOrderId is required"
            });
        }

        if (hasOrderId && hasClientOrderId)
        {
            return BadRequest(new
            {
                success = false,
                error = "Cannot provide both orderId and clientOrderId - provide exactly one"
            });
        }

        try
        {
            var order = await _spotOrderHistoryService.GetOrderDetailAsync(
                symbol, orderId, clientOrderId, cancellationToken);
            
            return Ok(new
            {
                success = true,
                data = new[] { order },
                count = 1
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting spot order detail");
            
            // Map parameter errors to HTTP 400
            if (IsParameterError(ex.Message))
            {
                return BadRequest(new
                {
                    success = false,
                    error = SanitizeErrorMessage(ex.Message)
                });
            }

            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve spot order detail from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get spot order detail");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("spot/trades")]
    public async Task<IActionResult> GetSpotTrades(
        [FromQuery] string? symbol = null,
        [FromQuery] string? orderId = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] string? idLessThan = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var trades = await _spotOrderHistoryService.GetUserTradesAsync(
                symbol, orderId, startTime, endTime, idLessThan, limit, cancellationToken);
            var tradeList = trades.ToList();
            
            return Ok(new
            {
                success = true,
                data = tradeList,
                count = tradeList.Count
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting spot trades");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve spot trades from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get spot trades");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("futures/orders/closed")]
    public async Task<IActionResult> GetFuturesClosedOrders(
        [FromQuery] bool includeUsdt = true,
        [FromQuery] bool includeUsdc = true,
        [FromQuery] string? symbol = null,
        [FromQuery] string? orderId = null,
        [FromQuery] string? clientOrderId = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] string? idLessThan = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orders = await _futuresOrderHistoryService.GetClosedOrdersAsync(
                includeUsdt, includeUsdc, symbol, orderId, clientOrderId, 
                startTime, endTime, idLessThan, limit, cancellationToken);
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
            _logger.LogError(ex, "Bitget API error while getting futures closed orders");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve futures closed orders from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get futures closed orders");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("futures/orders/detail")]
    public async Task<IActionResult> GetFuturesOrderDetail(
        [FromQuery] bool includeUsdt = true,
        [FromQuery] bool includeUsdc = true,
        [FromQuery] string? productType = null,
        [FromQuery] string? symbol = null,
        [FromQuery] string? orderId = null,
        [FromQuery] string? clientOrderId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new
            {
                success = false,
                error = "Symbol parameter is required"
            });
        }

        // XOR validation: require exactly one of orderId or clientOrderId
        var hasOrderId = !string.IsNullOrWhiteSpace(orderId);
        var hasClientOrderId = !string.IsNullOrWhiteSpace(clientOrderId);

        if (!hasOrderId && !hasClientOrderId)
        {
            return BadRequest(new
            {
                success = false,
                error = "Exactly one of orderId or clientOrderId is required"
            });
        }

        if (hasOrderId && hasClientOrderId)
        {
            return BadRequest(new
            {
                success = false,
                error = "Cannot provide both orderId and clientOrderId - provide exactly one"
            });
        }

        try
        {
            var order = await _futuresOrderHistoryService.GetOrderDetailAsync(
                includeUsdt, includeUsdc, productType, symbol, orderId, clientOrderId, cancellationToken);
            
            return Ok(new
            {
                success = true,
                data = new[] { order },
                count = 1
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting futures order detail");
            
            // Map parameter errors to HTTP 400
            if (IsParameterError(ex.Message))
            {
                return BadRequest(new
                {
                    success = false,
                    error = SanitizeErrorMessage(ex.Message)
                });
            }

            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve futures order detail from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get futures order detail");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("futures/trades")]
    public async Task<IActionResult> GetFuturesTrades(
        [FromQuery] bool includeUsdt = true,
        [FromQuery] bool includeUsdc = true,
        [FromQuery] string? symbol = null,
        [FromQuery] string? orderId = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] string? idLessThan = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var trades = await _futuresOrderHistoryService.GetUserTradesAsync(
                includeUsdt, includeUsdc, symbol, orderId, 
                startTime, endTime, idLessThan, limit, cancellationToken);
            var tradeList = trades.ToList();
            
            return Ok(new
            {
                success = true,
                data = tradeList,
                count = tradeList.Count
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting futures trades");
            return StatusCode(502, new
            {
                success = false,
                error = "Failed to retrieve futures trades from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get futures trades");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("market/candles")]
    public async Task<IActionResult> GetCandles(
        [FromQuery] string symbol,
        [FromQuery] string interval,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new
            {
                success = false,
                error = "Symbol parameter is required"
            });
        }

        if (string.IsNullOrWhiteSpace(interval))
        {
            return BadRequest(new
            {
                success = false,
                error = "Interval parameter is required"
            });
        }

        try
        {
            var candles = await _candleService.GetCandlesAsync(
                symbol, interval, startTime, endTime, limit, cancellationToken);
            var candleList = candles.ToList();

            return Ok(new
            {
                success = true,
                data = candleList,
                count = candleList.Count
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while getting candles for {Symbol}", symbol);
            
            // Map parameter errors to HTTP 400
            if (IsParameterError(ex.Message))
            {
                return BadRequest(new
                {
                    success = false,
                    error = SanitizeErrorMessage(ex.Message)
                });
            }

            return StatusCode(502, new
            {
                success = false,
                error = $"Failed to retrieve candles for {symbol} from Bitget",
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for getting candles");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get candles for {Symbol}", symbol);
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("market/latest-candle")]
    public IActionResult GetLatestCandle(
        [FromQuery] string symbol,
        [FromQuery] string interval)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new
            {
                success = false,
                error = "Symbol parameter is required"
            });
        }

        if (string.IsNullOrWhiteSpace(interval))
        {
            return BadRequest(new
            {
                success = false,
                error = "Interval parameter is required"
            });
        }

        try
        {
            var candle = _subscriptionService.GetLatestCandle(symbol, interval);
            
            if (candle != null)
            {
                return Ok(new
                {
                    success = true,
                    data = new[] { candle },
                    count = 1
                });
            }

            return Ok(new
            {
                success = true,
                data = Array.Empty<CandleDto>(),
                count = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest candle for {Symbol} {Interval}", symbol, interval);
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("market/candle-buffer")]
    public IActionResult GetCandleBuffer(
        [FromQuery] string symbol,
        [FromQuery] string interval,
        [FromQuery] int limit = 500)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new
            {
                success = false,
                error = "Symbol parameter is required"
            });
        }

        if (string.IsNullOrWhiteSpace(interval))
        {
            return BadRequest(new
            {
                success = false,
                error = "Interval parameter is required"
            });
        }

        // Validate and cap limit
        if (limit <= 0)
        {
            limit = 500;
        }
        if (limit > 500)
        {
            limit = 500;
        }

        try
        {
            var candles = _subscriptionService.GetCandleBuffer(symbol, interval, limit);
            
            return Ok(new
            {
                success = true,
                data = candles,
                count = candles.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get candle buffer for {Symbol} {Interval}", symbol, interval);
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("market/indicators")]
    public async Task<IActionResult> GetIndicators(
        [FromQuery] string symbol,
        [FromQuery] string interval,
        [FromQuery] string indicator,
        [FromQuery] int period,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new
            {
                success = false,
                error = "Symbol parameter is required"
            });
        }

        if (string.IsNullOrWhiteSpace(interval))
        {
            return BadRequest(new
            {
                success = false,
                error = "Interval parameter is required"
            });
        }

        if (string.IsNullOrWhiteSpace(indicator))
        {
            return BadRequest(new
            {
                success = false,
                error = "Indicator parameter is required (SMA, EMA, or RSI)"
            });
        }

        if (period <= 0)
        {
            return BadRequest(new
            {
                success = false,
                error = "Period must be greater than 0"
            });
        }

        try
        {
            var indicators = await _indicatorService.ComputeIndicatorAsync(
                symbol, interval, indicator, period, startTime, endTime, limit, cancellationToken);
            var indicatorList = indicators.ToList();

            return Ok(new
            {
                success = true,
                data = indicatorList,
                count = indicatorList.Count,
                indicator = indicator.ToUpperInvariant(),
                period
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error while computing indicators for {Symbol}", symbol);
            
            // Map parameter errors to HTTP 400
            if (IsParameterError(ex.Message))
            {
                return BadRequest(new
                {
                    success = false,
                    error = SanitizeErrorMessage(ex.Message)
                });
            }

            return StatusCode(502, new
            {
                success = false,
                error = $"Failed to retrieve data for computing indicators for {symbol} from Bitget",
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for computing indicators");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot compute indicators");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute indicators for {Symbol}", symbol);
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpPost("market/subscribe")]
    public async Task<IActionResult> Subscribe(
        [FromBody] SubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Symbol))
        {
            return BadRequest(new
            {
                success = false,
                error = "Symbol is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Interval))
        {
            return BadRequest(new
            {
                success = false,
                error = "Interval is required"
            });
        }

        try
        {
            var subscribed = await _subscriptionService.SubscribeAsync(
                request.Symbol, request.Interval, cancellationToken);

            return Ok(new
            {
                success = true,
                subscribed,
                symbol = request.Symbol,
                interval = request.Interval,
                message = subscribed 
                    ? "Subscription created successfully" 
                    : "Subscription already exists"
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid subscription request");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create subscription");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpPost("market/unsubscribe")]
    public async Task<IActionResult> Unsubscribe(
        [FromBody] SubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Symbol))
        {
            return BadRequest(new
            {
                success = false,
                error = "Symbol is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Interval))
        {
            return BadRequest(new
            {
                success = false,
                error = "Interval is required"
            });
        }

        try
        {
            var unsubscribed = await _subscriptionService.UnsubscribeAsync(
                request.Symbol, request.Interval, cancellationToken);

            return Ok(new
            {
                success = true,
                unsubscribed,
                symbol = request.Symbol,
                interval = request.Interval,
                message = unsubscribed 
                    ? "Unsubscribed successfully" 
                    : "Subscription not found"
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid unsubscribe request");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("market/subscriptions")]
    public IActionResult GetSubscriptions()
    {
        try
        {
            var subscriptions = _subscriptionService.GetActiveSubscriptions();
            var subscriptionList = subscriptions.Select(s => new
            {
                symbol = s.Symbol,
                interval = s.Interval,
                subscribedAt = s.SubscribedAt,
                hasLatestCandle = s.LatestCandle != null
            }).ToList();

            return Ok(new
            {
                success = true,
                data = subscriptionList,
                count = subscriptionList.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get subscriptions");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    // Helper methods for error mapping
    private bool IsParameterError(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return false;

        var lowerError = errorMessage.ToLowerInvariant();
        
        // Check for specific parameter-related error patterns
        return lowerError.Contains("parameter") || 
               lowerError.Contains("param") ||
               lowerError.Contains("invalid request") || 
               lowerError.Contains("bad request") ||
               lowerError.Contains("missing required") ||
               lowerError.Contains("must be provided") ||
               (lowerError.Contains("invalid") && (lowerError.Contains("orderid") || lowerError.Contains("clientorderid") || lowerError.Contains("symbol")));
    }

    private string SanitizeErrorMessage(string errorMessage)
    {
        // Return a clean, user-friendly error message
        if (string.IsNullOrEmpty(errorMessage))
            return "Invalid request parameters";

        // Strip out any internal details and keep it simple
        return errorMessage.Length > 200 
            ? errorMessage.Substring(0, 200) + "..." 
            : errorMessage;
    }
}
