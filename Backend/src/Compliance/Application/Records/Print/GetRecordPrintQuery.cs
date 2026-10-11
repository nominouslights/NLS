using NorthernLink.Compliance.Application.Printing;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Print;

/// <summary>Renders the record's print template (its cover sheet today) as a PDF.</summary>
public sealed record GetRecordPrintQuery(Guid TenantId, Guid RecordId) : IQuery<RecordPrintResult>;
