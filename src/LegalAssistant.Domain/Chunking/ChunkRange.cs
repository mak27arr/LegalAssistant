namespace LegalAssistant.Domain.Chunking;

public readonly record struct ChunkRange(int Start, int Length)
{
    public int EndExclusive => Start + Length;
}
