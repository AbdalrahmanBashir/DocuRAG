using Application.contracts;
using Application.Service;
using MediatR;

namespace Application.Features.Documents.SearchDocuments
{
    public class SearchDocumentsQueryHandler : IRequestHandler<SearchDocumentsQuery, SearchDocumentsResult>
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IDocumentRepository _documentRepository;

        public SearchDocumentsQueryHandler(
            IEmbeddingService embeddingService,
            IDocumentRepository documentRepository)
        {
            _embeddingService = embeddingService;
            _documentRepository = documentRepository;
        }
        public async Task<SearchDocumentsResult> Handle(SearchDocumentsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Query);
                if (queryEmbedding == null)
                {
                    return new SearchDocumentsResult(Array.Empty<DocumentSearchResult>());
                }

                var similarDocuments = await _documentRepository.SearchSimilarAsync(queryEmbedding, 0.3f, 5);
                var results = similarDocuments.Select(doc => new DocumentSearchResult(
                    Content: doc.Content,
                    FilePath: doc.FilePath,
                    Score: CalculateScore(queryEmbedding, doc.Embedding!),
                    ConfidenceLevel: GetConfidenceLevel(CalculateScore(queryEmbedding, doc.Embedding!))
                ));
                return new SearchDocumentsResult(results);
            }
            catch (Exception ex)
            {
                return new SearchDocumentsResult(Array.Empty<DocumentSearchResult>());
            }
        }

        private float CalculateScore(float[] queryEmbedding, float[] documentEmbedding)
        {
            float dotProduct = 0;
            float normA = 0;
            float normB = 0;

            for (int i = 0; i < queryEmbedding.Length; i++)
            {
                dotProduct += queryEmbedding[i] * documentEmbedding[i];
                normA += queryEmbedding[i] * queryEmbedding[i];
                normB += documentEmbedding[i] * documentEmbedding[i];
            }

            return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private string GetConfidenceLevel(float score)
        {
            return score switch
            {
                >= 0.8f => "Very High",
                >= 0.6f => "High",
                >= 0.4f => "Moderate",
                >= 0.3f => "Low",
                _ => "Very Low"
            };
        }
    }
}
