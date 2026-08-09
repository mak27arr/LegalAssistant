using LegalAssistant.Core.Correlation;

namespace LegalAssistant.Api.Common;

public sealed class ApiCorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
}
