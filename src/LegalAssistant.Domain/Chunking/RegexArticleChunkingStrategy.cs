using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LegalAssistant.Domain.Chunking;

public sealed class RegexArticleChunkingStrategy : IChunkingStrategy
{
    private readonly Regex _articleRegex;
    private readonly int _maxChunkSize;

    public RegexArticleChunkingStrategy(Regex articleRegex, int maxChunkSize = 2000)
    {
        _articleRegex = articleRegex ?? throw new ArgumentNullException(nameof(articleRegex));
        if (maxChunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxChunkSize));
        _maxChunkSize = maxChunkSize;
    }

    public IEnumerable<ChunkRange> GetRanges(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        var matches = _articleRegex.Matches(text);
        if (matches.Count == 0) yield break;

        for (int i = 0; i < matches.Count; i++)
        {
            int start = matches[i].Index;
            int end = (i + 1 < matches.Count) ? matches[i + 1].Index : text.Length;
            int length = end - start;

            if (length <= 0)
                continue;

            // Split large articles into fixed-size segments
            int localIdx = 0;
            while (localIdx < length)
            {
                var len = Math.Min(_maxChunkSize, length - localIdx);
                yield return new ChunkRange(start + localIdx, len);
                localIdx += len;
            }
        }
    }
}
