-- Create backtest tables for backtesting functionality
-- This migration adds tables to store backtest runs and their simulated trades

-- Backtests table - stores backtest configuration and results
CREATE TABLE IF NOT EXISTS backtests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    symbol VARCHAR(50) NOT NULL,
    interval VARCHAR(10) NOT NULL,
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP NOT NULL,
    strategy VARCHAR(50) NOT NULL,
    parameters JSONB NOT NULL DEFAULT '{}'::jsonb,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    summary JSONB,
    error TEXT
);

-- Index for querying backtests by symbol and strategy
CREATE INDEX IF NOT EXISTS idx_backtests_symbol_strategy 
ON backtests (symbol, strategy, created_at DESC);

-- Index for querying backtests by status
CREATE INDEX IF NOT EXISTS idx_backtests_status 
ON backtests (status, created_at DESC);

-- Index for querying backtests by creation date
CREATE INDEX IF NOT EXISTS idx_backtests_created_at 
ON backtests (created_at DESC);

-- Backtest trades table - stores individual trades from backtest simulations
CREATE TABLE IF NOT EXISTS backtest_trades (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    backtest_id UUID NOT NULL,
    entry_time TIMESTAMP NOT NULL,
    exit_time TIMESTAMP,
    side VARCHAR(10) NOT NULL,
    entry_price NUMERIC(20, 8) NOT NULL,
    exit_price NUMERIC(20, 8),
    qty NUMERIC(20, 8) NOT NULL,
    pnl NUMERIC(20, 8),
    metadata JSONB DEFAULT '{}'::jsonb,
    
    -- Foreign key constraint
    CONSTRAINT fk_backtest_trades_backtest 
        FOREIGN KEY (backtest_id) 
        REFERENCES backtests(id) 
        ON DELETE CASCADE
);

-- Index for querying trades by backtest_id
CREATE INDEX IF NOT EXISTS idx_backtest_trades_backtest_id 
ON backtest_trades (backtest_id, entry_time);

-- Index for querying trades by time
CREATE INDEX IF NOT EXISTS idx_backtest_trades_time 
ON backtest_trades (entry_time, exit_time);

-- Comments for documentation
COMMENT ON TABLE backtests IS 'Stores backtest configurations and results for strategy testing';
COMMENT ON COLUMN backtests.id IS 'Unique identifier for the backtest run';
COMMENT ON COLUMN backtests.created_at IS 'When the backtest was created';
COMMENT ON COLUMN backtests.symbol IS 'Trading pair symbol (e.g., BTCUSDT)';
COMMENT ON COLUMN backtests.interval IS 'Candle interval used (e.g., 1m, 5m, 1h, 1d)';
COMMENT ON COLUMN backtests.start_time IS 'Start time of the backtesting period';
COMMENT ON COLUMN backtests.end_time IS 'End time of the backtesting period';
COMMENT ON COLUMN backtests.strategy IS 'Strategy name (e.g., ema_cross, rsi)';
COMMENT ON COLUMN backtests.parameters IS 'Strategy parameters as JSON (e.g., {"fastPeriod": 10, "slowPeriod": 20})';
COMMENT ON COLUMN backtests.status IS 'Backtest status: pending, running, completed, failed';
COMMENT ON COLUMN backtests.summary IS 'Backtest results summary as JSON (total trades, win rate, PnL, drawdown, equity curve)';
COMMENT ON COLUMN backtests.error IS 'Error message if backtest failed';

COMMENT ON TABLE backtest_trades IS 'Individual simulated trades from backtest runs';
COMMENT ON COLUMN backtest_trades.id IS 'Unique identifier for the trade';
COMMENT ON COLUMN backtest_trades.backtest_id IS 'Reference to the parent backtest';
COMMENT ON COLUMN backtest_trades.entry_time IS 'Time when the position was opened';
COMMENT ON COLUMN backtest_trades.exit_time IS 'Time when the position was closed';
COMMENT ON COLUMN backtest_trades.side IS 'Trade direction: long or short';
COMMENT ON COLUMN backtest_trades.entry_price IS 'Price at which position was opened';
COMMENT ON COLUMN backtest_trades.exit_price IS 'Price at which position was closed';
COMMENT ON COLUMN backtest_trades.qty IS 'Position size/quantity';
COMMENT ON COLUMN backtest_trades.pnl IS 'Profit/loss for this trade';
COMMENT ON COLUMN backtest_trades.metadata IS 'Additional trade metadata as JSON';
