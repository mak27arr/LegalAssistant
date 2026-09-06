using LegalAssistant.Core.Correlation;
using LegalAssistant.Api.Services;
using LegalAssistant.Api.Common;
using LegalAssistant.Api.Configuration;
using LegalAssistant.Api.Services.Auth;
using LegalAssistant.Application.Auth;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Ask;
using LegalAssistant.Messaging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

namespace LegalAssistant.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private const string CorsPolicyName = "Frontend";

    public static IServiceCollection AddApiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationContext, ApiCorrelationContext>();
        services.AddSingleton<LegalAssistant.Application.Ask.IAskJobEventFanout, InMemoryAskJobEventFanout>();
        services.AddScoped<IAskJobEventStreamService, AskJobEventStreamService>();
        services.AddRabbitMqConsumer<AskJobEventRecord, RabbitMqAskJobEventRelayConsumerDefinition>();
        services.AddHostedService<RoleBootstrapper>();
        services.AddScoped<IAuthUserProvisioningService, AuthUserProvisioningService>();
        services.AddSingleton(TimeProvider.System);
        services.Configure<LegalAssistant.Infrastructure.Auth.AuthSessionOptions>(configuration.GetSection($"{AuthOptions.SectionName}:Session"));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddDataProtectionAndAntiforgery(environment);

        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        ConfigureAuthentication(services, authOptions, environment);
        ConfigureAuthorization(services);
        ConfigureCors(services, configuration);

        return services;
    }

    public static IServiceCollection AddApiSwaggerSecurity(this IServiceCollection services)
    {
        return services;
    }

    public static string GetFrontendCorsPolicyName() => CorsPolicyName;

    private static void ConfigureAuthentication(
        IServiceCollection services,
        AuthOptions authOptions,
        IHostEnvironment environment)
    {
        var cookieSecurePolicy = GetCookieSecurePolicy(environment);

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ApplicationAuthSchemes.Application;
                options.DefaultChallengeScheme = ApplicationAuthSchemes.Application;
                options.DefaultSignInScheme = ApplicationAuthSchemes.Application;
                options.DefaultScheme = ApplicationAuthSchemes.Application;
            })
            .AddCookie(ApplicationAuthSchemes.Application, options =>
            {
                options.Cookie.Name = GetCookieName(authOptions.Session.CookieName, environment);
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = cookieSecurePolicy;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(Math.Max(1, authOptions.Session.IdleTimeoutMinutes));
                options.SlidingExpiration = true;
                options.LoginPath = authOptions.Google.LoginPath;
                options.AccessDeniedPath = "/api/auth/forbidden";
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    },
                    OnValidatePrincipal = ValidateApplicationSessionAsync
                };
            })
            .AddCookie(ApplicationAuthSchemes.External, options =>
            {
                options.Cookie.Name = "legalassistant.external";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = cookieSecurePolicy;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddGoogle("Google", options =>
            {
                options.SignInScheme = ApplicationAuthSchemes.External;
                options.ClientId = authOptions.Google.ClientId;
                options.ClientSecret = authOptions.Google.ClientSecret;
                options.CallbackPath = authOptions.Google.CallbackPath;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = authOptions.Jwt.Issuer,
                    ValidAudience = authOptions.Jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.Jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ActiveUserJwtBearerEvents.OnTokenValidatedAsync
                };
            });

        services.AddOptions<CookieAuthenticationOptions>(ApplicationAuthSchemes.Application)
            .Configure<LegalAssistant.Infrastructure.Auth.IAuthSessionStore>((options, sessionStore) =>
            {
                options.SessionStore = sessionStore;
            });
    }

    private static async Task ValidateApplicationSessionAsync(CookieValidatePrincipalContext context)
    {
        var authOptions = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthOptions>>().Value;
        var db = context.HttpContext.RequestServices.GetRequiredService<LegalAssistant.Infrastructure.Db.LegalAssistantDbContext>();
        var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            context.RejectPrincipal();
            return;
        }

        if (context.Properties.IssuedUtc.HasValue)
        {
            var absoluteExpiresAt = context.Properties.IssuedUtc.Value.AddHours(Math.Max(1, authOptions.Session.AbsoluteLifetimeHours));
            if (absoluteExpiresAt <= DateTimeOffset.UtcNow)
            {
                context.RejectPrincipal();
                return;
            }
        }

        var isActive = await db.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.IsActive);
        if (!isActive)
        {
            context.RejectPrincipal();
        }
    }

    private static void ConfigureAuthorization(IServiceCollection services)
    {
        services.AddAuthorization();
    }

    private static void ConfigureCors(IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection($"{CorsOptions.SectionName}:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
        });
    }

    private static void AddDataProtectionAndAntiforgery(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        services.AddDataProtection()
            .SetApplicationName("LegalAssistant");

        services.AddOptions<KeyManagementOptions>()
            .Configure<Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository>((options, repository) =>
            {
                options.XmlRepository = repository;
            });

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = GetCookieName("__Host-legalassistant.csrf", environment);
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = GetCookieSecurePolicy(environment);
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.HeaderName = "X-CSRF-TOKEN";
        });
    }

    private static CookieSecurePolicy GetCookieSecurePolicy(IHostEnvironment environment) =>
        environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

    private static string GetCookieName(string cookieName, IHostEnvironment environment)
    {
        if (environment.IsDevelopment() && cookieName.StartsWith("__Host-", StringComparison.Ordinal))
        {
            return cookieName["__Host-".Length..];
        }

        return cookieName;
    }
}
