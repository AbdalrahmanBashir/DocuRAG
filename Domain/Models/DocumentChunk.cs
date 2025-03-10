namespace Domain.Models
{
    public class DocumentChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public int ChunkNumber { get; set; }
        public float[]? Embedding { get; set; }
    }
}
