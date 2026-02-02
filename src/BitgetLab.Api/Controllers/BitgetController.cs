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
    private readonly ILogger<BitgetController> _logger;
    
    public BitgetController(
        IMarketDataService marketDataService,
        ITradingService tradingService,
        ILogger<BitgetController> logger)
    {
        _marketDataService = marketDataService;
        _tradingService = tradingService;
        _logger = logger;
    }
    
    [HttpGet("symbols")]
    public async Task<IActionResult> GetSymbols(CancellationToken cancellationToken)
    {
        try
        {
            var symbols = await _marketDataService.GetSymbolsAsync(cancellationToken);
            return Ok(new { symbols = symbols.ToList() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting symbols");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("market/ticker")]
    public async Task<IActionResult> GetTicker([FromQuery] string symbol, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new { error = "Symbol parameter is required" });
        }

        try
        {
            var ticker = await _marketDataService.GetTickerAsync(symbol, cancellationToken);
            return Ok(ticker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ticker for {Symbol}", symbol);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("orders")]
    public async Task<IActionResult> PlaceOrder([FromBody] OrderRequest orderRequest, CancellationToken cancellationToken)
    {
        if (orderRequest == null)
        {
            return BadRequest(new { error = "Order request is required" });
        }

        try
        {
            var result = await _tradingService.PlaceOrderAsync(orderRequest, cancellationToken);
            
            if (result.Status == "Failed")
            {
                return BadRequest(new { error = result.ErrorMessage ?? "Order placement failed" });
            }
            
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ReadOnly mode"))
        {
            // Return 403 Forbidden for ReadOnly mode violation
            return StatusCode(403, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing order");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
