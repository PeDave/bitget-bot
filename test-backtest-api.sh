#!/bin/bash
# Test script for backtesting API endpoints
# 
# Prerequisites:
# 1. BitgetLab API running on port 3001
# 2. PostgreSQL database configured
# 3. Database tables created (002_create_backtest_tables.sql)
# 4. Some historical candle data loaded
#
# Usage: ./test-backtest-api.sh

set -e

API_BASE="http://localhost:3001/api/bitget"
echo "Testing Backtesting API at $API_BASE"
echo "=========================================="
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Test 1: Run a backtest
echo -e "${YELLOW}Test 1: Running EMA Crossover backtest${NC}"
BACKTEST_RESPONSE=$(curl -s -X POST "${API_BASE}/backtests/run" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-07T23:59:59Z",
    "strategy": "ema_cross",
    "parameters": {"fastPeriod": 10, "slowPeriod": 20},
    "initialBalance": 10000,
    "feeBps": 10,
    "slippageBps": 5
  }')

echo "Response: $BACKTEST_RESPONSE"
echo ""

# Extract backtest ID from response
BACKTEST_ID=$(echo $BACKTEST_RESPONSE | grep -o '"backtestId":"[^"]*"' | cut -d'"' -f4)

if [ -z "$BACKTEST_ID" ]; then
  echo -e "${RED}✗ Failed to get backtest ID${NC}"
  echo "Response was: $BACKTEST_RESPONSE"
  exit 1
else
  echo -e "${GREEN}✓ Backtest created with ID: $BACKTEST_ID${NC}"
fi
echo ""

# Test 2: Get backtest details
echo -e "${YELLOW}Test 2: Getting backtest details${NC}"
sleep 2  # Give it a moment to complete
DETAILS_RESPONSE=$(curl -s "${API_BASE}/backtests/${BACKTEST_ID}")
echo "Response: $DETAILS_RESPONSE"
echo ""

# Check if backtest completed
STATUS=$(echo $DETAILS_RESPONSE | grep -o '"status":"[^"]*"' | cut -d'"' -f4)
echo -e "${GREEN}✓ Backtest status: $STATUS${NC}"
echo ""

# Test 3: Get backtest trades
echo -e "${YELLOW}Test 3: Getting backtest trades${NC}"
TRADES_RESPONSE=$(curl -s "${API_BASE}/backtests/${BACKTEST_ID}/trades")
echo "Response: $TRADES_RESPONSE"
TRADE_COUNT=$(echo $TRADES_RESPONSE | grep -o '"count":[0-9]*' | cut -d':' -f2)
echo -e "${GREEN}✓ Found $TRADE_COUNT trades${NC}"
echo ""

# Test 4: List all backtests
echo -e "${YELLOW}Test 4: Listing all backtests${NC}"
LIST_RESPONSE=$(curl -s "${API_BASE}/backtests?limit=10")
echo "Response: $LIST_RESPONSE"
BACKTEST_COUNT=$(echo $LIST_RESPONSE | grep -o '"count":[0-9]*' | cut -d':' -f2)
echo -e "${GREEN}✓ Found $BACKTEST_COUNT backtests${NC}"
echo ""

# Test 5: List with filters
echo -e "${YELLOW}Test 5: Listing backtests with filters${NC}"
FILTERED_RESPONSE=$(curl -s "${API_BASE}/backtests?symbol=BTCUSDT&strategy=ema_cross")
echo "Response: $FILTERED_RESPONSE"
echo -e "${GREEN}✓ Filter applied successfully${NC}"
echo ""

# Test 6: Run RSI strategy backtest
echo -e "${YELLOW}Test 6: Running RSI strategy backtest${NC}"
RSI_RESPONSE=$(curl -s -X POST "${API_BASE}/backtests/run" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-07T23:59:59Z",
    "strategy": "rsi",
    "parameters": {"period": 14, "oversoldThreshold": 30, "overboughtThreshold": 70},
    "initialBalance": 10000,
    "feeBps": 10,
    "slippageBps": 5
  }')

echo "Response: $RSI_RESPONSE"
RSI_BACKTEST_ID=$(echo $RSI_RESPONSE | grep -o '"backtestId":"[^"]*"' | cut -d'"' -f4)

if [ -z "$RSI_BACKTEST_ID" ]; then
  echo -e "${RED}✗ Failed to create RSI backtest${NC}"
else
  echo -e "${GREEN}✓ RSI backtest created with ID: $RSI_BACKTEST_ID${NC}"
fi
echo ""

# Test 7: Test EMA crossover with different periods (edge case validation)
echo -e "${YELLOW}Test 7: Testing EMA Crossover with fastPeriod=5, slowPeriod=15${NC}"
EMA_EDGE_RESPONSE=$(curl -s -X POST "${API_BASE}/backtests/run" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-07T23:59:59Z",
    "strategy": "ema_cross",
    "parameters": {"fastPeriod": 5, "slowPeriod": 15},
    "initialBalance": 10000,
    "feeBps": 10,
    "slippageBps": 5
  }')

echo "Response: $EMA_EDGE_RESPONSE"
EMA_EDGE_BACKTEST_ID=$(echo $EMA_EDGE_RESPONSE | grep -o '"backtestId":"[^"]*"' | cut -d'"' -f4)

if [ -z "$EMA_EDGE_BACKTEST_ID" ]; then
  echo -e "${RED}✗ Failed to create EMA edge case backtest${NC}"
else
  echo -e "${GREEN}✓ EMA edge case backtest created with ID: $EMA_EDGE_BACKTEST_ID${NC}"
fi
echo ""

# Summary
echo "=========================================="
echo -e "${GREEN}All tests completed!${NC}"
echo ""
echo "Summary:"
echo "- EMA Crossover backtest ID: $BACKTEST_ID"
echo "- RSI backtest ID: $RSI_BACKTEST_ID"
echo "- EMA edge case backtest ID: $EMA_EDGE_BACKTEST_ID"
echo ""
echo "Next steps:"
echo "1. View backtest details in your browser:"
echo "   http://localhost:3001/api/bitget/backtests/${BACKTEST_ID}"
echo "2. Import the n8n workflow from docs/n8n/backtest-workflow.json"
echo "3. Check the database tables:"
echo "   SELECT * FROM backtests;"
echo "   SELECT * FROM backtest_trades;"
