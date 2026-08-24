using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Documents.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegalAssistant.Infrastructure.Documents;

public sealed class HttpDocumentContentFetcher : IDocumentContentFetcher
{
    private readonly HttpClient _httpClient;
    private readonly IHtmlToTextConverter _htmlToText;
    private readonly ILogger<HttpDocumentContentFetcher> _logger;
    private readonly DocumentFetchOptions _options;
    private readonly IDocumentUrlValidator _urlValidator;
    private readonly IReadOnlyList<IDocumentHtmlParser> _parsers;

    public HttpDocumentContentFetcher(
        HttpClient httpClient,
        IHtmlToTextConverter htmlToText,
        ILogger<HttpDocumentContentFetcher> logger,
        IOptions<DocumentFetchOptions> options,
        IDocumentUrlValidator urlValidator,
        IEnumerable<IDocumentHtmlParser> parsers)
    {
        _httpClient = httpClient;
        _htmlToText = htmlToText;
        _logger = logger;
        _options = options.Value;
        _urlValidator = urlValidator;
        _parsers = parsers?.ToArray() ?? Array.Empty<IDocumentHtmlParser>();
    }

    public async Task<string?> FetchPlainTextAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        _logger.LogInformation("Starting document fetch. originalUrl={OriginalUrl}", url);
        await _urlValidator.ValidateAsync(url, cancellationToken);

        using var timeoutCts = CreateTimeoutCancellationTokenSource(cancellationToken);
        var initialFetch = await FetchResponseAsync(url, referer: null, timeoutCts.Token);
        if (initialFetch is null)
        {
            _logger.LogWarning("Document fetch returned no response body. originalUrl={OriginalUrl}", url);
            return null;
        }

        if (!initialFetch.IsHtml)
        {
            _logger.LogInformation(
                "Returning non-html document content without parser. originalUrl={OriginalUrl} finalUri={FinalUri} mediaType={MediaType} bodyLength={BodyLength}",
                url,
                initialFetch.FinalUri,
                initialFetch.MediaType ?? "(none)",
                initialFetch.Body.Length);
            return NormalizeText(initialFetch.Body);
        }

        var html = initialFetch.Body;
        if (Uri.TryCreate(initialFetch.FinalUri, UriKind.Absolute, out var finalUri))
        {
            var parserEvaluations = _parsers
                .Select(parser => new
                {
                    Parser = parser,
                    Match = parser.Evaluate(finalUri, initialFetch.MediaType)
                })
                .ToList();

            foreach (var evaluation in parserEvaluations)
            {
                _logger.LogInformation(
                    "Evaluated document html parser. parser={Parser} finalUri={FinalUri} isMatch={IsMatch} priority={Priority} specificity={Specificity} urlMatchScore={UrlMatchScore} confidence={Confidence} reason={Reason}",
                    evaluation.Parser.GetType().Name,
                    finalUri,
                    evaluation.Match.IsMatch,
                    evaluation.Match.Priority,
                    evaluation.Match.Specificity,
                    evaluation.Match.UrlMatchScore,
                    evaluation.Match.Confidence,
                    evaluation.Match.Reason ?? "(none)");
            }

            var parserSelection = parserEvaluations
                .Where(x => x.Match.IsMatch)
                .OrderByDescending(x => x.Match.Priority)
                .ThenByDescending(x => x.Match.Specificity)
                .ThenByDescending(x => x.Match.UrlMatchScore)
                .ThenByDescending(x => x.Match.Confidence)
                .FirstOrDefault();

            if (parserSelection is not null)
            {
                _logger.LogInformation(
                    "Selected document html parser. parser={Parser} finalUri={FinalUri} priority={Priority} specificity={Specificity} urlMatchScore={UrlMatchScore} confidence={Confidence} reason={Reason}",
                    parserSelection.Parser.GetType().Name,
                    finalUri,
                    parserSelection.Match.Priority,
                    parserSelection.Match.Specificity,
                    parserSelection.Match.UrlMatchScore,
                    parserSelection.Match.Confidence,
                    parserSelection.Match.Reason ?? "(none)");

                html = await parserSelection.Parser.ParseAsync(
                    new DocumentHtmlParserContext(url, initialFetch, FetchResponseAsync, _logger),
                    timeoutCts.Token);
            }
            else
            {
                _logger.LogWarning(
                    "No document html parser matched html response. originalUrl={OriginalUrl} finalUri={FinalUri} mediaType={MediaType} bodyLength={BodyLength}",
                    url,
                    finalUri,
                    initialFetch.MediaType ?? "(none)",
                    initialFetch.Body.Length);
            }
        }
        else
        {
            _logger.LogWarning(
                "Could not parse final response uri for parser selection. originalUrl={OriginalUrl} finalUri={FinalUri}",
                url,
                initialFetch.FinalUri);
        }

        var plainText = NormalizeText(_htmlToText.Convert(html));
        _logger.LogInformation(
            "Converted html document to plain text. originalUrl={OriginalUrl} finalUri={FinalUri} htmlLength={HtmlLength} textLength={TextLength}",
            url,
            initialFetch.FinalUri,
            html.Length,
            plainText.Length);
        return plainText;
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

    private async Task<string> TryReadFailureBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await ReadBodyAsync(response, cancellationToken);
            return Abbreviate(NormalizeText(body), 600);
        }
        catch (Exception ex)
        {
            return $"<failed to read body: {ex.GetType().Name}: {ex.Message}>";
        }
    }

    private static string NormalizeText(string text)
        => string.IsNullOrEmpty(text) ? text : text.Replace("\0", string.Empty);

    private static string Abbreviate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }

    private async Task<FetchedDocumentResponse?> FetchResponseAsync(string url, string? referer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            request.Headers.Referrer = refererUri;

        using var resp = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        var finalUri = resp.RequestMessage?.RequestUri?.ToString() ?? url;
        var contentType = resp.Content.Headers.ContentType?.ToString() ?? "(none)";
        _logger.LogInformation(
            "Fetched document response. url={Url} finalUri={FinalUri} statusCode={StatusCode} contentType={ContentType}",
            url,
            finalUri,
            (int)resp.StatusCode,
            contentType);

        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await TryReadFailureBodyAsync(resp, cancellationToken);
            _logger.LogWarning(
                "Document fetch returned non-success status. url={Url} finalUri={FinalUri} statusCode={StatusCode} contentType={ContentType} bodyPreview={BodyPreview}",
                url,
                finalUri,
                (int)resp.StatusCode,
                contentType,
                errorBody);
            return null;
        }

        var body = await ReadBodyAsync(resp, cancellationToken);
        var mediaType = resp.Content.Headers.ContentType?.MediaType;
        return new FetchedDocumentResponse(finalUri, body, mediaType);
    }

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
