namespace LegalAssistant.Application.Common;

public interface ICorrelationContext
{
    string CorrelationId { get; set; }
}
