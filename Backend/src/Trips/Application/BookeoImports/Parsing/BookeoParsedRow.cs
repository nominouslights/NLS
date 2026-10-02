using System.Text.Json.Serialization;

namespace NorthernLink.Trips.Application.BookeoImports.Parsing;

/// <summary>
/// One Bookeo booking row, normalized — exactly what <c>bookeo_import_batches.parsed_rows</c>
/// stores and what a commit re-plans from.
/// <para>
/// <b>There is deliberately no tax member here, and there must never be one.</b> Bookeo's
/// <c>GST</c> and <c>Total net</c> columns are never read by the parser, so they cannot reach a
/// table, a log, a DTO or the UI: the platform never computes or stores tax (QuickBooks owns it).
/// <see cref="TotalGrossCad"/> is the tax-inclusive gross. A test pins this type's member list.
/// </para>
/// </summary>
public sealed record BookeoParsedRow
{
    /// <summary>1-based spreadsheet row, for messages ("row 7").</summary>
    public int RowNumber { get; init; }

    /// <summary>The 16-digit Bookeo booking number — the idempotency key. Null only on an unreadable row.</summary>
    public string? BookingNumber { get; init; }

    /// <summary>Local wall-clock date and times as Bookeo shows them — no time-zone conversion.</summary>
    public DateOnly? ServiceDate { get; init; }

    public TimeOnly? WindowStart { get; init; }
    public TimeOnly? WindowEnd { get; init; }

    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? CustomerEmail { get; init; }
    public string? CustomerPhone { get; init; }

    public int Participants { get; init; }

    /// <summary>Per-category head counts — every column between <c>Participants</c> and <c>Participants (details)</c>.</summary>
    public IReadOnlyList<BookeoCategoryCount> Categories { get; init; } = [];

    /// <summary>The passengers named in <c>Participants (details)</c> (may be fewer than <see cref="Participants"/>).</summary>
    public IReadOnlyList<BookeoParsedPassenger> Passengers { get; init; } = [];

    /// <summary>Bookeo's <c>Trip</c> column — the product name.</summary>
    public string? ProductName { get; init; }

    public string? ProductCode { get; init; }

    /// <summary>Bookeo's <c>Destinations</c> — set only on bidirectional products.</summary>
    public string? Destination { get; init; }

    public string? UnitText { get; init; }

    /// <summary>Lower-cased Bookeo status: "normal", "pending payment", "canceled".</summary>
    public string Status { get; init; } = string.Empty;

    public bool IsCanceled { get; init; }

    public decimal TotalGrossCad { get; init; }
    public decimal TotalPaidCad { get; init; }
    public decimal TotalDueCad { get; init; }

    /// <summary>Why the row cannot be imported — non-empty makes it a <c>RowUnreadable</c> Block.</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    [JsonIgnore]
    public bool IsReadable => Problems.Count == 0;

    [JsonIgnore]
    public string CustomerName =>
        Domain.BookeoImports.BookeoText.Clean($"{FirstName} {LastName}")
        ?? $"Bookeo booking {BookingNumber}";
}

/// <summary>One people-category column's count on a booking ("Lynn Lake Residents": 2).</summary>
public sealed record BookeoCategoryCount(string Category, int Count);

/// <summary>One "### &lt;Category&gt; &lt;n&gt;:" block from <c>Participants (details)</c>.</summary>
public sealed record BookeoParsedPassenger(string Category, int Index, string Name, string? Email, string? Phone);
