# Bitget.Net Vendor Integration Plan

This document describes how to integrate the Bitget.Net SDK into the BitgetLab project.

## Overview

The Bitget.Net SDK (https://github.com/JKorf/Bitget.Net) will be vendored into this repository to provide cryptocurrency exchange integration with Bitget.

## Directory Structure

```
/root/bitget-bot/
├── vendor/
│   └── Bitget.Net/          # Git submodule/subtree
│       ├── Bitget.Net/
│       │   └── Bitget.Net.csproj
│       └── ...
├── src/
│   ├── BitgetLab.Core/
│   ├── BitgetLab.Api/
│   └── ...
```

## Integration Methods

### Option 1: Git Submodule (Recommended)

Git submodules allow you to keep a Git repository as a subdirectory of another Git repository.

**Add the submodule:**

```bash
cd /root/bitget-bot
git submodule add https://github.com/JKorf/Bitget.Net.git vendor/Bitget.Net
git submodule update --init --recursive
```

**Clone with submodules:**

```bash
git clone --recursive https://github.com/PeDave/bitget-bot.git
```

**Update submodule to latest:**

```bash
cd vendor/Bitget.Net
git checkout main
git pull
cd ../..
git add vendor/Bitget.Net
git commit -m "Update Bitget.Net to latest version"
```

**Pros:**
- Easy to track upstream changes
- Clear separation between your code and vendor code
- Can easily update to newer versions

**Cons:**
- Requires explicit initialization when cloning
- Slightly more complex Git workflow

### Option 2: Git Subtree

Git subtree allows you to nest one repository inside another as a sub-directory.

**Add the subtree:**

```bash
cd /root/bitget-bot
git subtree add --prefix=vendor/Bitget.Net https://github.com/JKorf/Bitget.Net.git main --squash
```

**Update subtree:**

```bash
git subtree pull --prefix=vendor/Bitget.Net https://github.com/JKorf/Bitget.Net.git main --squash
```

**Pros:**
- No special commands needed when cloning
- Simpler for users

**Cons:**
- More complex update process
- Larger repository size

## Add Project Reference

Once the vendor code is in place, add project references:

### Update BitgetLab.Core.csproj

Add a project reference to Bitget.Net:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- Add reference to vendored Bitget.Net -->
    <ProjectReference Include="..\..\vendor\Bitget.Net\Bitget.Net\Bitget.Net.csproj" />
  </ItemGroup>
</Project>
```

### Update BitgetLab.Api.csproj

The API project already references Core, so it will transitively get Bitget.Net:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\BitgetLab.Core\BitgetLab.Core.csproj" />
  </ItemGroup>
</Project>
```

## Implementation Steps

Once Bitget.Net is vendored, implement the following:

### 1. Update BitgetClientFactory

```csharp
using Bitget.Net.Clients;
using BitgetLab.Core.Options;

namespace BitgetLab.Core.Services.Bitget;

public class BitgetClientFactory : IBitgetClientFactory
{
    private readonly BitgetOptions _options;

    public BitgetClientFactory(IOptions<BitgetOptions> options)
    {
        _options = options.Value;
    }

    public BitgetRestClient CreateClient(BitgetCredentials credentials)
    {
        return new BitgetRestClient(options =>
        {
            options.ApiCredentials = new ApiCredentials(
                credentials.ApiKey,
                credentials.ApiSecret,
                credentials.Passphrase
            );
        });
    }

    public BitgetRestClient CreateClientForCurrentMode()
    {
        var credentials = _options.Mode == "Trade" 
            ? _options.Trade 
            : _options.ReadOnly;
        
        return CreateClient(credentials);
    }
}
```

### 2. Update MarketDataService

```csharp
using Bitget.Net.Clients;
using Bitget.Net.Interfaces.Clients;

namespace BitgetLab.Core.Services.Bitget;

public class MarketDataService : IMarketDataService
{
    private readonly BitgetRestClient _client;

    public MarketDataService(IBitgetClientFactory factory)
    {
        _client = factory.CreateClientForCurrentMode();
    }

    public async Task<IEnumerable<string>> GetSymbolsAsync(CancellationToken ct = default)
    {
        var result = await _client.SpotApi.ExchangeData.GetSymbolsAsync(ct: ct);
        return result.Data.Select(s => s.Symbol);
    }

    public async Task<TickerData> GetTickerAsync(string symbol, CancellationToken ct = default)
    {
        var result = await _client.SpotApi.ExchangeData.GetTickerAsync(symbol, ct: ct);
        var ticker = result.Data;
        
        return new TickerData
        {
            Symbol = ticker.Symbol,
            LastPrice = ticker.LastPrice,
            Volume = ticker.Volume24h,
            Timestamp = ticker.Timestamp
        };
    }
}
```

### 3. Update TradingService

```csharp
using Bitget.Net.Clients;
using Bitget.Net.Enums;

namespace BitgetLab.Core.Services.Bitget;

public class TradingService : ITradingService
{
    private readonly BitgetRestClient _client;
    private readonly BitgetOptions _options;

    public TradingService(IBitgetClientFactory factory, IOptions<BitgetOptions> options)
    {
        _client = factory.CreateClientForCurrentMode();
        _options = options.Value;
    }

    public async Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct = default)
    {
        // Safety check: prevent trading in ReadOnly mode
        if (_options.Mode == "ReadOnly")
        {
            throw new InvalidOperationException("Cannot place orders in ReadOnly mode");
        }

        var result = await _client.SpotApi.Trading.PlaceOrderAsync(
            symbol: request.Symbol,
            side: request.Side == OrderSide.Buy ? Bitget.Net.Enums.OrderSide.Buy : Bitget.Net.Enums.OrderSide.Sell,
            type: Bitget.Net.Enums.OrderType.Limit,
            quantity: request.Quantity,
            price: request.Price,
            ct: ct
        );

        return new OrderResult
        {
            OrderId = result.Data.OrderId,
            Symbol = request.Symbol,
            Status = result.Success ? "Success" : "Failed"
        };
    }
}
```

### 4. Register Services in API

Update `Program.cs` in BitgetLab.Api:

```csharp
// Register Bitget services
builder.Services.AddSingleton<IBitgetClientFactory, BitgetClientFactory>();
builder.Services.AddSingleton<IMarketDataService, MarketDataService>();
builder.Services.AddSingleton<ITradingService, TradingService>();
```

### 5. Update Controllers

Update the BitgetController to use real services:

```csharp
[ApiController]
[Route("api/bitget")]
public class BitgetController : ControllerBase
{
    private readonly IMarketDataService _marketData;
    private readonly ITradingService _trading;

    public BitgetController(IMarketDataService marketData, ITradingService trading)
    {
        _marketData = marketData;
        _trading = trading;
    }

    [HttpGet("symbols")]
    public async Task<IActionResult> GetSymbols()
    {
        var symbols = await _marketData.GetSymbolsAsync();
        return Ok(symbols);
    }

    [HttpGet("market/ticker")]
    public async Task<IActionResult> GetTicker([FromQuery] string symbol)
    {
        var ticker = await _marketData.GetTickerAsync(symbol);
        return Ok(ticker);
    }

    [HttpPost("orders")]
    public async Task<IActionResult> PlaceOrder([FromBody] OrderRequest request)
    {
        try
        {
            var result = await _trading.PlaceOrderAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
```

## Testing

### Test Market Data (ReadOnly Mode)

```bash
# Get symbols
curl https://api.labotkripto.com/api/bitget/symbols

# Get ticker
curl https://api.labotkripto.com/api/bitget/market/ticker?symbol=BTCUSDT
```

### Test Trading (Trade Mode - Use with Caution!)

```bash
# Attempt to place order (requires Trade mode)
curl -X POST https://api.labotkripto.com/api/bitget/orders \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "side": "Buy",
    "quantity": 0.001,
    "price": 45000
  }'
```

## Security Best Practices

1. **Start with ReadOnly mode** until thoroughly tested
2. **Never commit API credentials** to git
3. **Use separate API keys** for ReadOnly and Trade modes
4. **Enable IP whitelisting** on Bitget for your VPS IP
5. **Test with small amounts** before scaling up
6. **Monitor API usage** to avoid rate limits
7. **Keep API keys secure** using environment variables or secure configuration

## References

- Bitget.Net GitHub: https://github.com/JKorf/Bitget.Net
- Bitget API Documentation: https://bitgetlimited.github.io/apidoc/en/mix/
- CryptoExchange.Net Documentation: https://jkorf.github.io/CryptoExchange.Net/
