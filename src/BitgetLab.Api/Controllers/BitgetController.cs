using Microsoft.AspNetCore.Mvc;

namespace BitgetLab.Api.Controllers;

[ApiController]
[Route("api/bitget")]
public class BitgetController : ControllerBase
{
    // TODO: Inject MarketDataService and TradingService when Bitget.Net is vendored
    
    [HttpGet("symbols")]
    public IActionResult GetSymbols()
    {
        // TODO: Implement with Bitget.Net SDK
        return Ok(new
        {
            message = "TODO: Implement with Bitget.Net SDK",
            symbols = new[] { "BTCUSDT", "ETHUSDT" }
        });
    }

    [HttpGet("market/ticker")]
    public IActionResult GetTicker([FromQuery] string symbol)
    {
        // TODO: Implement with Bitget.Net SDK
        return Ok(new
        {
            message = "TODO: Implement with Bitget.Net SDK",
            symbol,
            price = 0.0,
            timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("orders")]
    public IActionResult PlaceOrder([FromBody] object orderRequest)
    {
        // TODO: Implement with Bitget.Net SDK
        return Ok(new
        {
            message = "TODO: Implement with Bitget.Net SDK - check mode (ReadOnly/Trade)",
            orderId = "stub-order-123"
        });
    }
}
