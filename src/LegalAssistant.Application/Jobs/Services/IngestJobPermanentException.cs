namespace LegalAssistant.Application.Jobs.Services;

public sealed class IngestJobPermanentException : Exception
{
    public IngestJobPermanentException(string message)
        : base(message)
    {
    }

    public IngestJobPermanentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
