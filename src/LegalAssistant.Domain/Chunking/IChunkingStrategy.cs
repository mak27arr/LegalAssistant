namespace LegalAssistant.Domain.Chunking;

public interface IChunkingStrategy
{
    IEnumerable<ChunkRange> GetRanges(string text);
}
