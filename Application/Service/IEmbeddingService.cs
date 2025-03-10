namespace Application.Service
{
    public interface IEmbeddingService
    {
        Task<float[]?> GenerateEmbeddingAsync(string text);
        Task<IEnumerable<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts);
    }
}
