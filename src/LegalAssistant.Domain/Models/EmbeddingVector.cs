
namespace LegalAssistant.Domain.Models
{
    public sealed class EmbeddingVector
    {
        private readonly float[] _values;

        public IReadOnlyList<float> Values => _values;

        public int Dimensions => _values.Length;

        public EmbeddingVector(IEnumerable<float> values)
        {
            _values = values.ToArray();

            if (_values.Length == 0)
                throw new ArgumentException("Embedding cannot be empty.");
        }
    }
}
