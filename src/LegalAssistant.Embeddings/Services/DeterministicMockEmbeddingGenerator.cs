using System;

namespace LegalAssistant.Embeddings.Services;

public sealed class DeterministicMockEmbeddingGenerator : IEmbeddingGenerator
{
    public int Dimensions => 768;

    public float[] Generate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        var seed = text.GetHashCode();
        var random = new Random(seed);
        var vector = new float[Dimensions];
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)random.NextDouble();
        }

        return vector;
    }

    public async Task<float[]> GenerateAsync(string text)
    {
        return await Task.FromResult(Generate(text));
    }
}
