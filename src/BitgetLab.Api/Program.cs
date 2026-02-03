using BitgetLab.Api.Services;
using BitgetLab.Core.Options;
using BitgetLab.Core.Services.Bitget;
using BitgetLab.Core.Services.Backtest;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Bitget options
builder.Services.Configure<BitgetOptions>(
    builder.Configuration.GetSection(BitgetOptions.SectionName));

// Configure Charting options
builder.Services.Configure<ChartingOptions>(
    builder.Configuration.GetSection(ChartingOptions.SectionName));

// Register Bitget services
builder.Services.AddSingleton<IBitgetClientFactory, BitgetClientFactory>();
builder.Services.AddSingleton<IBitgetSocketClientFactory, BitgetSocketClientFactory>();
builder.Services.AddSingleton<IMarketDataService, MarketDataService>();
builder.Services.AddSingleton<ITradingService, TradingService>();
builder.Services.AddSingleton<IAccountBalanceService, AccountBalanceService>();
builder.Services.AddSingleton<IFuturesPositionService, FuturesPositionService>();
builder.Services.AddSingleton<IAccountValuationService, AccountValuationService>();
builder.Services.AddSingleton<ISpotOrderQueryService, SpotOrderQueryService>();
builder.Services.AddSingleton<IFuturesOrderQueryService, FuturesOrderQueryService>();
builder.Services.AddSingleton<ISpotOrderHistoryService, SpotOrderHistoryService>();
builder.Services.AddSingleton<IFuturesOrderHistoryService, FuturesOrderHistoryService>();
builder.Services.AddSingleton<ICopyTradingService, CopyTradingService>();

// Register charting services
builder.Services.AddSingleton<ICandleRepository, PostgresCandleRepository>();
builder.Services.AddSingleton<ICandleService, CandleService>();
builder.Services.AddSingleton<IIndicatorService, IndicatorService>();

// Register backtest services
builder.Services.AddSingleton<IBacktestRepository, PostgresBacktestRepository>();
builder.Services.AddSingleton<IBacktestEngine, BacktestEngine>();
builder.Services.AddSingleton<IBacktestService, BacktestService>();

// Register WebSocketSubscriptionService as singleton and hosted service
builder.Services.AddSingleton<WebSocketSubscriptionService>();
builder.Services.AddSingleton<IWebSocketSubscriptionService>(sp => sp.GetRequiredService<WebSocketSubscriptionService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebSocketSubscriptionService>());

// Register system services
builder.Services.AddSingleton<ISystemMetricsService, SystemMetricsService>();
builder.Services.AddSingleton<ISystemServicesService, SystemServicesService>();

// CORS for local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
