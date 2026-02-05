using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using BitgetLab.Core.Services.Bitget;
using BitgetLab.Core.Services.Backtest;
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
    private readonly ICandleRepository? _candleRepository;
    private readonly IBacktestService? _backtestService;
    private readonly IFuturesSymbolPipelineManager? _pipelineManager;
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
        ILogger<BitgetController> logger,
        ICandleRepository? candleRepository = null,
        IBacktestService? backtestService = null,
        IFuturesSymbolPipelineManager? pipelineManager = null)
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
        _candleRepository = candleRepository;
        _backtestService = backtestService;
        _pipelineManager = pipelineManager;
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
        [FromQuery] string? market = null,
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
            // Parse market type (defaults to spot if not provided)
            var marketType = MarketTypeExtensions.ParseMarketType(market);
            
            var candles = await _candleService.GetCandlesAsync(
                symbol, interval, startTime, endTime, limit, marketType, cancellationToken);
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

    [HttpGet("market/candles/stats")]
    public async Task<IActionResult> GetCandleStats(
        [FromQuery] string symbol,
        [FromQuery] string interval,
        [FromQuery] string? market = null,
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
            // Parse market type (defaults to spot if not provided)
            var marketType = MarketTypeExtensions.ParseMarketType(market);
            
            if (_candleRepository == null)
            {
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        dbEnabled = false,
                        dbAvailable = false,
                        count = 0,
                        minOpenTime = (DateTime?)null,
                        maxOpenTime = (DateTime?)null,
                        lastUpdatedAt = (DateTime?)null
                    },
                    count = 0
                });
            }

            var stats = await _candleRepository.GetStatsAsync(symbol, interval, marketType, cancellationToken);
            
            return Ok(new
            {
                success = true,
                data = new
                {
                    dbEnabled = stats.DbEnabled,
                    dbAvailable = stats.DbAvailable,
                    count = stats.Count,
                    minOpenTime = stats.MinOpenTime,
                    maxOpenTime = stats.MaxOpenTime,
                    lastUpdatedAt = stats.LastUpdatedAt
                },
                count = stats.Count
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for getting candle stats");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get candle stats for {Symbol} {Interval}", symbol, interval);
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpPost("market/backfill")]
    public async Task<IActionResult> BackfillCandles(
        [FromBody] CandleBackfillRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                success = false,
                error = "Request body is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Symbol))
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

        if (!request.StartTime.HasValue || !request.EndTime.HasValue)
        {
            return BadRequest(new
            {
                success = false,
                error = "StartTime and EndTime are required"
            });
        }

        if (request.StartTime >= request.EndTime)
        {
            return BadRequest(new
            {
                success = false,
                error = "StartTime must be before EndTime"
            });
        }

        try
        {
            var limit = request.Limit ?? 1000;
            if (limit > 1000)
            {
                limit = 1000;
            }

            _logger.LogInformation("Backfill requested for {Symbol} {Interval} from {Start} to {End} with limit {Limit}", 
                request.Symbol, request.Interval, request.StartTime, request.EndTime, limit);

            var candles = await _candleService.GetCandlesAsync(
                request.Symbol,
                request.Interval,
                startTime: request.StartTime,
                endTime: request.EndTime,
                limit: limit,
                cancellationToken: cancellationToken);

            var candleList = candles.ToList();
            
            return Ok(new
            {
                success = true,
                data = new
                {
                    symbol = request.Symbol,
                    interval = request.Interval,
                    startTime = request.StartTime,
                    endTime = request.EndTime,
                    candlesBackfilled = candleList.Count
                },
                count = candleList.Count
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for backfill");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error during backfill for {Symbol}", request.Symbol);
            return StatusCode(502, new
            {
                success = false,
                error = $"Failed to backfill candles for {request.Symbol} from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backfill candles for {Symbol} {Interval}", request.Symbol, request.Interval);
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpPost("market/candles/backfill")]
    public async Task<IActionResult> RangeBackfillCandles(
        [FromBody] RangeBackfillRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = System.Diagnostics.Stopwatch.StartNew();
        
        // Validate request body
        if (request == null)
        {
            return BadRequest(new
            {
                ok = false,
                error = "Request body is required"
            });
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Symbol))
        {
            return BadRequest(new
            {
                ok = false,
                error = "Symbol is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Interval))
        {
            return BadRequest(new
            {
                ok = false,
                error = "Interval is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Start))
        {
            return BadRequest(new
            {
                ok = false,
                error = "Start is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.End))
        {
            return BadRequest(new
            {
                ok = false,
                error = "End is required"
            });
        }

        // Parse ISO date strings
        DateTime startDateTime;
        DateTime endDateTime;
        
        try
        {
            startDateTime = DateTime.Parse(request.Start, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                ok = false,
                error = $"Invalid start date format: {ex.Message}"
            });
        }

        try
        {
            endDateTime = DateTime.Parse(request.End, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                ok = false,
                error = $"Invalid end date format: {ex.Message}"
            });
        }

        // Ensure start < end
        if (startDateTime >= endDateTime)
        {
            return BadRequest(new
            {
                ok = false,
                error = "Start must be before end"
            });
        }

        // Default and clamp limit
        int limit = request.Limit ?? 200;
        if (limit < 1) limit = 200;
        if (limit > 1000) limit = 1000;

        // Parse market type
        MarketType marketType;
        try
        {
            marketType = MarketTypeExtensions.ParseMarketType(request.Market ?? "spot");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                ok = false,
                error = ex.Message
            });
        }

        try
        {
            _logger.LogInformation(
                "Range backfill requested for {Symbol} {Market} {Interval} from {Start} to {End} with limit {Limit}",
                request.Symbol, request.Market, request.Interval, request.Start, request.End, limit);

            // Get existing candles from DB to track inserts vs updates
            var existingCandles = _candleRepository != null
                ? (await _candleRepository.GetCandlesAsync(
                    request.Symbol,
                    request.Interval,
                    startDateTime,
                    endDateTime,
                    limit: 100000, // Get all candles in range for comparison
                    market: marketType,
                    cancellationToken: cancellationToken)).ToList()
                : new List<CandleDto>();

            var existingTimes = new HashSet<DateTime>(existingCandles.Select(c => c.OpenTime));

            // Fetch candles from Bitget for the full range
            // The CandleService will handle pagination and fetching in batches
            var fetchedCandles = await _candleService.GetCandlesAsync(
                request.Symbol,
                request.Interval,
                startTime: startDateTime,
                endTime: endDateTime,
                limit: limit,
                market: marketType,
                cancellationToken: cancellationToken);

            var fetchedList = fetchedCandles.ToList();
            var fetchedCount = fetchedList.Count;

            // Calculate statistics
            int inserted = 0;
            int updated = 0;
            int skipped = 0;

            foreach (var candle in fetchedList)
            {
                if (existingTimes.Contains(candle.OpenTime))
                {
                    updated++;
                }
                else
                {
                    inserted++;
                }
            }

            // Estimate number of batches (approximate)
            var timeSpan = endDateTime - startDateTime;
            var intervalMinutes = ParseIntervalToMinutes(request.Interval);
            var expectedCandles = intervalMinutes > 0 ? (int)(timeSpan.TotalMinutes / intervalMinutes) : fetchedCount;
            var fetchedBatches = expectedCandles > 0 ? Math.Max(1, (expectedCandles + limit - 1) / limit) : 1;

            startTime.Stop();

            var response = new RangeBackfillResponse
            {
                Ok = true,
                Symbol = request.Symbol,
                Market = request.Market ?? "spot",
                Interval = request.Interval,
                Start = request.Start,
                End = request.End,
                FetchedBatches = fetchedBatches,
                FetchedCandles = fetchedCount,
                Inserted = inserted,
                Updated = updated,
                Skipped = skipped,
                DurationMs = startTime.ElapsedMilliseconds
            };

            _logger.LogInformation(
                "Range backfill completed for {Symbol} {Market} {Interval}: {Fetched} candles ({Inserted} inserted, {Updated} updated) in {Duration}ms",
                request.Symbol, request.Market, request.Interval, fetchedCount, inserted, updated, startTime.ElapsedMilliseconds);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for range backfill");
            return BadRequest(new
            {
                ok = false,
                error = ex.Message
            });
        }
        catch (BitgetApiException ex)
        {
            _logger.LogError(ex, "Bitget API error during range backfill for {Symbol}", request.Symbol);
            return StatusCode(502, new
            {
                ok = false,
                error = $"Failed to backfill candles for {request.Symbol} from Bitget",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform range backfill for {Symbol} {Interval}", request.Symbol, request.Interval);
            return StatusCode(500, new
            {
                ok = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    private int ParseIntervalToMinutes(string interval)
    {
        if (string.IsNullOrWhiteSpace(interval))
            return 0;

        var lower = interval.ToLower().Trim();
        
        // Extract number and unit
        var numPart = new string(lower.TakeWhile(char.IsDigit).ToArray());
        var unitPart = new string(lower.SkipWhile(char.IsDigit).ToArray());

        if (!int.TryParse(numPart, out int value))
            value = 1;

        return unitPart switch
        {
            "m" => value,
            "h" => value * 60,
            "d" => value * 1440,
            "w" => value * 10080,
            _ => 0
        };
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

    // ============================================
    // Backtest Endpoints
    // ============================================

    [HttpPost("backtests/run")]
    public async Task<IActionResult> RunBacktest([FromBody] RunBacktestRequest request, CancellationToken cancellationToken)
    {
        if (_backtestService == null)
        {
            return StatusCode(503, new
            {
                success = false,
                error = "Backtest service not available",
                message = "Backtest functionality requires database configuration"
            });
        }

        try
        {
            var backtest = await _backtestService.RunBacktestAsync(request, cancellationToken);
            
            return Ok(new
            {
                success = true,
                data = new
                {
                    backtestId = backtest.Id,
                    status = backtest.Status,
                    summary = backtest.Summary
                },
                count = 1
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid backtest request");
            return BadRequest(new
            {
                success = false,
                error = "Invalid request",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run backtest");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("backtests/{id}")]
    public async Task<IActionResult> GetBacktest(Guid id, CancellationToken cancellationToken)
    {
        if (_backtestService == null)
        {
            return StatusCode(503, new
            {
                success = false,
                error = "Backtest service not available"
            });
        }

        try
        {
            var backtest = await _backtestService.GetBacktestAsync(id, cancellationToken);
            
            if (backtest == null)
            {
                return NotFound(new
                {
                    success = false,
                    error = "Backtest not found"
                });
            }

            return Ok(new
            {
                success = true,
                data = backtest,
                count = 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backtest {BacktestId}", id);
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("backtests")]
    public async Task<IActionResult> GetBacktests(
        [FromQuery] string? symbol = null,
        [FromQuery] string? strategy = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (_backtestService == null)
        {
            return StatusCode(503, new
            {
                success = false,
                error = "Backtest service not available"
            });
        }

        try
        {
            // Cap limit to prevent excessive queries
            if (limit > 100)
            {
                limit = 100;
            }
            if (limit <= 0)
            {
                limit = 50;
            }

            var backtests = await _backtestService.GetBacktestsAsync(symbol, strategy, status, limit, cancellationToken);
            var backtestList = backtests.ToList();

            return Ok(new
            {
                success = true,
                data = backtestList,
                count = backtestList.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backtests");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpGet("backtests/{id}/trades")]
    public async Task<IActionResult> GetBacktestTrades(Guid id, CancellationToken cancellationToken)
    {
        if (_backtestService == null)
        {
            return StatusCode(503, new
            {
                success = false,
                error = "Backtest service not available"
            });
        }

        try
        {
            var trades = await _backtestService.GetBacktestTradesAsync(id, cancellationToken);
            var tradeList = trades.ToList();

            return Ok(new
            {
                success = true,
                data = tradeList,
                count = tradeList.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backtest trades for {BacktestId}", id);
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    [HttpPost("backtests/sweep")]
    public async Task<IActionResult> SweepBacktest([FromBody] SweepBacktestRequest request, CancellationToken cancellationToken)
    {
        if (_backtestService == null)
        {
            return StatusCode(503, new
            {
                success = false,
                error = "Backtest service not available",
                message = "Backtest functionality requires database configuration"
            });
        }

        try
        {
            // Validate strategy
            if (request.Strategy?.ToLowerInvariant() != "rsi")
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Invalid strategy",
                    message = "Only 'rsi' strategy is supported for parameter sweep"
                });
            }

            // Validate grid parameters exist
            if (request.Grid == null || request.Grid.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Invalid request",
                    message = "Grid parameters are required"
                });
            }

            // Extract grid parameters
            var periods = GetGridValues<int>(request.Grid, "period");
            var oversoldThresholds = GetGridValues<int>(request.Grid, "oversoldThreshold");
            var overboughtThresholds = GetGridValues<int>(request.Grid, "overboughtThreshold");

            if (periods.Count == 0 || oversoldThresholds.Count == 0 || overboughtThresholds.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Invalid request",
                    message = "Grid must contain period, oversoldThreshold, and overboughtThreshold arrays"
                });
            }

            // Generate parameter combinations with oversold < overbought constraint
            var combinations = new List<Dictionary<string, object>>();
            foreach (var period in periods)
            {
                foreach (var oversold in oversoldThresholds)
                {
                    foreach (var overbought in overboughtThresholds)
                    {
                        if (oversold < overbought)
                        {
                            combinations.Add(new Dictionary<string, object>
                            {
                                { "period", period },
                                { "oversoldThreshold", oversold },
                                { "overboughtThreshold", overbought }
                            });
                        }
                    }
                }
            }

            if (combinations.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Invalid request",
                    message = "No valid parameter combinations (oversoldThreshold must be < overboughtThreshold)"
                });
            }

            _logger.LogInformation("Starting parameter sweep with {Count} combinations", combinations.Count);

            // Execute backtests with concurrency control
            var maxConcurrency = Math.Max(1, Math.Min(request.MaxConcurrency, 10)); // Cap at 10
            var semaphore = new SemaphoreSlim(maxConcurrency);
            var results = new List<SweepResultItem>();
            var completed = 0;
            var failed = 0;
            var lockObj = new object();

            var tasks = combinations.Select(async parameters =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var backtestRequest = new RunBacktestRequest
                    {
                        Symbol = request.Symbol,
                        Interval = request.Interval,
                        StartTime = request.StartTime,
                        EndTime = request.EndTime,
                        Strategy = request.Strategy,
                        Parameters = parameters,
                        FeeBps = request.FeeBps,
                        SlippageBps = request.SlippageBps,
                        InitialBalance = request.InitialBalance,
                        Market = request.Market
                    };

                    var backtest = await _backtestService.RunBacktestAsync(backtestRequest, cancellationToken);
                    
                    lock (lockObj)
                    {
                        if (backtest.Status == BacktestStatus.Completed && backtest.Summary != null)
                        {
                            results.Add(new SweepResultItem
                            {
                                BacktestId = backtest.Id,
                                Parameters = parameters,
                                Summary = backtest.Summary
                            });
                            completed++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Backtest failed for parameters: {@Parameters}", parameters);
                    lock (lockObj)
                    {
                        failed++;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Parameter sweep completed: {Completed} completed, {Failed} failed", completed, failed);

            // Sort results by the specified metric
            var sortedResults = request.SortBy?.ToLowerInvariant() switch
            {
                "winrate" => results.OrderByDescending(r => r.Summary.WinRate).ToList(),
                "returnpercent" => results.OrderByDescending(r => r.Summary.ReturnPercent).ToList(),
                "maxdrawdown" => results.OrderBy(r => r.Summary.MaxDrawdown).ToList(),
                "totaltrades" => results.OrderByDescending(r => r.Summary.TotalTrades).ToList(),
                _ => results.OrderByDescending(r => r.Summary.NetPnl).ToList()
            };

            // Take top N results
            var topResults = sortedResults.Take(Math.Max(1, request.TopN)).ToList();

            return Ok(new
            {
                success = true,
                data = topResults,
                count = topResults.Count,
                meta = new
                {
                    totalCombinations = combinations.Count,
                    completed = completed,
                    failed = failed
                }
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid sweep backtest request");
            return BadRequest(new
            {
                success = false,
                error = "Invalid request",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run sweep backtest");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    private List<T> GetGridValues<T>(Dictionary<string, List<object>> grid, string key)
    {
        if (!grid.TryGetValue(key, out var values))
        {
            return new List<T>();
        }

        var result = new List<T>();
        foreach (var value in values)
        {
            try
            {
                if (value is T typedValue)
                {
                    result.Add(typedValue);
                }
                else
                {
                    var converted = Convert.ChangeType(value, typeof(T));
                    if (converted is T convertedValue)
                    {
                        result.Add(convertedValue);
                    }
                }
            }
            catch
            {
                // Skip invalid values
            }
        }
        return result;
    }

    // ===============================
    // Pipeline Management Endpoints
    // ===============================

    /// <summary>
    /// Start a data pipeline for a symbol
    /// </summary>
    [HttpPost("pipeline/start")]
    public async Task<IActionResult> StartPipeline(
        [FromQuery, Required] string symbol,
        [FromQuery] string market = "futures",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new { error = "Symbol is required" });
        }

        if (_pipelineManager == null)
        {
            return BadRequest(new { error = "Pipeline manager not available" });
        }

        try
        {
            var status = await _pipelineManager.StartAsync(symbol, market, cancellationToken);
            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start pipeline for {Symbol} {Market}", symbol, market);
            return StatusCode(500, new { error = "Failed to start pipeline" });
        }
    }

    /// <summary>
    /// Stop a data pipeline for a symbol
    /// </summary>
    [HttpPost("pipeline/stop")]
    public async Task<IActionResult> StopPipeline(
        [FromQuery, Required] string symbol,
        [FromQuery] string market = "futures")
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new { error = "Symbol is required" });
        }

        if (_pipelineManager == null)
        {
            return BadRequest(new { error = "Pipeline manager not available" });
        }

        try
        {
            var status = await _pipelineManager.StopAsync(symbol, market);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop pipeline for {Symbol} {Market}", symbol, market);
            return StatusCode(500, new { error = "Failed to stop pipeline" });
        }
    }

    /// <summary>
    /// Get status of a data pipeline for a symbol
    /// </summary>
    [HttpGet("pipeline/status")]
    public IActionResult GetPipelineStatus(
        [FromQuery, Required] string symbol,
        [FromQuery] string market = "futures")
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new { error = "Symbol is required" });
        }

        if (_pipelineManager == null)
        {
            return BadRequest(new { error = "Pipeline manager not available" });
        }

        try
        {
            var status = _pipelineManager.GetStatus(symbol, market);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pipeline status for {Symbol} {Market}", symbol, market);
            return StatusCode(500, new { error = "Failed to get pipeline status" });
        }
    }
}
