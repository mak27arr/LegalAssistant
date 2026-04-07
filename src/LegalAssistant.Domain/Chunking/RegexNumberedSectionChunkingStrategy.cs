using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LegalAssistant.Domain.Chunking;

public sealed class RegexNumberedSectionChunkingStrategy : IChunkingStrategy
{
    private readonly Regex _sectionRegex;
    private readonly int _maxChunkSize;

    public RegexNumberedSectionChunkingStrategy(Regex sectionRegex, int maxChunkSize = 2000)
    {
        _sectionRegex = sectionRegex ?? throw new ArgumentNullException(nameof(sectionRegex));
        if (maxChunkSize <= 0) 
            throw new ArgumentOutOfRangeException(nameof(maxChunkSize));
        _maxChunkSize = maxChunkSize;
    }

    public IEnumerable<ChunkRange> GetRanges(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        var matches = _sectionRegex.Matches(text);
        if (matches.Count == 0) yield break;

        for (int i = 0; i < matches.Count; i++)
        {
            int start = matches[i].Index;
            int end = (i + 1 < matches.Count) ? matches[i + 1].Index : text.Length;
            int length = end - start;

            if (length <= 0)
                continue;

            foreach (var r in SplitByParagraphsOrFixed(text, start, length))
                yield return r;
        }
    }

    private IEnumerable<ChunkRange> SplitByParagraphsOrFixed(string text, int start, int length)
    {
        if (length <= _maxChunkSize)
        {
            yield return new ChunkRange(start, length);
            yield break;
        }

        int sectionEnd = start + length;
        int pos = start;

        while (pos < sectionEnd)
        {
            int desiredEnd = Math.Min(pos + _maxChunkSize, sectionEnd);

            int breakAt = text.LastIndexOf("\n\n", desiredEnd, desiredEnd - pos, StringComparison.Ordinal);
            if (breakAt >= pos)
            {
                int chunkLen = (breakAt + 2) - pos;
                if (chunkLen > 0)
                {
                    yield return new ChunkRange(pos, chunkLen);
                    pos += chunkLen;
                    continue;
                }
            }

            yield return new ChunkRange(pos, desiredEnd - pos);
            pos = desiredEnd;
        }
    }
}
