using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.BookeoImports;

/// <summary>
/// One uploaded Bookeo booking report: the normalized rows it parsed to (never the raw file, and
/// never a tax figure — <c>GST</c> and <c>Total net</c> are dropped at parse time) plus the hash
/// of the plan its preview showed. A commit recomputes the plan from <see cref="ParsedRowsJson"/>
/// and the current database, and applies only when the hash still matches.
/// <para>
/// A plain tenant-scoped row, not an aggregate: nothing projects it and nothing journals it — the
/// trips and manifests the commit writes carry the audit trail. <see cref="CommittedAtUtc"/> is
/// mapped as a concurrency token, so two simultaneous confirms of one preview cannot both apply.
/// </para>
/// </summary>
public sealed class BookeoImportBatch : Entity, ITenantScoped
{
    private BookeoImportBatch()
    {
        FileName = null!;
        UploadedBy = null!;
        ParsedRowsJson = null!;
        PlanHash = null!;
        SummaryJson = null!;
    }

    public Guid TenantId { get; private set; }
    public string FileName { get; private set; }
    public string UploadedBy { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }

    /// <summary>The normalized rows (jsonb). Tax-free by construction: the row type has no tax member.</summary>
    public string ParsedRowsJson { get; private set; }

    public string PlanHash { get; private set; }

    /// <summary>The preview's summary counts (jsonb) — what the History tab lists.</summary>
    public string SummaryJson { get; private set; }

    public DateTimeOffset? CommittedAtUtc { get; private set; }
    public string? CommittedBy { get; private set; }

    public bool IsCommitted => CommittedAtUtc is not null;

    public static BookeoImportBatch Create(
        Guid tenantId,
        string fileName,
        string uploadedBy,
        DateTimeOffset uploadedAtUtc,
        string parsedRowsJson,
        string planHash,
        string summaryJson) => new()
        {
            TenantId = tenantId,
            FileName = Truncate(string.IsNullOrWhiteSpace(fileName) ? "bookeo.xls" : fileName.Trim(), 260),
            UploadedBy = Truncate(string.IsNullOrWhiteSpace(uploadedBy) ? "unknown" : uploadedBy.Trim(), 256),
            UploadedAtUtc = uploadedAtUtc,
            ParsedRowsJson = parsedRowsJson,
            PlanHash = planHash,
            SummaryJson = summaryJson,
        };

    public Result MarkCommitted(string committedBy, DateTimeOffset committedAtUtc)
    {
        if (IsCommitted)
        {
            return Result.Failure(BookeoImportErrors.AlreadyCommitted);
        }

        CommittedAtUtc = committedAtUtc;
        CommittedBy = Truncate(string.IsNullOrWhiteSpace(committedBy) ? "unknown" : committedBy.Trim(), 256);
        return Result.Success();
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
