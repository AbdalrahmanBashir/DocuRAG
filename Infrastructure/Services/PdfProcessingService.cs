using Application.Service;
using Domain.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace Infrastructure.Services
{
    public class PdfProcessingService : IPdfProcessingService
    {
        private readonly ILogger<PdfProcessingService> _logger;
        private readonly TextChunkingService _chunkingService;

        public PdfProcessingService(ILogger<PdfProcessingService> logger, TextChunkingService chunkingService)
        {
            _logger = logger;
            _chunkingService = chunkingService;
        }

        public async Task<IEnumerable<Document>> ProcessPdfFilesAsync(string directory)
        {
            var documents = new List<Document>();
            var pdfFiles = Directory.GetFiles(directory, "*.pdf");

            foreach (var filePath in pdfFiles)
            {
                try
                {
                    var document = await ProcessPdfFileAsync(filePath);
                    if (document != null)
                    {
                        documents.Add(document);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing PDF file: {FilePath}", filePath);
                }
            }

            return documents;
        }

        private async Task<Document?> ProcessPdfFileAsync(string filePath)
        {
            var document = new Document { FilePath = filePath };
            var allText = new System.Text.StringBuilder();

            using (var pdfDocument = PdfDocument.Open(filePath))
            {
                for (var i = 1; i <= pdfDocument.NumberOfPages; i++)
                {
                    var page = pdfDocument.GetPage(i);
                    var pageText = page.Text;
                    allText.AppendLine(pageText);

                    // Create chunks for this page using the injected TextChunkingService,
                    var chunks = _chunkingService.ChunkText(pageText, i);
                    document.Chunks.AddRange(chunks);
                }
            }

            document.Content = allText.ToString();
            return document;
        }

        public async Task<string> ExtractTextFromPdfAsync(string filePath)
        {
            var document = await ProcessPdfFileAsync(filePath);
            return document?.Content ?? string.Empty;
        }
    }
}
