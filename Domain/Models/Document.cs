namespace Domain.Models
{
    public class Document
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<DocumentChunk> Chunks { get; set; } = new();
        public float[]? Embedding { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
    }
}
