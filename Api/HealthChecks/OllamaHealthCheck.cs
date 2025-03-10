using Application.Service;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.HealthChecks
{
    public class OllamaHealthCheck : IHealthCheck
    {
        private readonly IEmbeddingService _embeddingService;

        public OllamaHealthCheck(IEmbeddingService embeddingService)
        {
            _embeddingService = embeddingService;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to generate an embedding for a simple test string
                var embedding = await _embeddingService.GenerateEmbeddingAsync("test");

                if (embedding != null && embedding.Length > 0)
                {
                    return HealthCheckResult.Healthy("Ollama service is responding normally");
                }

                return HealthCheckResult.Degraded("Ollama service returned empty embedding");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Ollama service is not responding", ex);
            }
        }
    }
}
