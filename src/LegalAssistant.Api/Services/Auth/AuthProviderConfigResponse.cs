namespace LegalAssistant.Api.Services.Auth;

public sealed record AuthConfigResponse(
    ProviderConfigResponse Providers);

public sealed record ProviderConfigResponse(
    GoogleProviderConfigResponse Google);

public sealed record GoogleProviderConfigResponse(
    bool Enabled,
    string? LoginUrl);
