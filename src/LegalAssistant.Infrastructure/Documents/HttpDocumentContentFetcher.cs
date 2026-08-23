using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Documents.Services;
using Microsoft.Extensions.Options;

namespace LegalAssistant.Infrastructure.Documents;

public sealed class HttpDocumentContentFetcher : IDocumentContentFetcher
{
    private readonly HttpClient _httpClient;
    private readonly IHtmlToTextConverter _htmlToText;
    private readonly DocumentFetchOptions _options;
    private readonly IDocumentUrlValidator _urlValidator;

    public HttpDocumentContentFetcher(
        HttpClient httpClient,
        IHtmlToTextConverter htmlToText,
        IOptions<DocumentFetchOptions> options,
        IDocumentUrlValidator urlValidator)
    {
        _httpClient = httpClient;
        _htmlToText = htmlToText;
        _options = options.Value;
        _urlValidator = urlValidator;
    }

    public async Task<string?> FetchPlainTextAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        await _urlValidator.ValidateAsync(url, cancellationToken);

        using var timeoutCts = CreateTimeoutCancellationTokenSource(cancellationToken);
        using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
        if (!resp.IsSuccessStatusCode)
            return null;

        var contentType = resp.Content.Headers.ContentType?.MediaType;
        var body = await ReadBodyAsync(resp, timeoutCts.Token);

        if (!string.IsNullOrWhiteSpace(contentType) && !contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            return NormalizeText(body);

        return NormalizeText(_htmlToText.Convert(body));
    }

    private CancellationTokenSource CreateTimeoutCancellationTokenSource(CancellationToken cancellationToken)
    {
        if (_options.RequestTimeoutSeconds is null or <= 0)
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds.Value));
        return timeoutCts;
    }

    private async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (_options.MaxResponseBytes > 0)
        {
            var bytes = await ReadBytesWithLimitAsync(await response.Content.ReadAsStreamAsync(cancellationToken), _options.MaxResponseBytes, cancellationToken);
            var encoding = ResolveEncoding(response);
            return encoding.GetString(bytes);
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string NormalizeText(string text)
        => string.IsNullOrEmpty(text) ? text : text.Replace("\0", string.Empty);

    private static async Task<byte[]> ReadBytesWithLimitAsync(Stream stream, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (read <= 0)
                break;

            total += read;
            if (total > maxBytes)
                throw new ArgumentException($"Document content exceeds the configured maximum size of {maxBytes} bytes.");

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }

    private static Encoding ResolveEncoding(HttpResponseMessage response)
    {
        var charset = response.Content.Headers.ContentType?.CharSet;
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(charset);
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        return Encoding.UTF8;
    }
}
