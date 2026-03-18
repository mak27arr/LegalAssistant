using System;
using System.Collections.Generic;

namespace LegalAssistant.Domain.Chunking;

public sealed class FixedSizeChunkingStrategy : IChunkingStrategy
{
    private readonly int _chunkSize;

    public FixedSizeChunkingStrategy(int chunkSize = 2000)
    {
        if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        _chunkSize = chunkSize;
    }

    public IEnumerable<ChunkRange> GetRanges(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        int idx = 0;
        while (idx < text.Length)
        {
            var len = Math.Min(_chunkSize, text.Length - idx);
            yield return new ChunkRange(idx, len);
            idx += len;
        }
    }
}
