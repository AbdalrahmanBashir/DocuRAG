using Application.contracts;
using Application.Service;
using MediatR;

namespace Application.Features.Documents.ProcessDocument
{
    public class ProcessDocumentCommandHandler : IRequestHandler<ProcessDocumentCommand, ProcessDocumentResult>
    {
        private readonly IPdfProcessingService _pdfService;
        private readonly IEmbeddingService _embeddingService;
        private readonly IDocumentRepository _documentRepository;

        public ProcessDocumentCommandHandler(
            IPdfProcessingService pdfService,
            IEmbeddingService embeddingService,
            IDocumentRepository documentRepository)
        {
            _pdfService = pdfService;
            _embeddingService = embeddingService;
            _documentRepository = documentRepository;
        }
        public async Task<ProcessDocumentResult> Handle(ProcessDocumentCommand request, CancellationToken cancellationToken)
        {
            try
            {

                var documents = await _pdfService.ProcessPdfFilesAsync(Path.GetDirectoryName(request.FilePath)!);

                var document = documents.FirstOrDefault(d => d.FilePath == request.FilePath);

                if (document == null)
                {
                    throw new Exception($"Failed to process PDF file: {request.FilePath}");
                }

                // Generate embeddings for each chunk
                foreach (var chunk in document.Chunks)
                {
                    var chunkEmbedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content);
                    if (chunkEmbedding != null)
                    {
                        chunk.Embedding = chunkEmbedding;
                    }
                    else
                    {

                    }
                }

                // Generate embedding for the entire document
                var documentEmbedding = await _embeddingService.GenerateEmbeddingAsync(document.Content);
                if (documentEmbedding != null)
                {
                    document.Embedding = documentEmbedding;
                    document.ProcessedAt = DateTime.UtcNow;
                }
                else
                {
                }

                var savedDocument = await _documentRepository.AddAsync(document);

                var allDocs = await _documentRepository.GetAllAsync();

                return new ProcessDocumentResult(savedDocument.Id, true);
            }
            catch (Exception ex)
            {
                return new ProcessDocumentResult(string.Empty, false, ex.Message);
            }
        }
    }
}
