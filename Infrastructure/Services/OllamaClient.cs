using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Domain.Models;
using System.Linq;

namespace Infrastructure.Services
{
    public class OllamaClient : IOllamaClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaClient> _logger;
        private readonly OllamaSettings _settings;

        public OllamaClient(
            HttpClient httpClient,
            IOptions<OllamaSettings> settings,
            ILogger<OllamaClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

            // Use OllamaEndpoint from settings, fallback to localhost if not provided.
            var baseUrl = !string.IsNullOrWhiteSpace(_settings.OllamaEndpoint)
                ? _settings.OllamaEndpoint
                : "http://localhost:11434";
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // Set reasonable timeout
        }

        public async Task<float[]?> GetEmbeddingsAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty text provided for embedding generation");
                return null;
            }

            // Truncate text if it exceeds max length
            if (text.Length > _settings.MaxTextLength)
            {
                _logger.LogWarning("Text exceeds maximum length of {MaxLength}, truncating from {OriginalLength} to {MaxLength}", 
                    _settings.MaxTextLength, text.Length, _settings.MaxTextLength);
                text = text[.._settings.MaxTextLength];
            }

            // Ensure minimum text length
            if (text.Length < _settings.MinTextLength)
            {
                _logger.LogWarning("Text length {Length} is shorter than minimum length of {MinLength}", 
                    text.Length, _settings.MinTextLength);
                return null;
            }

            for (int attempt = 1; attempt <= _settings.MaxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Attempt {Attempt}/{MaxRetries} to generate embedding for text of length {TextLength}", 
                        attempt, _settings.MaxRetries, text.Length);

                    // Use ModelName from settings
                    var modelName = _settings.ModelName;
                    var request = new { model = modelName, prompt = text };
                    var requestJson = JsonSerializer.Serialize(request);
                    _logger.LogDebug("Sending request to Ollama API. Model: {Model}, Text length: {TextLength}", 
                        modelName, text.Length);

                    var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                    using var response = await _httpClient.PostAsync("api/embeddings", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("API returned error {StatusCode}: {Error}. Request: Model={Model}, TextLength={TextLength}", 
                            response.StatusCode, errorContent, modelName, text.Length);

                        if (attempt < _settings.MaxRetries)
                        {
                            var delay = TimeSpan.FromMilliseconds(_settings.RetryDelayMs * attempt);
                            _logger.LogInformation("Retrying after {Delay}ms", delay.TotalMilliseconds);
                            await Task.Delay(delay);
                            continue;
                        }
                        return null;
                    }

                    var responseString = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("Received response from Ollama API: Length={Length}, Content={Content}", 
                        responseString.Length, responseString.Length > 1000 ? responseString[..1000] + "..." : responseString);

                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                            PropertyNameCaseInsensitive = true
                        };
                        
                        _logger.LogDebug("Attempting to deserialize response");
                        var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseString, options);
                        _logger.LogDebug("Deserialization successful. Result is null? {IsNull}", result == null);

                        if (result == null)
                        {
                            _logger.LogError("API response deserialized to null object");
                            return null;
                        }

                        _logger.LogDebug("Embedding array is null? {IsNull}", result.Embedding == null);
                        
                        if (result.Embedding == null)
                        {
                            _logger.LogError("API returned null embedding array");
                            return null;
                        }

                        _logger.LogDebug("Embedding array length: {Length}", result.Embedding.Length);

                        if (result.Embedding.Length == 0)
                        {
                            _logger.LogError("API returned empty embedding array");
                            return null;
                        }

                        // Validate embedding dimensions
                        if (result.Embedding.Length != 768)
                        {
                            _logger.LogError("API returned embedding with unexpected dimension: {Length}, expected 768", 
                                result.Embedding.Length);
                            return null;
                        }

                        // Validate embedding values
                        if (result.Embedding.Any(float.IsNaN) || result.Embedding.Any(float.IsInfinity))
                        {
                            _logger.LogError("API returned embedding with invalid values (NaN or Infinity)");
                            return null;
                        }

                        _logger.LogInformation("Successfully generated embedding. Dimension: {Dimension}, First few values: [{Values}]", 
                            result.Embedding.Length, 
                            string.Join(", ", result.Embedding.Take(5).Select(v => v.ToString("F6"))));
                        return result.Embedding;
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Error deserializing API response. Message: {Message}, Path: {Path}, Response: {Response}", 
                            ex.Message, ex.Path, responseString);
                            
                        if (attempt < _settings.MaxRetries)
                        {
                            var delay = TimeSpan.FromMilliseconds(_settings.RetryDelayMs * attempt);
                            _logger.LogInformation("Retrying after {Delay}ms", delay.TotalMilliseconds);
                            await Task.Delay(delay);
                            continue;
                        }
                        return null;
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "HTTP error during attempt {Attempt}/{MaxRetries}. Status: {Status}, Message: {Message}", 
                        attempt, _settings.MaxRetries, ex.StatusCode, ex.Message);
                    if (attempt < _settings.MaxRetries)
                    {
                        var delay = TimeSpan.FromMilliseconds(_settings.RetryDelayMs * attempt);
                        await Task.Delay(delay);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during embedding generation. Type: {ExceptionType}, Message: {Message}", 
                        ex.GetType().Name, ex.Message);
                    return null;
                }
            }

            _logger.LogError("Failed to generate embedding after {MaxRetries} attempts", _settings.MaxRetries);
            return null;
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                _logger.LogInformation("Checking Ollama API health");
                var embedding = await GetEmbeddingsAsync("test");
                var isHealthy = embedding != null && embedding.Length > 0;

                if (isHealthy)
                    _logger.LogInformation("Ollama API is healthy");
                else
                    _logger.LogWarning("Ollama API health check failed");

                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");
                return false;
            }
        }
    }

    internal class OllamaEmbeddingResponse
    {
        public float[]? Embedding { get; set; }
        public string? Model { get; set; }
        public int? Size { get; set; }
        public string? Error { get; set; }
    }
}
