# Swagger and API Improvements

This document describes the changes made to add Swagger/OpenAPI documentation and improve the developer experience for the BitgetLab API.

## Changes Made

### 1. Swagger/OpenAPI Integration

Added comprehensive API documentation using Swashbuckle.AspNetCore:

- **Swagger JSON**: Available at `/swagger/v1/swagger.json`
- **Swagger UI**: Available at `/swagger` (interactive API documentation)
- **Environment Control**: 
  - Automatically enabled in Development environment
  - Configurable for Production via `Swagger:EnableInProduction` setting

### 2. Order Endpoint Improvements

**Case-Insensitive Enum Deserialization**:
- Enum values are now case-insensitive
- `"buy"`, `"Buy"`, `"BUY"` all work correctly
- Applies to both `side` (Buy/Sell) and `type` (Market/Limit) fields

**Direct Request Body Binding**:
- No wrapper property required
- Send the OrderRequest object directly in the request body
- Clean and intuitive API design

**Comprehensive Documentation**:
- All enum values documented in Swagger
- XML comments for better IntelliSense
- Clear error responses (400 for invalid, 403 for ReadOnly mode)

### 3. Configuration

Add to `appsettings.json` to enable Swagger in Production:

```json
{
  "Swagger": {
    "EnableInProduction": true
  }
}
```

### 4. cURL Examples

**Limit Order (Buy)**:
```bash
curl -X POST http://localhost:3001/api/bitget/orders \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "side": "Buy",
    "type": "Limit",
    "quantity": 0.001,
    "price": 50000
  }'
```

**Market Order (Sell)**:
```bash
curl -X POST http://localhost:3001/api/bitget/orders \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "ETHUSDT",
    "side": "Sell",
    "type": "Market",
    "quantity": 0.01
  }'
```

**Case-Insensitive Examples**:
```bash
# Lowercase works
curl -X POST http://localhost:3001/api/bitget/orders \
  -H "Content-Type: application/json" \
  -d '{"symbol":"BTCUSDT","side":"buy","type":"limit","quantity":0.001,"price":50000}'

# Uppercase works
curl -X POST http://localhost:3001/api/bitget/orders \
  -H "Content-Type: application/json" \
  -d '{"symbol":"BTCUSDT","side":"SELL","type":"MARKET","quantity":0.001}'

# Mixed case works
curl -X POST http://localhost:3001/api/bitget/orders \
  -H "Content-Type: application/json" \
  -d '{"symbol":"BTCUSDT","side":"Sell","type":"Limit","quantity":0.001,"price":40000}'
```

### 5. Error Handling

**400 Bad Request**: Invalid request (missing required fields, invalid enum values)
```bash
# Missing symbol field
curl -X POST http://localhost:3001/api/bitget/orders \
  -H "Content-Type: application/json" \
  -d '{"side":"buy","type":"limit","quantity":0.001}'
```

**403 Forbidden**: Trading not allowed (API in ReadOnly mode)
```bash
# Returns 403 when Mode is "ReadOnly"
curl -X POST http://localhost:3001/api/bitget/orders \
  -H "Content-Type: application/json" \
  -d '{"symbol":"BTCUSDT","side":"buy","type":"limit","quantity":0.001,"price":50000}'
```

## Technical Details

### Packages Added

- `Swashbuckle.AspNetCore` (v10.1.1)
- `Swashbuckle.AspNetCore.Annotations` (v10.1.1)

### Code Changes

**Program.cs**:
- Added `JsonStringEnumConverter` for case-insensitive enum deserialization
- Configured Swashbuckle with XML comments
- Added conditional Swagger middleware based on environment

**TradingService.cs**:
- Added XML documentation comments
- Added `[Required]` validation attributes
- Added `[JsonConverter]` attributes for enums

**BitgetController.cs**:
- Added Swagger operation attributes
- Added response status code documentation
- Enhanced parameter descriptions

## Testing

All functionality has been tested and verified:

✓ Swagger JSON accessible at `/swagger/v1/swagger.json`  
✓ Swagger UI accessible at `/swagger`  
✓ Case-insensitive enum deserialization (buy/Buy/BUY)  
✓ Direct request body binding (no wrapper needed)  
✓ 400 error for invalid requests  
✓ 403 error when in ReadOnly mode  
✓ Health endpoint working  

## .NET 8 Compatibility

The project targets .NET 8 and uses the PeDave fork of Bitget.Net which ensures full .NET 8 compatibility:

- Submodule URL: `https://github.com/PeDave/Bitget.Net`
- Current commit: `b6b15c54af28865f3c0c52c3ceeee1d9f9fe62fe`
- All projects target `net8.0`
- Built and tested with .NET 8 SDK

## Benefits

1. **Better Developer Experience**: Interactive API documentation with Swagger UI
2. **Case-Insensitive Enums**: More forgiving API that accepts various casing styles
3. **Clean Request Format**: No wrapper properties needed in request bodies
4. **Comprehensive Documentation**: All endpoints, parameters, and responses documented
5. **Easy Testing**: Swagger UI allows testing endpoints directly from the browser
6. **Production-Ready**: Configurable Swagger availability for production deployments
