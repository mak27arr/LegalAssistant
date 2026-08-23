namespace LegalAssistant.Api.Services;

public interface IAskJobEventStreamService
{
    Task StreamAsync(Guid jobId, HttpContext httpContext, CancellationToken cancellationToken = default);
}
