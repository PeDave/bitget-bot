using BitgetLab.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// PostgreSQL implementation of candle repository using Npgsql and raw SQL
/// </summary>
public class PostgresCandleRepository : ICandleRepository
{
    private readonly string? _connectionString;
    private readonly ILogger<PostgresCandleRepository> _logger;

    public PostgresCandleRepository(
        IConfiguration configuration,
        ILogger<PostgresCandleRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("PostgreSQL");
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogWarning("PostgreSQL connection string not configured");
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return false;
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to PostgreSQL");
            return false;
        }
    }

    public async Task<IEnumerable<CandleDto>> GetCandlesAsync(
        string symbol,
        string interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return Enumerable.Empty<CandleDto>();
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT symbol, interval, open_time, open, high, low, close, volume, quote_volume
                FROM candles
                WHERE symbol = @symbol AND interval = @interval";

            var parameters = new List<NpgsqlParameter>
            {
                new("@symbol", symbol),
                new("@interval", interval)
            };

            if (startTime.HasValue)
            {
                sql += " AND open_time >= @startTime";
                parameters.Add(new NpgsqlParameter("@startTime", startTime.Value));
            }

            if (endTime.HasValue)
            {
                sql += " AND open_time <= @endTime";
                parameters.Add(new NpgsqlParameter("@endTime", endTime.Value));
            }

            sql += " ORDER BY open_time ASC LIMIT @limit";
            parameters.Add(new NpgsqlParameter("@limit", limit));

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddRange(parameters.ToArray());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var candles = new List<CandleDto>();

            while (await reader.ReadAsync(cancellationToken))
            {
                candles.Add(new CandleDto
                {
                    OpenTime = reader.GetDateTime(2),
                    Open = reader.GetDecimal(3),
                    High = reader.GetDecimal(4),
                    Low = reader.GetDecimal(5),
                    Close = reader.GetDecimal(6),
                    Volume = reader.GetDecimal(7),
                    QuoteVolume = reader.GetDecimal(8)
                });
            }

            return candles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get candles from database for {Symbol} {Interval}", symbol, interval);
            return Enumerable.Empty<CandleDto>();
        }
    }

    public async Task UpsertCandleAsync(
        string symbol,
        string interval,
        CandleDto candle,
        CancellationToken cancellationToken = default)
    {
        await UpsertCandlesAsync(symbol, interval, new[] { candle }, cancellationToken);
    }

    public async Task UpsertCandlesAsync(
        string symbol,
        string interval,
        IEnumerable<CandleDto> candles,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        var candleList = candles.ToList();
        if (candleList.Count == 0)
        {
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Use UPSERT (INSERT ... ON CONFLICT DO UPDATE)
            var sql = @"
                INSERT INTO candles (symbol, interval, open_time, open, high, low, close, volume, quote_volume, updated_at)
                VALUES (@symbol, @interval, @openTime, @open, @high, @low, @close, @volume, @quoteVolume, @updatedAt)
                ON CONFLICT (symbol, interval, open_time)
                DO UPDATE SET
                    open = EXCLUDED.open,
                    high = EXCLUDED.high,
                    low = EXCLUDED.low,
                    close = EXCLUDED.close,
                    volume = EXCLUDED.volume,
                    quote_volume = EXCLUDED.quote_volume,
                    updated_at = EXCLUDED.updated_at";

            foreach (var candle in candleList)
            {
                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@symbol", symbol);
                command.Parameters.AddWithValue("@interval", interval);
                command.Parameters.AddWithValue("@openTime", candle.OpenTime);
                command.Parameters.AddWithValue("@open", candle.Open);
                command.Parameters.AddWithValue("@high", candle.High);
                command.Parameters.AddWithValue("@low", candle.Low);
                command.Parameters.AddWithValue("@close", candle.Close);
                command.Parameters.AddWithValue("@volume", candle.Volume);
                command.Parameters.AddWithValue("@quoteVolume", candle.QuoteVolume);
                command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            _logger.LogDebug("Upserted {Count} candles for {Symbol} {Interval}", candleList.Count, symbol, interval);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert candles to database for {Symbol} {Interval}", symbol, interval);
        }
    }
}
