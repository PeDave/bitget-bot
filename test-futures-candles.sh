#!/bin/bash

# Verification script for futures candle support
# This script tests the API endpoints for spot and futures markets

BASE_URL="${API_URL:-http://localhost:3001}"

echo "=== Futures Candle Support Verification ==="
echo ""
echo "Base URL: $BASE_URL"
echo ""

# Test 1: Spot candles (default)
echo "Test 1: Fetching spot candles (default)..."
curl -s -w "\nHTTP Status: %{http_code}\n" \
  "$BASE_URL/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&limit=5" \
  | head -20
echo ""

# Test 2: Spot candles (explicit)
echo "Test 2: Fetching spot candles (explicit market=spot)..."
curl -s -w "\nHTTP Status: %{http_code}\n" \
  "$BASE_URL/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&limit=5&market=spot" \
  | head -20
echo ""

# Test 3: Futures candles
echo "Test 3: Fetching futures candles (market=futures)..."
curl -s -w "\nHTTP Status: %{http_code}\n" \
  "$BASE_URL/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&limit=5&market=futures" \
  | head -20
echo ""

# Test 3.5: Futures candles with date range (the fix for this issue)
echo "Test 3.5: Fetching futures candles with date range (startTime + endTime)..."
curl -s -w "\nHTTP Status: %{http_code}\n" \
  "$BASE_URL/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&market=futures&startTime=2025-11-01T00:00:00Z&endTime=2026-02-01T00:00:00Z&limit=1000" \
  | head -20
echo ""

# Test 4: Invalid market type
echo "Test 4: Testing invalid market type..."
curl -s -w "\nHTTP Status: %{http_code}\n" \
  "$BASE_URL/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&limit=5&market=invalid" \
  | head -20
echo ""

# Test 5: Spot candle stats
echo "Test 5: Fetching spot candle stats..."
curl -s -w "\nHTTP Status: %{http_code}\n" \
  "$BASE_URL/api/bitget/market/candles/stats?symbol=BTCUSDT&interval=1h&market=spot" \
  | head -20
echo ""

# Test 6: Futures candle stats
echo "Test 6: Fetching futures candle stats..."
curl -s -w "\nHTTP Status: %{http_code}\n" \
  "$BASE_URL/api/bitget/market/candles/stats?symbol=BTCUSDT&interval=1h&market=futures" \
  | head -20
echo ""

# Test 7: Spot backtest
echo "Test 7: Running spot backtest..."
curl -s -w "\nHTTP Status: %{http_code}\n" \
  -X POST "$BASE_URL/api/bitget/backtests/run" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-07T23:59:59Z",
    "strategy": "ema_cross",
    "parameters": {"fastPeriod": 10, "slowPeriod": 20},
    "market": "spot"
  }' \
  | head -20
echo ""

# Test 8: Futures backtest
echo "Test 8: Running futures backtest..."
curl -s -w "\nHTTP Status: %{http_code}\n" \
  -X POST "$BASE_URL/api/bitget/backtests/run" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-07T23:59:59Z",
    "strategy": "ema_cross",
    "parameters": {"fastPeriod": 10, "slowPeriod": 20},
    "market": "futures"
  }' \
  | head -20
echo ""

echo "=== Verification Complete ==="
