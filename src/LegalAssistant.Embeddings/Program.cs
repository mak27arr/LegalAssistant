using LegalAssistant.Embeddings.DependencyInjection;
using LegalAssistant.Embeddings.ServiceEndpoints;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddEmbeddingsServices(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var enableRequestTiming = builder.Configuration.GetValue<bool?>("Logging:RequestTiming:Enabled") ?? true;
if (enableRequestTiming)
{
    app.UseMiddleware<LegalAssistant.Logging.Middleware.RequestTimingMiddleware>();
}

app.MapEmbedEndpoint();
app.MapHealthEndpoint();

app.Run();
