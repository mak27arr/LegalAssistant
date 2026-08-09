namespace LegalAssistant.Core.Correlation
{
    public interface ICorrelationContext
    {
        string CorrelationId { get; set; }
    }
}
