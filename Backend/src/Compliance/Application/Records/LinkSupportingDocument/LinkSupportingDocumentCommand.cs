using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.LinkSupportingDocument;

/// <summary>Attaches a pointer to a supporting document (a URL, a storage key, another module's id) to a record.</summary>
public sealed record LinkSupportingDocumentCommand(Guid TenantId, Guid RecordId, SupportingLink Link, Guid? ActorId) : ICommand;
