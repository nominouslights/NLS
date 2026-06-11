using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Application.Clients;

public sealed record GetClientEmailTemplatesQuery(Guid ClientId)
    : IQuery<IReadOnlyList<ClientEmailTemplateResult>>;

public sealed record ClientEmailTemplateResult(
    Guid Id,
    ClientEmailTemplateType Type,
    string Subject,
    string Body,
    DateTime UpdatedAt);
