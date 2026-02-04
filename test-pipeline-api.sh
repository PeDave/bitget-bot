#!/bin/bash
# Test script for pipeline API endpoints

API_URL="http://localhost:3001"
SYMBOL="BTCUSDT"
MARKET="futures"

echo "==================================="
echo "Pipeline API Endpoint Tests"
echo "==================================="
echo ""

# Test 1: Start pipeline (should succeed when enabled, or return error when disabled)
echo "Test 1: POST /api/bitget/pipeline/start?symbol=$SYMBOL&market=$MARKET"
curl -X POST "$API_URL/api/bitget/pipeline/start?symbol=$SYMBOL&market=$MARKET" -H "Content-Type: application/json" -s | python3 -m json.tool
echo ""
echo ""

# Test 2: Get pipeline status
echo "Test 2: GET /api/bitget/pipeline/status?symbol=$SYMBOL&market=$MARKET"
curl -X GET "$API_URL/api/bitget/pipeline/status?symbol=$SYMBOL&market=$MARKET" -s | python3 -m json.tool
echo ""
echo ""

# Test 3: List all pipelines
echo "Test 3: GET /api/bitget/pipeline/list"
curl -X GET "$API_URL/api/bitget/pipeline/list" -s | python3 -m json.tool
echo ""
echo ""

# Test 4: Stop pipeline
echo "Test 4: POST /api/bitget/pipeline/stop?symbol=$SYMBOL&market=$MARKET"
curl -X POST "$API_URL/api/bitget/pipeline/stop?symbol=$SYMBOL&market=$MARKET" -H "Content-Type: application/json" -s | python3 -m json.tool
echo ""
echo ""

echo "==================================="
echo "Tests complete"
echo "==================================="
