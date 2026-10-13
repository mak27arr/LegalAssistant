using LegalAssistant.Application.Admin.Services;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Infrastructure.Admin;
using LegalAssistant.Infrastructure.Chunks;
using LegalAssistant.Infrastructure.Documents;
using LegalAssistant.Infrastructure.Embeddings;
using LegalAssistant.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAssistant.Api.Composition;

public static class ApiInfrastructureSupportServiceCollectionExtensions
{
    public static IServiceCollection AddApiInfrastructureSupport(this IServiceCollection services)
    {
        services.AddScoped<IDocumentChunkQueryService, DocumentChunkQueryService>();
        services.AddScoped<IDocumentQueryService, DocumentQueryService>();
        services.AddScoped<IDocumentStatsQueryService, DocumentStatsQueryService>();
        services.AddScoped<IEmbeddingReplayService, EmbeddingReplayService>();
        services.AddScoped<IJobQueue, EfJobQueue>();
        services.AddScoped<IAdminUserQueryService, AdminUserQueryService>();
        services.AddScoped<IAdminUserRoleService, AdminUserRoleService>();
        services.AddScoped<IAdminUserManagementService, AdminUserManagementService>();

        services.AddSingleton<LegalAssistant.Infrastructure.Auth.EfAuthSessionStore>();
        services.AddSingleton<LegalAssistant.Application.Auth.IUserSessionManager>(serviceProvider =>
            serviceProvider.GetRequiredService<LegalAssistant.Infrastructure.Auth.EfAuthSessionStore>());
        services.AddSingleton<LegalAssistant.Infrastructure.Auth.IAuthSessionStore>(serviceProvider =>
            serviceProvider.GetRequiredService<LegalAssistant.Infrastructure.Auth.EfAuthSessionStore>());
        services.AddSingleton<Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository, LegalAssistant.Infrastructure.Auth.EfDataProtectionKeyRepository>();
        return services;
    }
}
