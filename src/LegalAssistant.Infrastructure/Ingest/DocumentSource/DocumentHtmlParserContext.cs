using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Documents;

public sealed record DocumentHtmlParserContext(
    string OriginalUrl,
    FetchedDocumentResponse InitialResponse,
    Func<string, string?, CancellationToken, Task<FetchedDocumentResponse?>> FetchAsync,
    ILogger Logger);
