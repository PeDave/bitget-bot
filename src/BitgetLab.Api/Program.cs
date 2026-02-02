using System.Text.Json.Serialization;
using BitgetLab.Api.Services;
using BitgetLab.Core.Options;
using BitgetLab.Core.Services.Bitget;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enable string-based enum serialization (allows case-insensitive deserialization in ASP.NET Core)
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "BitgetLab API", Version = "v1" });
    c.EnableAnnotations();
    
    // Include XML comments for better documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configure Bitget options
builder.Services.Configure<BitgetOptions>(
    builder.Configuration.GetSection(BitgetOptions.SectionName));

// Register Bitget services
builder.Services.AddSingleton<IBitgetClientFactory, BitgetClientFactory>();
builder.Services.AddSingleton<IMarketDataService, MarketDataService>();
builder.Services.AddSingleton<ITradingService, TradingService>();

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
// Enable Swagger in Development and optionally in Production with configuration
var enableSwaggerInProduction = builder.Configuration.GetValue<bool>("Swagger:EnableInProduction", false);
if (app.Environment.IsDevelopment() || enableSwaggerInProduction)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BitgetLab API v1");
        c.RoutePrefix = "swagger"; // Swagger UI at /swagger
    });
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
