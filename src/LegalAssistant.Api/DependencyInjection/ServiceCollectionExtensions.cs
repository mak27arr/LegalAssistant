using LegalAssistant.Core.Correlation;
using LegalAssistant.Api.Services;
using LegalAssistant.Api.Common;
using LegalAssistant.Api.Configuration;
using LegalAssistant.Api.Services.Auth;
using LegalAssistant.Infrastructure.Ask;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace LegalAssistant.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private const string ExternalScheme = "External";
    private const string CorsPolicyName = "Frontend";

    public static IServiceCollection AddApiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationContext, ApiCorrelationContext>();
        services.AddSingleton<LegalAssistant.Application.Ask.IAskJobEventFanout, InMemoryAskJobEventFanout>();
        services.AddScoped<IAskJobEventStreamService, AskJobEventStreamService>();
        services.AddHostedService<RabbitMqAskJobEventRelayHostedService>();
        services.AddHostedService<RoleBootstrapper>();
        services.AddScoped<IAuthUserProvisioningService, AuthUserProvisioningService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        ConfigureAuthentication(services, authOptions);
        ConfigureAuthorization(services);
        ConfigureCors(services, configuration);

        return services;
    }

    public static IServiceCollection AddApiSwaggerSecurity(this IServiceCollection services)
    {
        return services;
    }

    public static string GetFrontendCorsPolicyName() => CorsPolicyName;

    private static void ConfigureAuthentication(IServiceCollection services, AuthOptions authOptions)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddCookie(ExternalScheme, options =>
            {
                options.Cookie.Name = "legalassistant.external";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddGoogle("Google", options =>
            {
                options.SignInScheme = ExternalScheme;
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
            });
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
}
