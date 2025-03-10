namespace Application.Features.Documents.SearchDocuments;

public record DocumentSearchResult(
    string Content,
    string FilePath,
    float Score,
    string ConfidenceLevel,
    string? PageNumber = null,
    string? SectionTitle = null
);