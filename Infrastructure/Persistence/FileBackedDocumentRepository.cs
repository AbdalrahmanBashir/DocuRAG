using System.Text.Json;
using Application.contracts;
using Domain.Models;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence
{
    public class FileBackedDocumentRepository : IDocumentRepository
    {
        private readonly Dictionary<string, Document> _documents = new();
        private readonly string _dataDirectory;
        private readonly bool _isReadOnly;

        public FileBackedDocumentRepository(
            IConfiguration configuration,
            string contentRootPath)
        {
            var dataPath = configuration.GetSection("Storage:DataPath").Value ?? "data";
            _dataDirectory = Path.Combine(contentRootPath, dataPath, "documents");
            _isReadOnly = configuration.GetValue<bool>("Storage:ReadOnlyMode");
            InitializeRepository();
        }

        private void InitializeRepository()
        {
            try
            {
                Directory.CreateDirectory(_dataDirectory);
                foreach (var file in Directory.GetFiles(_dataDirectory, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var document = JsonSerializer.Deserialize<Document>(json);
                        if (document != null)
                        {
                            _documents[document.Id] = document;
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to load document from file: {file}", ex);
                    }
                }

            }
            catch (Exception ex)
            {
                throw new Exception("Failed to initialize document repository", ex);
            }
        }

        private async Task SaveDocumentToDisk(Document document)
        {
            var filePath = Path.Combine(_dataDirectory, $"{document.Id}.json");
            var json = JsonSerializer.Serialize(document, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<Document> AddAsync(Document document)
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException("Repository is in read-only mode. New documents cannot be added in production.");
            }
            
            _documents[document.Id] = document;
            await SaveDocumentToDisk(document);
            return document;
        }

        public Task<IEnumerable<Document>> GetAllAsync()
        {
            return Task.FromResult(_documents.Values.AsEnumerable());
        }

        public Task<Document?> GetByIdAsync(string id)
        {
            _documents.TryGetValue(id, out var document);
            return Task.FromResult(document);
        }

        public Task<IEnumerable<Document>> SearchSimilarAsync(float[] embedding, float minSimilarity, int maxResults)
        {
            var results = new List<(Document Document, DocumentChunk Chunk, float Similarity)>();

            foreach (var document in _documents.Values)
            {

                if (document.Embedding != null)
                {
                    var docSimilarity = CosineSimilarity(embedding, document.Embedding);

                    if (docSimilarity >= minSimilarity)
                    {
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
                        }
                    }
                }
            }

            var topResults = results
                .OrderByDescending(x => x.Similarity)
                .Take(maxResults)
                .Select(x => new Document
                {
                    Id = x.Document.Id,
                    FilePath = x.Document.FilePath,
                    Content = x.Chunk.Content,
                    Chunks = new List<DocumentChunk> { x.Chunk },
                    Embedding = x.Document.Embedding,
                    CreatedAt = x.Document.CreatedAt,
                    ProcessedAt = x.Document.ProcessedAt
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

        public async Task UpdateEmbeddingAsync(string id, float[] embedding)
        {
            if (_documents.TryGetValue(id, out var document))
            {
                document.Embedding = embedding;
                document.ProcessedAt = DateTime.UtcNow;
                await SaveDocumentToDisk(document);

            }
        }
    }
}
