namespace LegalAssistant.Api.Dtos.Auth;

public sealed record AuthMeResponse(
    string Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);

public sealed record AuthCsrfResponse(string Token);
