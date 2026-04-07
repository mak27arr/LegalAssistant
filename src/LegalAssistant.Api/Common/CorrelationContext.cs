using LegalAssistant.Application.Common;

namespace LegalAssistant.Api.Common;

public sealed class ApiCorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
}
