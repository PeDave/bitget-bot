-- Migration: Add market_type column to candles table
-- This migration adds market_type to distinguish between spot and futures candles
-- Default value is 'spot' for backward compatibility with existing data

-- Step 1: Add market_type column with default value
ALTER TABLE candles 
ADD COLUMN IF NOT EXISTS market_type VARCHAR(20) NOT NULL DEFAULT 'spot';

-- Step 2: Drop existing primary key constraint
ALTER TABLE candles DROP CONSTRAINT IF EXISTS pk_candles;

-- Step 3: Add new primary key constraint with market_type
ALTER TABLE candles 
ADD CONSTRAINT pk_candles PRIMARY KEY (symbol, interval, open_time, market_type);

-- Step 4: Drop old index and create new one with market_type
DROP INDEX IF EXISTS idx_candles_symbol_interval_time;
CREATE INDEX idx_candles_symbol_interval_time_market 
ON candles (symbol, interval, market_type, open_time DESC);

-- Step 5: Add comment for documentation
COMMENT ON COLUMN candles.market_type IS 'Market type: spot or futures';
