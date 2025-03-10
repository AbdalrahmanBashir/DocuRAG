using Application.Service;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Infrastructure.Services
{
    public class CachedEmbeddingService : IEmbeddingService
    {
        private readonly OllamaEmbeddingService _ollamaService;
        private readonly IMemoryCache _cache;
        private readonly string _cacheDirectory;
        private readonly bool _isReadOnly;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

        public CachedEmbeddingService(
            OllamaEmbeddingService ollamaService, 
            IMemoryCache cache,
            IConfiguration configuration,
            string contentRootPath)
        {
            _ollamaService = ollamaService;
            _cache = cache;
            _isReadOnly = configuration.GetValue<bool>("Storage:ReadOnlyMode");
            var cachePath = configuration.GetSection("Storage:CachePath").Value ?? "cache";
            _cacheDirectory = Path.Combine(contentRootPath, cachePath, "embeddings");
            Directory.CreateDirectory(_cacheDirectory);
        }

        public async Task<float[]?> GenerateEmbeddingAsync(string text)
        {
            var cacheKey = $"embedding_{ComputeHash(text)}";

            // Try memory cache first
            if (_cache.TryGetValue(cacheKey, out float[]? cachedEmbedding))
            {
                return cachedEmbedding;
            }

            // Try file cache
            var filePath = Path.Combine(_cacheDirectory, $"{cacheKey}.json");
            if (File.Exists(filePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var embedding = JsonSerializer.Deserialize<float[]>(json);
                    if (embedding != null)
                    {
                        _cache.Set(cacheKey, embedding, CacheDuration);
                        return embedding;
                    }
                }
                catch
                {
                    // If file read fails, continue to generate new embedding
                }
            }

            if (_isReadOnly)
            {
                throw new InvalidOperationException("Service is in read-only mode. New embeddings cannot be generated in production.");
            }

            // Generate new embedding
            var newEmbedding = await _ollamaService.GenerateEmbeddingAsync(text);
            if (newEmbedding != null)
            {
                _cache.Set(cacheKey, newEmbedding, CacheDuration);
                try
                {
                    var json = JsonSerializer.Serialize(newEmbedding);
                    await File.WriteAllTextAsync(filePath, json);
                }
                catch
                {
                    // If file write fails, we still have the embedding in memory
                }
            }

            return newEmbedding;
        }

        public async Task<IEnumerable<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts)
        {
            var results = new List<float[]?>();
            foreach (var text in texts)
            {
                results.Add(await GenerateEmbeddingAsync(text));
            }
            return results.Where(e => e != null).Cast<float[]>();
        }

        private static string ComputeHash(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
