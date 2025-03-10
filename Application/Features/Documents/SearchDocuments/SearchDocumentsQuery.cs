using MediatR;

namespace Application.Features.Documents.SearchDocuments;

public record SearchDocumentsQuery(string Query) : IRequest<SearchDocumentsResult>;
