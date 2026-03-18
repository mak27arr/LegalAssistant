namespace LegalAssistant.Embeddings.Services;

public interface IEmbeddingGenerator
{
    int Dimensions { get; }
    float[] Generate(string text);
}
