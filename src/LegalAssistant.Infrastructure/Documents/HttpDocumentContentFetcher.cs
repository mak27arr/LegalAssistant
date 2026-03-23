using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Documents.Services;

namespace LegalAssistant.Infrastructure.Documents;

public sealed class HttpDocumentContentFetcher : IDocumentContentFetcher
{
    private readonly HttpClient _httpClient;
    private readonly IHtmlToTextConverter _htmlToText;

    public HttpDocumentContentFetcher(HttpClient httpClient, IHtmlToTextConverter htmlToText)
    {
        _httpClient = httpClient;
        _htmlToText = htmlToText;
    }

    public async Task<string?> FetchPlainTextAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        using var resp = await _httpClient.GetAsync(url, cancellationToken);
        if (!resp.IsSuccessStatusCode)
            return null;

        var contentType = resp.Content.Headers.ContentType?.MediaType;
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(contentType) && !contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            return body;

        return _htmlToText.Convert(body);
    }
}
