using LegalAssistant.Application.Common;
using LegalAssistant.Core.Correlation;

namespace LegalAssistant.Infrastructure.Common;

public sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
}
