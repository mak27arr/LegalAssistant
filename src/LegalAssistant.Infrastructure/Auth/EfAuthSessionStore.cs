using System.Security.Claims;
using LegalAssistant.Application.Auth;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LegalAssistant.Infrastructure.Auth;

public sealed class EfAuthSessionStore : IAuthSessionStore
{
    public const string SessionIdClaimType = "legalassistant:session_id";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public EfAuthSessionStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var sessionId = ticket.Principal.FindFirstValue(SessionIdClaimType);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
        }

        await StoreOrRenewAsync(sessionId, ticket);
        return sessionId;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
        => StoreOrRenewAsync(key, ticket);

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var record = await db.AuthSessions
            .AsNoTracking()
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == key);

        if (record == null || record.ExpiresAt <= now || record.User?.IsActive != true)
        {
            return null;
        }

        return TicketSerializer.Default.Deserialize(record.Ticket);
    }

    public async Task RemoveAsync(string key)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();

        try
        {
            var deleted = await db.AuthSessions
                .Where(x => x.Id == key)
                .ExecuteDeleteAsync();

            if (deleted > 0)
            {
                return;
            }
        }
        catch (InvalidOperationException)
        {
        }

        {
            var record = await db.AuthSessions.FindAsync(key);
            if (record != null)
            {
                db.AuthSessions.Remove(record);
                await db.SaveChangesAsync();
            }
        }
    }

    public async Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        return await db.AuthSessions.AnyAsync(x => x.Id == sessionId && x.ExpiresAt > now, cancellationToken);
    }

    public async Task RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();

        try
        {
            var deleted = await db.AuthSessions
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
            {
                return;
            }
        }
        catch (InvalidOperationException)
        {
        }

        {
            var sessions = await db.AuthSessions.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
            db.AuthSessions.RemoveRange(sessions);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task StoreOrRenewAsync(string key, AuthenticationTicket ticket)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
        var options = scope.ServiceProvider.GetService<IOptions<AuthSessionOptions>>()?.Value ?? new AuthSessionOptions();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var userIdClaim = ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Session ticket is missing a user id claim.");
        var userId = Guid.Parse(userIdClaim);
        var expiresAt = ticket.Properties.ExpiresUtc?.UtcDateTime
            ?? now.AddMinutes(Math.Max(1, options.IdleTimeoutMinutes));

        var absoluteExpiresAt = ticket.Properties.IssuedUtc?.UtcDateTime.AddHours(Math.Max(1, options.AbsoluteLifetimeHours));
        if (absoluteExpiresAt.HasValue && absoluteExpiresAt.Value < expiresAt)
        {
            expiresAt = absoluteExpiresAt.Value;
        }

        var record = await db.AuthSessions.SingleOrDefaultAsync(x => x.Id == key);
        if (record == null)
        {
            record = new AuthSessionRecord
            {
                Id = key,
                UserId = userId,
                Ticket = TicketSerializer.Default.Serialize(ticket),
                CreatedAt = ticket.Properties.IssuedUtc?.UtcDateTime ?? now,
                LastRenewedAt = now,
                ExpiresAt = expiresAt
            };
            await db.AuthSessions.AddAsync(record);
        }
        else
        {
            record.UserId = userId;
            record.Ticket = TicketSerializer.Default.Serialize(ticket);
            record.LastRenewedAt = now;
            record.ExpiresAt = expiresAt;
        }

        await db.SaveChangesAsync();
    }
}
