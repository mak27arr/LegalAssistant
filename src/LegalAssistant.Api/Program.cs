using LegalAssistant.Api.DependencyInjection;
using LegalAssistant.Application.DependencyInjection;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.DependencyInjection;
using LegalAssistant.Infrastructure.Health;
using LegalAssistant.Api.ServiceEndpoints;
using LegalAssistant.Api.Swagger;
using LegalAssistant.Logging.DependencyInjection;
using LegalAssistant.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.AddGlobalFilters();
}).ConfigureApiBehaviorOptions(options =>
{
    options.ConfigureValidationProblemDetails();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<RequireIdempotencyKeyOperationFilter>();
});

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddCentralizedLogging(builder.Configuration, "api");
builder.Services.AddApiInfrastructure(builder.Configuration);
builder.Services.AddApiReadinessHealthChecks();

var app = builder.Build();
await app.Services.ApplyDatabaseMigrationsAsync();
ConfigurationCheackwarning.LogIfIncomplete(app.Services);

app.UseMiddleware<LegalAssistant.Api.Middleware.GlobalExceptionHandlingMiddleware>();
app.UseMiddleware<LegalAssistant.Api.Middleware.CorrelationMiddleware>();

var enableRequestTiming = builder.Configuration.GetValue<bool?>("Logging:RequestTiming:Enabled") ?? true;
if (enableRequestTiming)
{
    app.UseMiddleware<LegalAssistant.Logging.Middleware.RequestTimingMiddleware>();
}

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrEmpty(urls))
{
    app.Logger.LogWarning("ASPNETCORE_URLS environment variable is not configured. Defaulting binding to http://0.0.0.0:80");
    app.Urls.Add("http://0.0.0.0:80");
}

if (app.Environment.IsDevelopment())
{
    app.Logger.LogWarning("Development environment detected: Enabling Swagger API documentation at /swagger");
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.Logger.LogInformation("Production/Non-development environment: Swagger UI is disabled.");
}

app.UseRouting();
app.UseCors(LegalAssistant.Api.DependencyInjection.ServiceCollectionExtensions.GetFrontendCorsPolicyName());
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthEndpoint();

app.Run();
