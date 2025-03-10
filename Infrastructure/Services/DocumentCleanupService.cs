using Application.contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Infrastructure.Services
{
    public class DocumentCleanupService : BackgroundService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly ILogger<DocumentCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24);
        private readonly TimeSpan _documentMaxAge = TimeSpan.FromDays(30);

        public DocumentCleanupService(
            IDocumentRepository documentRepository,
            ILogger<DocumentCleanupService> logger)
        {
            _documentRepository = documentRepository;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupDocumentsAsync();
                    await Task.Delay(_cleanupInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during document cleanup");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task CleanupDocumentsAsync()
        {
            try
            {
                var documents = await _documentRepository.GetAllAsync();
                var oldDocuments = documents.Where(d =>
                    d.ProcessedAt.HasValue &&
                    DateTime.UtcNow - d.ProcessedAt.Value > _documentMaxAge);

                foreach (var document in oldDocuments)
                {
                    _logger.LogInformation("Cleaning up old document: {Id}, processed at {ProcessedAt}",
                        document.Id, document.ProcessedAt);

                    // Implementation depends on your repository's cleanup method
                    // await _documentRepository.DeleteAsync(document.Id);
                }

                // Force garbage collection after cleanup
                GC.Collect();
                GC.WaitForPendingFinalizers();

                _logger.LogInformation("Cleanup completed. Memory usage: {MemoryUsage}MB",
                    Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up documents");
            }
        }
    }
}
