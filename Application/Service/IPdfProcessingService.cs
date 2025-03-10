using Domain.Models;

namespace Application.Service
{
    public interface IPdfProcessingService
    {
        Task<IEnumerable<Document>> ProcessPdfFilesAsync(string directory);
        Task<string> ExtractTextFromPdfAsync(string filePath);
    }
}
