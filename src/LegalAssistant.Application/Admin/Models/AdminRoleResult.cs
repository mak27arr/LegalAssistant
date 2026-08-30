namespace LegalAssistant.Application.Admin.Models;

public sealed record AdminRoleResult(
    Guid Id,
    string Name,
    string? Description);
