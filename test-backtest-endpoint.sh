#!/bin/bash

# Test script for the backtest API endpoint (issue #44)
# This script tests the new /api/backtest/run endpoint

set -e  # Exit on any error

API_URL="${API_URL:-http://localhost:3001}"
API_KEY="${BACKTEST_API_KEY:-}"

echo "===================================="
echo "Backtest API Endpoint Test"
echo "===================================="
echo ""
echo "API URL: $API_URL"
echo "API Key: ${API_KEY:+<set>}"
echo ""

# Test 1: Basic backtest request without auth
echo "Test 1: Basic backtest request (RSI mean-reversion)"
echo "---------------------------------------------------"

HEADERS="-H 'Content-Type: application/json'"
if [ -n "$API_KEY" ]; then
    HEADERS="$HEADERS -H 'X-Api-Key: $API_KEY'"
    echo "Using API key authentication"
fi

REQUEST_BODY='{
  "symbol": "BTCUSDT",
  "market": "spot",
  "interval": "1h",
  "start": "2024-01-01T00:00:00Z",
  "end": "2024-01-07T23:59:59Z",
  "strategy": {
    "type": "rsi-reversion",
    "rsiPeriod": 14,
    "entryBelow": 30,
    "exitAbove": 50
  },
  "feesBps": 10,
  "slippageBps": 0,
  "initialQuote": 1000
}'

echo ""
echo "Request:"
echo "$REQUEST_BODY" | jq .
echo ""

RESPONSE=$(curl -s -X POST "$API_URL/api/backtest/run" \
    -H "Content-Type: application/json" \
    ${API_KEY:+-H "X-Api-Key: $API_KEY"} \
    -d "$REQUEST_BODY")

echo "Response:"
echo "$RESPONSE" | jq .
echo ""

# Check if response has expected fields
if echo "$RESPONSE" | jq -e '.summary' > /dev/null 2>&1; then
    echo "✓ Response contains summary"
    
    # Extract metrics
    TRADE_COUNT=$(echo "$RESPONSE" | jq -r '.summary.tradeCount')
    TOTAL_PNL=$(echo "$RESPONSE" | jq -r '.summary.totalPnL')
    WIN_RATE=$(echo "$RESPONSE" | jq -r '.summary.winRate')
    PROFIT_FACTOR=$(echo "$RESPONSE" | jq -r '.summary.profitFactor')
    
    echo "  - Trade Count: $TRADE_COUNT"
    echo "  - Total PnL: $TOTAL_PNL"
    echo "  - Win Rate: $WIN_RATE%"
    echo "  - Profit Factor: $PROFIT_FACTOR"
else
    echo "✗ Response missing summary field"
    echo "  This might be an error response or the database has no candle data"
    
    # Check for error field
    if echo "$RESPONSE" | jq -e '.error' > /dev/null 2>&1; then
        ERROR_MSG=$(echo "$RESPONSE" | jq -r '.error')
        ERROR_DETAIL=$(echo "$RESPONSE" | jq -r '.message // empty')
        echo "  Error: $ERROR_MSG"
        if [ -n "$ERROR_DETAIL" ]; then
            echo "  Detail: $ERROR_DETAIL"
        fi
    fi
fi

if echo "$RESPONSE" | jq -e '.trades' > /dev/null 2>&1; then
    echo "✓ Response contains trades array"
fi

if echo "$RESPONSE" | jq -e '.equityCurve' > /dev/null 2>&1; then
    echo "✓ Response contains equityCurve array"
fi

if echo "$RESPONSE" | jq -e '.parameters' > /dev/null 2>&1; then
    echo "✓ Response contains parameters (echo back)"
fi

echo ""

# Test 2: Invalid request (missing required fields)
echo "Test 2: Invalid request (should return 400)"
echo "-------------------------------------------"

INVALID_REQUEST='{
  "symbol": "BTCUSDT",
  "interval": "1h"
}'

echo "Request:"
echo "$INVALID_REQUEST" | jq .
echo ""

INVALID_RESPONSE=$(curl -s -X POST "$API_URL/api/backtest/run" \
    -H "Content-Type: application/json" \
    ${API_KEY:+-H "X-Api-Key: $API_KEY"} \
    -d "$INVALID_REQUEST")

echo "Response:"
echo "$INVALID_RESPONSE" | jq .
echo ""

if echo "$INVALID_RESPONSE" | jq -e '.error' > /dev/null 2>&1; then
    echo "✓ Validation error returned as expected"
else
    echo "✗ Expected validation error"
fi

echo ""
echo "===================================="
echo "Test Complete"
echo "===================================="
echo ""
echo "Note: If you see 'No candles found' errors, make sure to backfill"
echo "historical candle data using:"
echo ""
echo "  curl \"$API_URL/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&limit=200\""
echo ""
