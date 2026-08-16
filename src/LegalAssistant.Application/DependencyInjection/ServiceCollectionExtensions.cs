using Microsoft.Extensions.DependencyInjection;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Rag.Services;
using LegalAssistant.Application.Documents.Services;

namespace LegalAssistant.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAskService, AskService>();
        services.AddScoped<IJobQueryService, JobQueryService>();
        services.AddScoped<IRagAnswerService, RagAnswerService>();
        services.AddScoped<IDocumentCommandService, DocumentCommandService>();

        services.AddSingleton<IRagPromptBuilder, DefaultRagPromptBuilder>();
        services.AddSingleton<IRagAnswerValidator, DefaultRagAnswerValidator>();

        return services;
    }
}
