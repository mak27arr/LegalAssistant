using LegalAssistant.Application.Common;

namespace LegalAssistant.Infrastructure.Common;

public sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
}
