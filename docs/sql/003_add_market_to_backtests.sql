-- Migration: Add market column to backtests table
-- This migration adds market field to distinguish between spot and futures backtests
-- Default value is 'spot' for backward compatibility with existing data

-- Add market column with default value
ALTER TABLE backtests 
ADD COLUMN IF NOT EXISTS market VARCHAR(20) NOT NULL DEFAULT 'spot';

-- Add comment for documentation
COMMENT ON COLUMN backtests.market IS 'Market type: spot or futures';

-- Update index to include market for better query performance
CREATE INDEX IF NOT EXISTS idx_backtests_symbol_strategy_market 
ON backtests (symbol, strategy, market, created_at DESC);
