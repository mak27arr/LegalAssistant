using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Ask.Services;
using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Rag.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAssistant.Application.DependencyInjection;

public static class AskApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAskApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAskService, AskService>();
        services.AddScoped<IAskJobProcessor, AskJobProcessor>();
        services.AddScoped<IRagAnswerService, RagAnswerService>();

        services.AddSingleton<IRagPromptBuilder, DefaultRagPromptBuilder>();
        services.AddSingleton<IRagAnswerValidator, DefaultRagAnswerValidator>();

        return services;
    }
}
