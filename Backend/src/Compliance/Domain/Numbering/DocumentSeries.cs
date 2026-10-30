using NorthernLink.Compliance.Domain.Numbering.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Numbering;

/// <summary>
/// The counter behind one series for one year in one tenant: <c>HS/2026</c> has issued 31
/// numbers, so the next Notice of Non-Compliance is <c>NL-HS-04-2026-0032</c>.
/// <para>
/// <b>Gap-free and concurrency-safe by construction, not by locking.</b> The row is an aggregate,
/// so <see cref="AggregateRoot.Version"/> is its optimistic-concurrency token; the handler issues
/// a number and saves the record that uses it <em>in the same SaveChanges</em>, so a lost race
/// (two requests loaded the same LastSequence) fails the loser's whole save — number and record
/// together — and the handler reloads and retries. A number is therefore never issued without the
/// record that carries it, and the committed sequence has no holes. One row per (tenant, series,
/// year), unique-indexed.
/// </para>
/// </summary>
public sealed class DocumentSeries : AggregateRoot, ITenantScoped
{
    private DocumentSeries()
    {
        // EF Core materialization only.
    }

    public Guid TenantId { get; private set; }
    public DocumentSeriesCode Series { get; private set; }
    public int Year { get; private set; }

    /// <summary>The last sequence handed out; 0 for a series nothing has used yet.</summary>
    public int LastSequence { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Opens a series for a year (LastSequence 0).</summary>
    public static DocumentSeries Open(Guid tenantId, DocumentSeriesCode series, int year)
    {
        var opened = new DocumentSeries
        {
            TenantId = tenantId,
            Series = series,
            Year = year,
            LastSequence = 0,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        opened.Raise(new DocumentSeriesOpenedDomainEvent(opened.Id, tenantId, series, year));
        return opened;
    }

    /// <summary>Hands out the next number in this series — the caller must save it together with the record that carries it.</summary>
    public string IssueNext(string prefix, string formNumber)
    {
        LastSequence++;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        var number = DocumentNumberFormat.Compose(prefix, Series, formNumber, Year, LastSequence);
        Raise(new DocumentNumberIssuedDomainEvent(Id, TenantId, Series, Year, LastSequence, number));
        return number;
    }

    /// <summary>
    /// Rebuilds a series from stored state — for the in-memory test repository, which models a
    /// fresh load per request (and therefore a real version race) without a database. Not used by
    /// production code: EF materializes through the private constructor.
    /// </summary>
    internal static DocumentSeries Rehydrate(Guid id, Guid tenantId, DocumentSeriesCode series, int year, int lastSequence) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Series = series,
            Year = year,
            LastSequence = lastSequence,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
}
