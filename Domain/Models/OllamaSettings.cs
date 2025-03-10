namespace Domain.Models
{
    public class OllamaSettings
    {
        public string OllamaEndpoint { get; set; } = string.Empty;
        public string PdfDirectory { get; set; } = "pdfs";
        public string ModelName { get; set; } = "nomic-embed-text";
        public int MaxTextLength { get; set; } = 8000;
        public int MinTextLength { get; set; } = 10;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 2000;
        public int ChunkSize { get; set; } = 1000;
        public int ChunkOverlap { get; set; } = 100;
        public float SimilarityThreshold { get; set; } = 0.5f;
        public int MaxResults { get; set; } = 3;
    }
}
