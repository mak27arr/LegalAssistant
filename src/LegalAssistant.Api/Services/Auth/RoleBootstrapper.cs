using LegalAssistant.Api.Services.Auth.Constants;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Api.Services.Auth;

public sealed class RoleBootstrapper : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public RoleBootstrapper(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();

        await EnsureRoleAsync(db, RoleNames.User, "Default application user role.", cancellationToken);
        await EnsureRoleAsync(db, RoleNames.Admin, "Administrative role.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureRoleAsync(
        LegalAssistantDbContext db,
        string name,
        string description,
        CancellationToken cancellationToken)
    {
        if (await db.Roles.AnyAsync(x => x.Name == name, cancellationToken))
        {
            return;
        }

        db.Roles.Add(new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description
        });
    }
}
