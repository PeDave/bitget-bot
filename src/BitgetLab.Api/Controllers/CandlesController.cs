using Microsoft.AspNetCore.Mvc;
using BitgetLab.Core.Services.Bitget;
using BitgetLab.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace BitgetLab.Api.Controllers;

/// <summary>
/// Controller for candle range queries with warmup support
/// </summary>
[ApiController]
[Route("api/candles")]
public class CandlesController : ControllerBase
{
    private readonly ICandleRepository? _candleRepository;
    private readonly ILogger<CandlesController> _logger;

    public CandlesController(
        ILogger<CandlesController> logger,
        ICandleRepository? candleRepository = null)
    {
        _logger = logger;
        _candleRepository = candleRepository;
    }

    /// <summary>
    /// Get candles by symbol, market, interval and time range with optional warmup candles
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g., BTCUSDT)</param>
    /// <param name="market">Market type (spot or futures), defaults to spot</param>
    /// <param name="interval">Candle interval (e.g., 1m, 5m, 1h, 1d)</param>
    /// <param name="start">Start time (ISO 8601 format)</param>
    /// <param name="end">End time (ISO 8601 format)</param>
    /// <param name="warmupCandles">Number of warmup candles to include before start time, defaults to 200</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Candle range response with metadata and candles array</returns>
    [HttpGet]
    public async Task<IActionResult> GetCandlesRange(
        [FromQuery, Required] string symbol,
        [FromQuery] string? market = null,
        [FromQuery, Required] string interval = null!,
        [FromQuery, Required] string start = null!,
        [FromQuery, Required] string end = null!,
        [FromQuery] int warmupCandles = 200,
        CancellationToken cancellationToken = default)
    {
        // Validate required parameters
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

        if (string.IsNullOrWhiteSpace(start))
        {
            return BadRequest(new
            {
                success = false,
                error = "Start parameter is required"
            });
        }

        if (string.IsNullOrWhiteSpace(end))
        {
            return BadRequest(new
            {
                success = false,
                error = "End parameter is required"
            });
        }

        // Parse market type
        MarketType marketType;
        try
        {
            marketType = MarketTypeExtensions.ParseMarketType(market);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }

        // Parse start and end times
        DateTime startTime, endTime;
        try
        {
            startTime = DateTime.Parse(start).ToUniversalTime();
            endTime = DateTime.Parse(end).ToUniversalTime();
        }
        catch (FormatException)
        {
            return BadRequest(new
            {
                success = false,
                error = "Invalid date format. Use ISO 8601 format (e.g., 2024-01-01T00:00:00Z)"
            });
        }

        // Validate time range
        if (endTime < startTime)
        {
            return BadRequest(new
            {
                success = false,
                error = "End time must be greater than or equal to start time"
            });
        }

        // Validate warmup candles
        if (warmupCandles < 0)
        {
            return BadRequest(new
            {
                success = false,
                error = "WarmupCandles must be non-negative"
            });
        }

        // Check if repository is available
        if (_candleRepository == null)
        {
            return StatusCode(503, new
            {
                success = false,
                error = "Candle repository is not configured"
            });
        }

        try
        {
            var candles = await _candleRepository.GetCandlesWithWarmupAsync(
                symbol,
                interval,
                startTime,
                endTime,
                warmupCandles,
                marketType,
                cancellationToken);

            var candleList = candles.ToList();

            var response = new CandleRangeResponse
            {
                Symbol = symbol,
                Market = marketType.ToStringValue(),
                Interval = interval,
                Start = startTime,
                End = endTime,
                WarmupCandles = warmupCandles,
                Count = candleList.Count,
                Candles = candleList
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get candles for {Symbol} {Interval} {Market} from {Start} to {End}",
                symbol, interval, marketType.ToStringValue(), startTime, endTime);
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error",
                message = ex.Message
            });
        }
    }
}
