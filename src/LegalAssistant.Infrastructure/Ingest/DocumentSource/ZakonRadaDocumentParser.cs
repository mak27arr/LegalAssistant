using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Documents;

public sealed class ZakonRadaDocumentParser : IDocumentHtmlParser
{
    private readonly ILogger<ZakonRadaDocumentParser> _logger;

    public ZakonRadaDocumentParser(ILogger<ZakonRadaDocumentParser> logger)
    {
        _logger = logger;
    }

    public DocumentHtmlParserMatch Evaluate(Uri uri, string? mediaType)
    {
        if (!string.Equals(uri.Host, "zakon.rada.gov.ua", StringComparison.OrdinalIgnoreCase))
            return DocumentHtmlParserMatch.NoMatch("Host does not match zakon.rada.gov.ua.");

        var specificity = 95;
        var urlMatchScore = 60;
        var priority = 100;
        var confidence = 90;

        if (uri.AbsolutePath.StartsWith("/laws/show/", StringComparison.OrdinalIgnoreCase))
        {
            urlMatchScore = 100;
            confidence = 100;
        }
        else if (uri.AbsolutePath.StartsWith("/go/", StringComparison.OrdinalIgnoreCase))
        {
            urlMatchScore = 85;
            confidence = 95;
        }
        else if (uri.AbsolutePath.StartsWith("/laws/", StringComparison.OrdinalIgnoreCase))
        {
            urlMatchScore = 70;
            confidence = 85;
        }

        if (!string.IsNullOrWhiteSpace(mediaType) &&
            mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            confidence = Math.Min(100, confidence + 5);
        }

        return DocumentHtmlParserMatch.Match(
            specificity,
            urlMatchScore,
            priority,
            confidence,
            "Specialized parser for Verkhovna Rada law document pages.");
    }

    public async Task<string> ParseAsync(DocumentHtmlParserContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Parsing zakon.rada.gov.ua document. originalUrl={OriginalUrl} initialFinalUri={InitialFinalUri} initialBodyLength={InitialBodyLength}",
            context.OriginalUrl,
            context.InitialResponse.FinalUri,
            context.InitialResponse.Body.Length);

        var html = context.InitialResponse.Body;
        if (TryResolveDeferredHtmlUrl(html, context.InitialResponse.FinalUri, out var deferredUrl))
        {
            _logger.LogInformation(
                "Resolved deferred zakon.rada.gov.ua document url. originalUrl={OriginalUrl} initialFinalUri={InitialFinalUri} deferredUrl={DeferredUrl}",
                context.OriginalUrl,
                context.InitialResponse.FinalUri,
                deferredUrl);

            var deferredFetch = await context.FetchAsync(deferredUrl, context.InitialResponse.FinalUri, cancellationToken);
            if (deferredFetch is { IsHtml: true })
            {
                _logger.LogInformation(
                    "Fetched deferred zakon.rada.gov.ua document html. deferredFinalUri={DeferredFinalUri} mediaType={MediaType} bodyLength={BodyLength}",
                    deferredFetch.FinalUri,
                    deferredFetch.MediaType ?? "(none)",
                    deferredFetch.Body.Length);
                html = deferredFetch.Body;
            }
            else
            {
                _logger.LogWarning(
                    "Deferred zakon.rada.gov.ua fetch did not return html. deferredUrl={DeferredUrl}",
                    deferredUrl);
            }
        }
        else
        {
            _logger.LogWarning(
                "Could not resolve deferred zakon.rada.gov.ua document url. originalUrl={OriginalUrl} initialFinalUri={InitialFinalUri}",
                context.OriginalUrl,
                context.InitialResponse.FinalUri);
        }

        var extractedHtml = ExtractMainDocumentHtml(html, out var extractedNodeName, out var removedStructureCount);
        _logger.LogInformation(
            "Extracted main zakon.rada.gov.ua html fragment. originalUrl={OriginalUrl} node={Node} removedStructureCount={RemovedStructureCount} extractedHtmlLength={ExtractedHtmlLength}",
            context.OriginalUrl,
            extractedNodeName,
            removedStructureCount,
            extractedHtml.Length);
        return extractedHtml;
    }

    private static bool TryResolveDeferredHtmlUrl(string html, string baseUrl, out string deferredUrl)
    {
        deferredUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(html) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            return false;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var deferredNode = doc.DocumentNode.SelectSingleNode("//*[@id='Document' and @data-load]");
        var loadPath = deferredNode?.GetAttributeValue("data-load", string.Empty);
        if (string.IsNullOrWhiteSpace(loadPath))
            return false;

        if (!Uri.TryCreate(baseUri, loadPath, out var deferredUri))
            return false;

        deferredUrl = deferredUri.ToString();
        return true;
    }

    private static string ExtractMainDocumentHtml(string html, out string extractedNodeName, out int removedStructureCount)
    {
        extractedNodeName = "(none)";
        removedStructureCount = 0;

        if (string.IsNullOrWhiteSpace(html))
            return html;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var preferredNode = doc.DocumentNode.SelectSingleNode("//*[@id='Text']")
                            ?? doc.DocumentNode.SelectSingleNode("//*[@id='Document']");
        extractedNodeName = preferredNode?.Name ?? "(none)";

        var structureNodes = preferredNode?.SelectNodes(".//*[@id='Stru']");
        if (structureNodes is not null)
        {
            removedStructureCount = structureNodes.Count;
            foreach (var structureNode in structureNodes)
                structureNode.Remove();
        }

        return preferredNode?.OuterHtml ?? html;
    }
}
