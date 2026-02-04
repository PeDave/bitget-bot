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
        MarketType market = MarketType.Spot,
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
                WHERE symbol = @symbol AND interval = @interval AND market_type = @marketType";

            var parameters = new List<NpgsqlParameter>
            {
                new("@symbol", symbol),
                new("@interval", interval),
                new("@marketType", market.ToStringValue())
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
            _logger.LogError(ex, "Failed to get candles from database for {Symbol} {Interval} {Market}", symbol, interval, market.ToStringValue());
            return Enumerable.Empty<CandleDto>();
        }
    }

    public async Task UpsertCandleAsync(
        string symbol,
        string interval,
        CandleDto candle,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default)
    {
        await UpsertCandlesAsync(symbol, interval, new[] { candle }, market, cancellationToken);
    }

    public async Task UpsertCandlesAsync(
        string symbol,
        string interval,
        IEnumerable<CandleDto> candles,
        MarketType market = MarketType.Spot,
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

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                // Process in chunks to respect PostgreSQL parameter limits
                // PostgreSQL has a limit of ~65535 parameters, we use 10 params per row
                // So chunk at 1000 rows to stay well under the limit
                const int chunkSize = 1000;
                var chunks = candleList
                    .Select((candle, index) => new { candle, index })
                    .GroupBy(x => x.index / chunkSize)
                    .Select(g => g.Select(x => x.candle).ToList())
                    .ToList();

                var totalUpserted = 0;
                var updatedAt = DateTime.UtcNow;
                var marketType = market.ToStringValue();

                for (int chunkIdx = 0; chunkIdx < chunks.Count; chunkIdx++)
                {
                    var chunk = chunks[chunkIdx];
                    // Build multi-row VALUES clause
                    var valuesClauses = new List<string>();
                    var parameters = new List<NpgsqlParameter>();
                    
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var candle = chunk[i];
                        
                        valuesClauses.Add($"(@symbol, @interval, @openTime{i}, @open{i}, @high{i}, @low{i}, @close{i}, @volume{i}, @quoteVolume{i}, @updatedAt, @marketType)");
                        
                        parameters.Add(new NpgsqlParameter($"@openTime{i}", candle.OpenTime));
                        parameters.Add(new NpgsqlParameter($"@open{i}", candle.Open));
                        parameters.Add(new NpgsqlParameter($"@high{i}", candle.High));
                        parameters.Add(new NpgsqlParameter($"@low{i}", candle.Low));
                        parameters.Add(new NpgsqlParameter($"@close{i}", candle.Close));
                        parameters.Add(new NpgsqlParameter($"@volume{i}", candle.Volume));
                        parameters.Add(new NpgsqlParameter($"@quoteVolume{i}", candle.QuoteVolume));
                    }

                    var sql = $@"
                        INSERT INTO candles (symbol, interval, open_time, open, high, low, close, volume, quote_volume, updated_at, market_type)
                        VALUES {string.Join(", ", valuesClauses)}
                        ON CONFLICT (symbol, interval, open_time, market_type)
                        DO UPDATE SET
                            open = EXCLUDED.open,
                            high = EXCLUDED.high,
                            low = EXCLUDED.low,
                            close = EXCLUDED.close,
                            volume = EXCLUDED.volume,
                            quote_volume = EXCLUDED.quote_volume,
                            updated_at = EXCLUDED.updated_at";

                    await using var command = new NpgsqlCommand(sql, connection, transaction);
                    command.Parameters.AddWithValue("@symbol", symbol);
                    command.Parameters.AddWithValue("@interval", interval);
                    command.Parameters.AddWithValue("@updatedAt", updatedAt);
                    command.Parameters.AddWithValue("@marketType", marketType);
                    command.Parameters.AddRange(parameters.ToArray());

                    await command.ExecuteNonQueryAsync(cancellationToken);
                    totalUpserted += chunk.Count;
                    
                    _logger.LogTrace("Batch upserted {Count} candles for {Symbol} {Interval} {Market} (chunk {Current}/{Total})", 
                        chunk.Count, symbol, interval, marketType, chunkIdx + 1, chunks.Count);
                }

                await transaction.CommitAsync(cancellationToken);
                _logger.LogDebug("Successfully upserted {Count} candles for {Symbol} {Interval} {Market} in {Chunks} batch(es)", 
                    totalUpserted, symbol, interval, marketType, chunks.Count);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert candles to database for {Symbol} {Interval} {Market}", symbol, interval, market.ToStringValue());
        }
    }

    public async Task<CandleStatsDto> GetStatsAsync(
        string symbol,
        string interval,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default)
    {
        var stats = new CandleStatsDto
        {
            DbEnabled = !string.IsNullOrWhiteSpace(_connectionString),
            DbAvailable = false
        };

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return stats;
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            stats.DbAvailable = true;

            var sql = @"
                SELECT 
                    COUNT(*) as count,
                    MIN(open_time) as min_open_time,
                    MAX(open_time) as max_open_time,
                    MAX(updated_at) as last_updated_at
                FROM candles
                WHERE symbol = @symbol AND interval = @interval AND market_type = @marketType";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@symbol", symbol);
            command.Parameters.AddWithValue("@interval", interval);
            command.Parameters.AddWithValue("@marketType", market.ToStringValue());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            if (await reader.ReadAsync(cancellationToken))
            {
                stats.Count = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                stats.MinOpenTime = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
                stats.MaxOpenTime = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
                stats.LastUpdatedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stats from database for {Symbol} {Interval} {Market}", symbol, interval, market.ToStringValue());
            stats.DbAvailable = false;
            return stats;
        }
    }
}
