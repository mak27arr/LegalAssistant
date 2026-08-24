using System;
using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Infrastructure.Documents;

public interface IDocumentHtmlParser
{
    DocumentHtmlParserMatch Evaluate(Uri uri, string? mediaType);

    Task<string> ParseAsync(DocumentHtmlParserContext context, CancellationToken cancellationToken = default);
}
