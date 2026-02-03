using System.Text.Json;
using BitgetLab.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// PostgreSQL implementation of backtest repository using Npgsql and raw SQL
/// </summary>
public class PostgresBacktestRepository : IBacktestRepository
{
    private readonly string? _connectionString;
    private readonly ILogger<PostgresBacktestRepository> _logger;

    public PostgresBacktestRepository(
        IConfiguration configuration,
        ILogger<PostgresBacktestRepository> logger)
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

    public async Task<Guid> SaveBacktestAsync(BacktestDto backtest, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("PostgreSQL connection string not configured");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            INSERT INTO backtests (id, created_at, symbol, interval, start_time, end_time, strategy, parameters, status, summary, error)
            VALUES (@id, @created_at, @symbol, @interval, @start_time, @end_time, @strategy, @parameters::jsonb, @status, @summary::jsonb, @error)
            RETURNING id";

        await using var command = new NpgsqlCommand(sql, connection);
        
        var id = backtest.Id == Guid.Empty ? Guid.NewGuid() : backtest.Id;
        
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@created_at", backtest.CreatedAt);
        command.Parameters.AddWithValue("@symbol", backtest.Symbol);
        command.Parameters.AddWithValue("@interval", backtest.Interval);
        command.Parameters.AddWithValue("@start_time", backtest.StartTime);
        command.Parameters.AddWithValue("@end_time", backtest.EndTime);
        command.Parameters.AddWithValue("@strategy", backtest.Strategy);
        command.Parameters.AddWithValue("@parameters", JsonSerializer.Serialize(backtest.Parameters));
        command.Parameters.AddWithValue("@status", backtest.Status);
        command.Parameters.AddWithValue("@summary", backtest.Summary != null ? JsonSerializer.Serialize(backtest.Summary) : DBNull.Value);
        command.Parameters.AddWithValue("@error", (object?)backtest.Error ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (Guid)result!;
    }

    public async Task UpdateBacktestAsync(Guid id, string status, BacktestSummary? summary, string? error, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("PostgreSQL connection string not configured");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            UPDATE backtests
            SET status = @status, summary = @summary::jsonb, error = @error
            WHERE id = @id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@summary", summary != null ? JsonSerializer.Serialize(summary) : DBNull.Value);
        command.Parameters.AddWithValue("@error", (object?)error ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<BacktestDto?> GetBacktestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT id, created_at, symbol, interval, start_time, end_time, strategy, parameters, status, summary, error
            FROM backtests
            WHERE id = @id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapBacktest(reader);
        }

        return null;
    }

    public async Task<IEnumerable<BacktestDto>> GetBacktestsAsync(
        string? symbol = null,
        string? strategy = null,
        string? status = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return Enumerable.Empty<BacktestDto>();
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT id, created_at, symbol, interval, start_time, end_time, strategy, parameters, status, summary, error
            FROM backtests
            WHERE 1=1";

        var parameters = new List<NpgsqlParameter>();

        if (!string.IsNullOrWhiteSpace(symbol))
        {
            sql += " AND symbol = @symbol";
            parameters.Add(new NpgsqlParameter("@symbol", symbol));
        }

        if (!string.IsNullOrWhiteSpace(strategy))
        {
            sql += " AND strategy = @strategy";
            parameters.Add(new NpgsqlParameter("@strategy", strategy));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            sql += " AND status = @status";
            parameters.Add(new NpgsqlParameter("@status", status));
        }

        sql += " ORDER BY created_at DESC LIMIT @limit";
        parameters.Add(new NpgsqlParameter("@limit", limit));

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        var backtests = new List<BacktestDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            backtests.Add(MapBacktest(reader));
        }

        return backtests;
    }

    public async Task SaveTradesAsync(IEnumerable<BacktestTradeDto> trades, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("PostgreSQL connection string not configured");
        }

        var tradeList = trades.ToList();
        if (tradeList.Count == 0)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Use a batch insert for better performance
        var sql = @"
            INSERT INTO backtest_trades (id, backtest_id, entry_time, exit_time, side, entry_price, exit_price, qty, pnl, metadata)
            VALUES (@id, @backtest_id, @entry_time, @exit_time, @side, @entry_price, @exit_price, @qty, @pnl, @metadata::jsonb)";

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var trade in tradeList)
            {
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                
                var id = trade.Id == Guid.Empty ? Guid.NewGuid() : trade.Id;
                
                command.Parameters.AddWithValue("@id", id);
                command.Parameters.AddWithValue("@backtest_id", trade.BacktestId);
                command.Parameters.AddWithValue("@entry_time", trade.EntryTime);
                command.Parameters.AddWithValue("@exit_time", trade.ExitTime.HasValue ? trade.ExitTime.Value : DBNull.Value);
                command.Parameters.AddWithValue("@side", trade.Side);
                command.Parameters.AddWithValue("@entry_price", trade.EntryPrice);
                command.Parameters.AddWithValue("@exit_price", trade.ExitPrice.HasValue ? trade.ExitPrice.Value : DBNull.Value);
                command.Parameters.AddWithValue("@qty", trade.Qty);
                command.Parameters.AddWithValue("@pnl", trade.Pnl.HasValue ? trade.Pnl.Value : DBNull.Value);
                command.Parameters.AddWithValue("@metadata", trade.Metadata != null ? JsonSerializer.Serialize(trade.Metadata) : "{}");

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IEnumerable<BacktestTradeDto>> GetTradesAsync(Guid backtestId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return Enumerable.Empty<BacktestTradeDto>();
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT id, backtest_id, entry_time, exit_time, side, entry_price, exit_price, qty, pnl, metadata
            FROM backtest_trades
            WHERE backtest_id = @backtest_id
            ORDER BY entry_time";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@backtest_id", backtestId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        var trades = new List<BacktestTradeDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            trades.Add(MapTrade(reader));
        }

        return trades;
    }

    private BacktestDto MapBacktest(NpgsqlDataReader reader)
    {
        var parametersJson = reader.GetString(7);
        var summaryJson = reader.IsDBNull(9) ? null : reader.GetString(9);

        return new BacktestDto
        {
            Id = reader.GetGuid(0),
            CreatedAt = reader.GetDateTime(1),
            Symbol = reader.GetString(2),
            Interval = reader.GetString(3),
            StartTime = reader.GetDateTime(4),
            EndTime = reader.GetDateTime(5),
            Strategy = reader.GetString(6),
            Parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson) ?? new(),
            Status = reader.GetString(8),
            Summary = summaryJson != null ? JsonSerializer.Deserialize<BacktestSummary>(summaryJson) : null,
            Error = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }

    private BacktestTradeDto MapTrade(NpgsqlDataReader reader)
    {
        var metadataJson = reader.IsDBNull(9) ? "{}" : reader.GetString(9);

        return new BacktestTradeDto
        {
            Id = reader.GetGuid(0),
            BacktestId = reader.GetGuid(1),
            EntryTime = reader.GetDateTime(2),
            ExitTime = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            Side = reader.GetString(4),
            EntryPrice = reader.GetDecimal(5),
            ExitPrice = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
            Qty = reader.GetDecimal(7),
            Pnl = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
            Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson)
        };
    }
}
