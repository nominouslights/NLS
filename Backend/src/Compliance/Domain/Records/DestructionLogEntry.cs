using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Records;

/// <summary>
/// One line of the destruction log: which record was destroyed, when, by whom, how. Append-only
/// and never edited — an auditor reads it to confirm that a record missing from the binder was
/// disposed on purpose at the end of its retention period (NL-SP-02 s. 11; HTA s. 318.10(2)).
/// A plain tenant-scoped <see cref="Entity"/>, written in the same save as the record's
/// Disposed transition, so the two can never disagree.
/// </summary>
public sealed class DestructionLogEntry : Entity, ITenantScoped
{
    public const int MethodMaxLength = 64;
    public const int NoteMaxLength = 1000;

    private DestructionLogEntry()
    {
        // EF Core materialization only.
        DocumentNumber = null!;
        Method = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid RecordId { get; private set; }
    public string DocumentNumber { get; private set; }
    public ComplianceRecordType RecordType { get; private set; }
    public DateTimeOffset DisposedAtUtc { get; private set; }
    public Guid? DisposedBy { get; private set; }

    /// <summary>"shredded", "deleted", "returned-to-driver" — free text, trimmed.</summary>
    public string Method { get; private set; }

    public string? Note { get; private set; }

    public static DestructionLogEntry For(ComplianceRecord record, DateTimeOffset disposedAtUtc, Guid? disposedBy, string method, string? note) =>
        new()
        {
            TenantId = record.TenantId,
            RecordId = record.Id,
            DocumentNumber = record.DocumentNumber,
            RecordType = record.RecordType,
            DisposedAtUtc = disposedAtUtc,
            DisposedBy = disposedBy,
            Method = method,
            Note = note,
        };
}
