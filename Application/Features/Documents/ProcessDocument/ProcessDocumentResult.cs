namespace Application.Features.Documents.ProcessDocument;

public record ProcessDocumentResult(string DocumentId, bool Success, string? Error = null);
