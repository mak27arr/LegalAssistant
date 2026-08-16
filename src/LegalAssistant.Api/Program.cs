using LegalAssistant.Api.DependencyInjection;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.DependencyInjection;
using LegalAssistant.Infrastructure.DependencyInjection;
using LegalAssistant.Api.ServiceEndpoints;
using LegalAssistant.Logging.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddCentralizedLogging(builder.Configuration, "api");
builder.Services.AddApiInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<LegalAssistant.Api.Middleware.CorrelationMiddleware>();

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
app.MapHealthEndpoint();

app.Run();
