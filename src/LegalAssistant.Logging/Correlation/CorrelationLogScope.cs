using LegalAssistant.Core.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Logging.Correlation;

public sealed class CorrelationLogScope : IDisposable
{
    private readonly IDisposable? _scope;

    public CorrelationLogScope(string correlationId, IDisposable? scope)
    {
        CorrelationId = correlationId;
        _scope = scope;
    }

    public string CorrelationId { get; }

    public void Dispose() => _scope?.Dispose();
}
