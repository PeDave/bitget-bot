#!/bin/bash
# Manual test script for the /api/candles endpoint
# This script demonstrates how to test the endpoint when the API is running

BASE_URL="http://localhost:3001"
ENDPOINT="/api/candles"

echo "=== Testing /api/candles endpoint ==="
echo ""

# Test 1: Missing parameters (should return 400)
echo "Test 1: Missing required parameters"
curl -s -o /dev/null -w "Status: %{http_code}\n" "${BASE_URL}${ENDPOINT}"
echo ""

# Test 2: Valid request with all parameters
echo "Test 2: Valid request with all parameters"
START=$(date -u -d '1 day ago' +%Y-%m-%dT%H:%M:%SZ)
END=$(date -u +%Y-%m-%dT%H:%M:%SZ)
curl -s -o /dev/null -w "Status: %{http_code}\n" \
  "${BASE_URL}${ENDPOINT}?symbol=BTCUSDT&interval=1h&start=${START}&end=${END}&warmupCandles=50"
echo ""

# Test 3: Invalid market type (should return 400)
echo "Test 3: Invalid market type"
curl -s -o /dev/null -w "Status: %{http_code}\n" \
  "${BASE_URL}${ENDPOINT}?symbol=BTCUSDT&market=invalid&interval=1h&start=${START}&end=${END}"
echo ""

# Test 4: Valid request with futures market
echo "Test 4: Valid request with futures market"
curl -s -o /dev/null -w "Status: %{http_code}\n" \
  "${BASE_URL}${ENDPOINT}?symbol=BTCUSDT&market=futures&interval=1h&start=${START}&end=${END}&warmupCandles=100"
echo ""

# Test 5: Full response (pretty printed)
echo "Test 5: Full response sample"
curl -s "${BASE_URL}${ENDPOINT}?symbol=BTCUSDT&interval=1h&start=${START}&end=${END}&warmupCandles=10" | \
  python3 -m json.tool 2>/dev/null || echo "Response received (install python3 for pretty print)"
echo ""

echo "=== Tests complete ==="
echo "Note: These tests require the API to be running with 'dotnet run' in src/BitgetLab.Api"
echo "Expected responses:"
echo "  - Test 1: 400 (Bad Request)"
echo "  - Test 2: 200 (OK) or 503 (Service Unavailable if DB not configured)"
echo "  - Test 3: 400 (Bad Request)"
echo "  - Test 4: 200 (OK) or 503 (Service Unavailable if DB not configured)"
echo "  - Test 5: JSON with symbol, market, interval, start, end, warmupCandles, count, candles"
