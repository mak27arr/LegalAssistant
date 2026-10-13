using LegalAssistant.Application.Ask.Services;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Rag.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAssistant.Api.Composition;

public static class ApiApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApiApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAskJobService, AskJobService>();
        services.AddScoped<IAskJobQueryService, AskJobQueryService>();
        services.AddScoped<IAskJobEventQueryService, AskJobEventQueryService>();
        services.AddScoped<IAskJobEventStreamUseCase, AskJobEventStreamUseCase>();
        services.AddScoped<IJobQueryService, JobQueryService>();
        services.AddScoped<IDocumentCommandService, DocumentCommandService>();
        services.AddScoped<IAskService, AskService>();
        services.AddScoped<IRagAnswerService, RagAnswerService>();
        services.AddSingleton<IRagPromptBuilder, DefaultRagPromptBuilder>();
        services.AddSingleton<IRagAnswerValidator, DefaultRagAnswerValidator>();
        return services;
    }
}
