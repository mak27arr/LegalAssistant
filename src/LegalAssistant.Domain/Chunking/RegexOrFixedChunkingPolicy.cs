using System;
using System.Collections.Generic;

namespace LegalAssistant.Domain.Chunking;

public sealed class RegexOrFixedChunkingPolicy : IChunkingPolicy
{
    private readonly IChunkingStrategy _regexStrategy;
    private readonly IChunkingStrategy _fallbackStrategy;

    public RegexOrFixedChunkingPolicy(IChunkingStrategy regexStrategy, IChunkingStrategy fallbackStrategy)
    {
        _regexStrategy = regexStrategy ?? throw new ArgumentNullException(nameof(regexStrategy));
        _fallbackStrategy = fallbackStrategy ?? throw new ArgumentNullException(nameof(fallbackStrategy));
    }

    public IEnumerable<ChunkRange> GetRanges(string text)
    {
        var primary = _regexStrategy.GetRanges(text);

        using var enumerator = primary.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            foreach (var r in _fallbackStrategy.GetRanges(text))
                yield return r;

            yield break;
        }

        yield return enumerator.Current;
        while (enumerator.MoveNext())
            yield return enumerator.Current;
    }
}
