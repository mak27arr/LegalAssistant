namespace LegalAssistant.Domain.Chunking;

public interface IChunkingPolicy
{
    IEnumerable<ChunkRange> GetRanges(string text);
}
