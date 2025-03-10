using Domain.Models;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class TextChunkingService
    {
        private readonly int _defaultChunkSize;
        private readonly int _defaultOverlap;

        public TextChunkingService(IOptions<OllamaSettings> options)
        {
            var settings = options.Value ?? throw new ArgumentNullException(nameof(options));
            // Use settings values if provided; otherwise, fallback to hardcoded defaults.
            _defaultChunkSize = settings.ChunkSize > 0 ? settings.ChunkSize : 1000;
            _defaultOverlap = settings.ChunkOverlap > 0 ? settings.ChunkOverlap : 200;
        }

        public List<DocumentChunk> ChunkText(string text, int pageNumber, int? chunkSize = null, int? overlap = null)
        {
            // Use provided chunkSize and overlap, or fall back to the defaults from settings.
            int size = chunkSize ?? _defaultChunkSize;
            int overlapValue = overlap ?? _defaultOverlap;

            if (string.IsNullOrWhiteSpace(text))
                return new List<DocumentChunk>();

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chunks = new List<DocumentChunk>();
            var currentChunkWords = new List<string>();
            var currentLength = 0;
            var chunkNumber = 1;

            for (int i = 0; i < words.Length; i++)
            {
                currentChunkWords.Add(words[i]);
                currentLength += words[i].Length + 1; // +1 for space

                // Check if we've reached the desired chunk size
                if (currentLength >= size)
                {
                    // Create a chunk with the current words
                    chunks.Add(new DocumentChunk
                    {
                        Content = string.Join(" ", currentChunkWords),
                        PageNumber = pageNumber,
                        ChunkNumber = chunkNumber++
                    });

                    // Retain overlapping words for the next chunk if overlap is configured
                    if (overlapValue > 0)
                    {
                        // Assume an average word length approximation: here using (overlapValue / 5)
                        var wordsToKeep = currentChunkWords.Skip(Math.Max(0, currentChunkWords.Count - (overlapValue / 5))).ToList();
                        currentChunkWords.Clear();
                        currentChunkWords.AddRange(wordsToKeep);
                        currentLength = wordsToKeep.Sum(w => w.Length + 1);
                    }
                    else
                    {
                        currentChunkWords.Clear();
                        currentLength = 0;
                    }
                }
            }

            // Add any remaining words as the final chunk
            if (currentChunkWords.Any())
            {
                chunks.Add(new DocumentChunk
                {
                    Content = string.Join(" ", currentChunkWords),
                    PageNumber = pageNumber,
                    ChunkNumber = chunkNumber
                });
            }

            return chunks;
        }
    }
}
