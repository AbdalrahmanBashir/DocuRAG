using Domain.Models;

namespace Application.contracts
{
    public interface IDocumentRepository
    {
        Task<Document> AddAsync(Document document);
        Task<Document?> GetByIdAsync(string id);
        Task<IEnumerable<Document>> GetAllAsync();
        Task<IEnumerable<Document>> SearchSimilarAsync(float[] embedding, float minSimilarity, int maxResults);
        Task UpdateEmbeddingAsync(string id, float[] embedding);
    }
}
