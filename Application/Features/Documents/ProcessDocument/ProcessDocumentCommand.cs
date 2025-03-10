using MediatR;

namespace Application.Features.Documents.ProcessDocument;

public record ProcessDocumentCommand(string FilePath) : IRequest<ProcessDocumentResult>;
