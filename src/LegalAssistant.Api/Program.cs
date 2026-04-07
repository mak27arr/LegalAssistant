using LegalAssistant.Api.DependencyInjection;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.DependencyInjection;
using LegalAssistant.Infrastructure.DependencyInjection;
using LegalAssistant.Logging.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
// Centralized logging registration for all containers (writes JSON to file for sidecar)
// provide service name so logs go to separate per-service files
builder.Services.AddCentralizedLogging(builder.Configuration, "api");
builder.Services.AddApiInfrastructure(builder.Configuration);

var app = builder.Build();

// Use correlation middleware
app.UseMiddleware<LegalAssistant.Api.Middleware.CorrelationMiddleware>();

// Request timing middleware (logs processing time for HTTP requests)
// Can be enabled/disabled via configuration: Logging:RequestTiming:Enabled (default: true)
var enableRequestTiming = builder.Configuration.GetValue<bool?>("Logging:RequestTiming:Enabled") ?? true;
if (enableRequestTiming)
{
    app.UseMiddleware<LegalAssistant.Logging.Middleware.RequestTimingMiddleware>();
}

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrEmpty(urls))
{
    app.Urls.Add("http://0.0.0.0:80");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.MapControllers();

app.Run();
