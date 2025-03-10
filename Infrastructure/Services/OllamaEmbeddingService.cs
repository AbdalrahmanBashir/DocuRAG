using Application.Service;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class OllamaEmbeddingService : IEmbeddingService
    {
        private readonly IOllamaClient _ollamaClient;
        private readonly ILogger<OllamaEmbeddingService> _logger;
        private readonly SemaphoreSlim _semaphore;
        private const int MaxConcurrentRequests = 3;

        public OllamaEmbeddingService(
            IOllamaClient ollamaClient,
            ILogger<OllamaEmbeddingService> logger)
        {
            _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _semaphore = new SemaphoreSlim(MaxConcurrentRequests);
        }

        public async Task<float[]?> GenerateEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty text provided for embedding generation");
                return null;
            }

            try
            {
                _logger.LogInformation("Attempting to acquire semaphore for embedding generation");
                await _semaphore.WaitAsync();
                _logger.LogInformation("Semaphore acquired, generating embedding for text of length: {TextLength}", text.Length);
                
                var embedding = await _ollamaClient.GetEmbeddingsAsync(text);
                
                if (embedding == null)
                {
                    _logger.LogError("Embedding generation returned null");
                    return null;
                }
                
                _logger.LogInformation("Successfully generated embedding with dimension: {Dimension}", embedding.Length);
                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding. Exception type: {ExceptionType}, Message: {Message}, Stack trace: {StackTrace}", 
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return null;
            }
            finally
            {
                _semaphore.Release();
                _logger.LogInformation("Semaphore released after embedding generation attempt");
            }
        }

        public async Task<IEnumerable<float[]?>> GenerateEmbeddingsAsync(IEnumerable<string> texts)
        {
            if (texts == null)
                throw new ArgumentNullException(nameof(texts));

            var textsList = texts.ToList();
            _logger.LogInformation("Starting batch embedding generation for {Count} texts", textsList.Count);

            var tasks = new List<Task<float[]?>>();
            foreach (var text in textsList)
            {
                tasks.Add(GenerateEmbeddingAsync(text));
            }

            try
            {
                _logger.LogInformation("Waiting for all embedding tasks to complete");
                var results = await Task.WhenAll(tasks);
                _logger.LogInformation("Batch embedding generation completed. Successful embeddings: {SuccessCount}/{TotalCount}", 
                    results.Count(r => r != null), results.Length);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embeddings batch. Exception type: {ExceptionType}, Message: {Message}, Stack trace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return Array.Empty<float[]>();
            }
        }
    }
}
