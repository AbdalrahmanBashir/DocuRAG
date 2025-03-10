using Application.contracts;
using Domain.Models;

namespace Infrastructure.Persistence
{
    public class InMemoryDocumentRepository : IDocumentRepository
    {
        private readonly Dictionary<string, Document> _documents = new();

        public InMemoryDocumentRepository()
        {
        }

        public Task<Document> AddAsync(Document document)
        {
            _documents[document.Id] = document;
            return Task.FromResult(document);
        }

        public Task<Document?> GetByIdAsync(string id)
        {
            _documents.TryGetValue(id, out var document);
            return Task.FromResult(document);
        }

        public Task<IEnumerable<Document>> GetAllAsync()
        {
            return Task.FromResult(_documents.Values.AsEnumerable());
        }

        public Task<IEnumerable<Document>> SearchSimilarAsync(float[] embedding, float minSimilarity, int maxResults)
        {

            var results = new List<(Document Document, DocumentChunk Chunk, float Similarity)>();

            foreach (var document in _documents.Values)
            {

                // Check document-level similarity
                if (document.Embedding != null)
                {
                    var docSimilarity = CosineSimilarity(embedding, document.Embedding);

                    if (docSimilarity >= minSimilarity)
                    {
                        // Add all chunks from highly similar documents
                        foreach (var chunk in document.Chunks)
                        {
                            if (chunk.Embedding != null)
                            {
                                var chunkSimilarity = CosineSimilarity(embedding, chunk.Embedding);

                                if (chunkSimilarity >= minSimilarity)
                                {
                                    results.Add((document, chunk, chunkSimilarity));
                                }
                            }
                            else
                            {
                            }
                        }
                    }
                }
                else
                {

                }
            }

            // Get the most relevant chunks
            var topResults = results
                .OrderByDescending(x => x.Similarity)
                .Take(maxResults)
                .Select(x =>
                {
                    // Create a new document with only the relevant chunk's content
                    return new Document
                    {
                        Id = x.Document.Id,
                        FilePath = x.Document.FilePath,
                        Content = x.Chunk.Content,
                        Chunks = new List<DocumentChunk> { x.Chunk },
                        Embedding = x.Document.Embedding,
                        CreatedAt = x.Document.CreatedAt,
                        ProcessedAt = x.Document.ProcessedAt
                    };
                });

            var resultsList = topResults.ToList();
            return Task.FromResult(resultsList.AsEnumerable());
        }

        private float CosineSimilarity(float[] v1, float[] v2)
        {
            if (v1.Length != v2.Length)
            {
                return 0;
            }

            float dotProduct = 0;
            float norm1 = 0;
            float norm2 = 0;

            for (int i = 0; i < v1.Length; i++)
            {
                dotProduct += v1[i] * v2[i];
                norm1 += v1[i] * v1[i];
                norm2 += v2[i] * v2[i];
            }

            if (norm1 == 0 || norm2 == 0)
            {
                return 0;
            }

            return dotProduct / (float)(Math.Sqrt(norm1) * Math.Sqrt(norm2));
        }

        public Task UpdateEmbeddingAsync(string id, float[] embedding)
        {
            if (_documents.TryGetValue(id, out var document))
            {
                document.Embedding = embedding;
                document.ProcessedAt = DateTime.UtcNow;

            }
            return Task.CompletedTask;
        }
    }

}
