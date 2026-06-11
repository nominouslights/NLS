using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Application.Clients;

public sealed record UpsertClientEmailTemplateCommand(
    Guid ClientId,
    ClientEmailTemplateType Type,
    string Subject,
    string Body) : ICommand;
