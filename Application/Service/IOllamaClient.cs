namespace Application.Service
{
    public interface IOllamaClient
    {
        Task<float[]?> GetEmbeddingsAsync(string text);
        Task<bool> IsHealthyAsync();
    }
}
