using Microsoft.Extensions.Options;

namespace LegalAssistant.Api.Configuration;

internal static class ConfigurationCheackwarning
{
    public static void LogIfIncomplete(IServiceProvider services)
    {
        var authOptions = services.GetRequiredService<IOptions<AuthOptions>>().Value;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        if (IsComplete(authOptions))
        {
            return;
        }

        logger.LogWarning(
            "Google authentication is not fully configured. Missing values: ClientId={HasClientId}, ClientSecret={HasClientSecret}, PublicApiBaseUrl={HasPublicApiBaseUrl}, SuccessRedirectUrl={HasSuccessRedirectUrl}, FailureRedirectUrl={HasFailureRedirectUrl}. Google sign-in will be unavailable until configuration is completed.",
            !string.IsNullOrWhiteSpace(authOptions.Google.ClientId),
            !string.IsNullOrWhiteSpace(authOptions.Google.ClientSecret),
            !string.IsNullOrWhiteSpace(authOptions.PublicApiBaseUrl),
            !string.IsNullOrWhiteSpace(authOptions.Frontend.SuccessRedirectUrl),
            !string.IsNullOrWhiteSpace(authOptions.Frontend.FailureRedirectUrl));
    }

    private static bool IsComplete(AuthOptions authOptions)
    {
        return !string.IsNullOrWhiteSpace(authOptions.Google.ClientId)
            && !string.IsNullOrWhiteSpace(authOptions.Google.ClientSecret)
            && !string.IsNullOrWhiteSpace(authOptions.PublicApiBaseUrl)
            && !string.IsNullOrWhiteSpace(authOptions.Frontend.SuccessRedirectUrl)
            && !string.IsNullOrWhiteSpace(authOptions.Frontend.FailureRedirectUrl);
    }
}
