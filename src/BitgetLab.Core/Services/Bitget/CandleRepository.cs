using BitgetLab.Core.Models;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Repository interface for candle data persistence
/// </summary>
public interface ICandleRepository
{
    /// <summary>
    /// Upsert a single candle
    /// </summary>
    Task UpsertCandleAsync(string symbol, string interval, CandleDto candle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert multiple candles
    /// </summary>
    Task UpsertCandlesAsync(string symbol, string interval, IEnumerable<CandleDto> candles, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get candles from the database
    /// </summary>
    Task<IEnumerable<CandleDto>> GetCandlesAsync(
        string symbol, 
        string interval, 
        DateTime? startTime = null, 
        DateTime? endTime = null, 
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if database connection is configured and available
    /// </summary>
    bool IsAvailable { get; }
}

/// <summary>
/// PostgreSQL-based implementation of candle repository
/// </summary>
public class CandleRepository : ICandleRepository
{
    private readonly string? _connectionString;
    private readonly ILogger<CandleRepository> _logger;

    public CandleRepository(IConfiguration configuration, ILogger<CandleRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("Postgres") 
                         ?? configuration.GetConnectionString("PostgreSQL");
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogWarning("Postgres connection string not configured. Database persistence will be unavailable.");
        }
        else
        {
            _logger.LogInformation("Postgres connection string configured. Database persistence is available.");
        }
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task UpsertCandleAsync(string symbol, string interval, CandleDto candle, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogDebug("Database not available, skipping upsert");
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                INSERT INTO candles (symbol, interval, open_time, open, high, low, close, volume, quote_volume, updated_at)
                VALUES (@symbol, @interval, @open_time, @open, @high, @low, @close, @volume, @quote_volume, @updated_at)
                ON CONFLICT (symbol, interval, open_time) 
                DO UPDATE SET 
                    open = EXCLUDED.open,
                    high = EXCLUDED.high,
                    low = EXCLUDED.low,
                    close = EXCLUDED.close,
                    volume = EXCLUDED.volume,
                    quote_volume = EXCLUDED.quote_volume,
                    updated_at = EXCLUDED.updated_at";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("symbol", symbol.ToUpperInvariant());
            cmd.Parameters.AddWithValue("interval", interval.ToLowerInvariant());
            cmd.Parameters.AddWithValue("open_time", candle.OpenTime);
            cmd.Parameters.AddWithValue("open", candle.Open);
            cmd.Parameters.AddWithValue("high", candle.High);
            cmd.Parameters.AddWithValue("low", candle.Low);
            cmd.Parameters.AddWithValue("close", candle.Close);
            cmd.Parameters.AddWithValue("volume", candle.Volume);
            cmd.Parameters.AddWithValue("quote_volume", candle.QuoteVolume);
            cmd.Parameters.AddWithValue("updated_at", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting candle for {Symbol} {Interval} at {OpenTime}", 
                symbol, interval, candle.OpenTime);
            // Don't rethrow - we don't want DB errors to break the websocket flow
        }
    }

    public async Task UpsertCandlesAsync(string symbol, string interval, IEnumerable<CandleDto> candles, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogDebug("Database not available, skipping batch upsert");
            return;
        }

        var candlesList = candles.ToList();
        if (candlesList.Count == 0)
        {
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Use a transaction for batch insert
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            const string sql = @"
                INSERT INTO candles (symbol, interval, open_time, open, high, low, close, volume, quote_volume, updated_at)
                VALUES (@symbol, @interval, @open_time, @open, @high, @low, @close, @volume, @quote_volume, @updated_at)
                ON CONFLICT (symbol, interval, open_time) 
                DO UPDATE SET 
                    open = EXCLUDED.open,
                    high = EXCLUDED.high,
                    low = EXCLUDED.low,
                    close = EXCLUDED.close,
                    volume = EXCLUDED.volume,
                    quote_volume = EXCLUDED.quote_volume,
                    updated_at = EXCLUDED.updated_at";

            foreach (var candle in candlesList)
            {
                await using var cmd = new NpgsqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("symbol", symbol.ToUpperInvariant());
                cmd.Parameters.AddWithValue("interval", interval.ToLowerInvariant());
                cmd.Parameters.AddWithValue("open_time", candle.OpenTime);
                cmd.Parameters.AddWithValue("open", candle.Open);
                cmd.Parameters.AddWithValue("high", candle.High);
                cmd.Parameters.AddWithValue("low", candle.Low);
                cmd.Parameters.AddWithValue("close", candle.Close);
                cmd.Parameters.AddWithValue("volume", candle.Volume);
                cmd.Parameters.AddWithValue("quote_volume", candle.QuoteVolume);
                cmd.Parameters.AddWithValue("updated_at", DateTime.UtcNow);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            
            _logger.LogDebug("Upserted {Count} candles for {Symbol} {Interval}", 
                candlesList.Count, symbol, interval);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting {Count} candles for {Symbol} {Interval}", 
                candlesList.Count, symbol, interval);
            // Don't rethrow - we don't want DB errors to break the websocket flow
        }
    }

    public async Task<IEnumerable<CandleDto>> GetCandlesAsync(
        string symbol, 
        string interval, 
        DateTime? startTime = null, 
        DateTime? endTime = null, 
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return Enumerable.Empty<CandleDto>();
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = "SELECT open_time, open, high, low, close, volume, quote_volume FROM candles WHERE symbol = @symbol AND interval = @interval";
            var parameters = new List<NpgsqlParameter>
            {
                new("symbol", symbol.ToUpperInvariant()),
                new("interval", interval.ToLowerInvariant())
            };

            if (startTime.HasValue)
            {
                sql += " AND open_time >= @start_time";
                parameters.Add(new NpgsqlParameter("start_time", startTime.Value));
            }

            if (endTime.HasValue)
            {
                sql += " AND open_time <= @end_time";
                parameters.Add(new NpgsqlParameter("end_time", endTime.Value));
            }

            sql += " ORDER BY open_time DESC";

            if (limit.HasValue && limit.Value > 0)
            {
                sql += " LIMIT @limit";
                parameters.Add(new NpgsqlParameter("limit", limit.Value));
            }

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddRange(parameters.ToArray());

            var candles = new List<CandleDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            
            while (await reader.ReadAsync(cancellationToken))
            {
                candles.Add(new CandleDto
                {
                    OpenTime = reader.GetDateTime(0),
                    Open = reader.GetDecimal(1),
                    High = reader.GetDecimal(2),
                    Low = reader.GetDecimal(3),
                    Close = reader.GetDecimal(4),
                    Volume = reader.GetDecimal(5),
                    QuoteVolume = reader.GetDecimal(6)
                });
            }

            // Reverse to get chronological order
            candles.Reverse();
            return candles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting candles from database for {Symbol} {Interval}", symbol, interval);
            return Enumerable.Empty<CandleDto>();
        }
    }
}
