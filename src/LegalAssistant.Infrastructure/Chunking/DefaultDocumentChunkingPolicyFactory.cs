using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Domain.Chunking;
using Microsoft.Extensions.Configuration;

namespace LegalAssistant.Infrastructure.Chunking;

public sealed class DefaultDocumentChunkingPolicyFactory : IDocumentChunkingPolicyFactory
{
    private readonly IConfiguration _config;

    public DefaultDocumentChunkingPolicyFactory(IConfiguration config)
    {
        _config = config;
    }

    public IChunkingPolicy Create(ChunkingRunDescriptor descriptor)
    {
        // MVP: ignore descriptor params beyond selecting this single strategy.
        // Future: parse descriptor.ParamsJson and build appropriate policy.

        var chunkSize = _config.GetValue<int?>("Chunking:ChunkSize") ?? 2000;
        var maxChunkSize = _config.GetValue<int?>("Chunking:MaxChunkSize") ?? chunkSize;
        var pattern = _config.GetValue<string>("Chunking:ArticleRegex") ?? @"Стаття\s+\d+[\d¹²³]*[\w\-]*";

        var articleRegex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var regex = new RegexArticleChunkingStrategy(articleRegex, maxChunkSize: maxChunkSize);
        var fallback = new FixedSizeChunkingStrategy(chunkSize: chunkSize);
        return new RegexOrFixedChunkingPolicy(regex, fallback);
    }
}
