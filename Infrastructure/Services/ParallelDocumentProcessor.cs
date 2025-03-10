using System.Collections.Concurrent;
using Application.Service;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class ParallelDocumentProcessor
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<ParallelDocumentProcessor> _logger;
        private readonly int _maxDegreeOfParallelism;

        public ParallelDocumentProcessor(
            IEmbeddingService embeddingService,
            ILogger<ParallelDocumentProcessor> logger,
            int maxDegreeOfParallelism = 3)
        {
            _embeddingService = embeddingService;
            _logger = logger;
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        public async Task ProcessDocumentAsync(Document document)
        {
            _logger.LogInformation("Starting parallel processing of document {Id} with {ChunkCount} chunks",
                document.Id, document.Chunks.Count);

            var chunks = new ConcurrentBag<DocumentChunk>(document.Chunks);
            var processedChunks = new ConcurrentBag<DocumentChunk>();

            // Process chunks in parallel
            await Parallel.ForEachAsync(
                chunks,
                new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism },
                async (chunk, ct) =>
                {
                    try
                    {
                        var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content);
                        if (embedding != null)
                        {
                            chunk.Embedding = embedding;
                            processedChunks.Add(chunk);
                            _logger.LogInformation("Generated embedding for chunk {ChunkNumber} of page {PageNumber}",
                                chunk.ChunkNumber, chunk.PageNumber);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing chunk {ChunkNumber} of page {PageNumber}",
                            chunk.ChunkNumber, chunk.PageNumber);
                    }
                });

            // Generate document-level embedding from concatenated successful chunks
            try
            {
                var successfulChunks = processedChunks.OrderBy(c => c.PageNumber).ThenBy(c => c.ChunkNumber);
                var fullText = string.Join(" ", successfulChunks.Select(c => c.Content));
                var documentEmbedding = await _embeddingService.GenerateEmbeddingAsync(fullText);

                if (documentEmbedding != null)
                {
                    document.Embedding = documentEmbedding;
                    document.ProcessedAt = DateTime.UtcNow;
                    _logger.LogInformation("Generated embedding for entire document {Id}", document.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating document-level embedding for document {Id}", document.Id);
            }

            // Update document chunks with processed ones
            document.Chunks = processedChunks.OrderBy(c => c.PageNumber).ThenBy(c => c.ChunkNumber).ToList();
            _logger.LogInformation("Completed parallel processing of document {Id} with {ProcessedChunkCount} processed chunks",
                document.Id, processedChunks.Count);
        }
    }
}
