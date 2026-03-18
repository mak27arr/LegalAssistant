namespace LegalAssistant.Embeddings.Services;

public interface IEmbeddingGenerator
{
    int Dimensions { get; }
    float[] Generate(string text);

    Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(Generate(text));
}
