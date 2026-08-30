using LegalAssistant.Infrastructure.Db;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Api.Services.Auth;

public static class ActiveUserJwtBearerEvents
{
    public static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        var authenticatedUser = context.Principal?.ToAuthenticatedUser();
        if (authenticatedUser == null)
        {
            context.Fail("Authenticated user claims are missing.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<LegalAssistantDbContext>();
        var isActive = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == authenticatedUser.Id)
            .Select(x => x.IsActive)
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (!isActive)
        {
            context.Fail("User account is blocked.");
        }
    }
}
