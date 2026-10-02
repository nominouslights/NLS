using System.Globalization;
using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.BookeoImports;

namespace NorthernLink.Trips.Application.BookeoImports.Parsing;

/// <summary>
/// Turns the cell grid of a Bookeo booking report (read by <c>IBookeoWorkbookReader</c>) into
/// normalized <see cref="BookeoParsedRow"/>s. Pure: no I/O, no clock.
/// <para>
/// The header row is found by <b>exact</b> column names. A missing required column fails the whole
/// file with <see cref="BookeoImportErrors.HeaderNotRecognized"/> listing what is missing; a bad
/// row never does — it comes back with <see cref="BookeoParsedRow.Problems"/> and the planner turns
/// that into a per-row <c>RowUnreadable</c> Block.
/// </para>
/// <para>
/// <b>Tax columns are never read.</b> Only the columns named below are looked up; <c>GST</c> and
/// <c>Total net</c> (and every other ignored column — IP address, coupons, waivers) are skipped by
/// construction, so no tax value ever leaves this method.
/// </para>
/// </summary>
public static class BookeoReportParser
{
    public const string BookingNumberColumn = "Booking number";
    public const string StartColumn = "Start";
    public const string EndColumn = "End";
    public const string FirstNameColumn = "First name";
    public const string LastNameColumn = "Last name";
    public const string EmailColumn = "Email address";
    public const string PhoneColumn = "Phone";
    public const string ParticipantsColumn = "Participants";
    public const string ParticipantsDetailsColumn = "Participants (details)";
    public const string TripColumn = "Trip";
    public const string ProductCodeColumn = "Product code";
    public const string DestinationsColumn = "Destinations";
    public const string UnitColumn = "Unit";
    public const string StatusColumn = "Status";
    public const string CanceledColumn = "Canceled";
    public const string TotalGrossColumn = "Total gross";
    public const string TotalPaidColumn = "Total paid";
    public const string TotalDueColumn = "Total due";

    /// <summary>Columns whose absence means "not a Bookeo booking report".</summary>
    public static readonly IReadOnlyList<string> RequiredColumns =
    [
        BookingNumberColumn, StartColumn, EndColumn, FirstNameColumn, LastNameColumn,
        ParticipantsColumn, ParticipantsDetailsColumn, TripColumn, ProductCodeColumn,
        StatusColumn, TotalGrossColumn, TotalPaidColumn, TotalDueColumn,
    ];

    /// <summary>How far down the sheet the header row may sit (Bookeo puts it on row 1).</summary>
    private const int HeaderScanRows = 10;

    public static Result<IReadOnlyList<BookeoParsedRow>> Parse(IReadOnlyList<IReadOnlyList<object?>> grid)
    {
        var (headerRow, columns, missing) = FindHeader(grid);
        if (missing.Count > 0)
        {
            return Result.Failure<IReadOnlyList<BookeoParsedRow>>(BookeoImportErrors.HeaderNotRecognized(missing));
        }

        var participantsAt = columns[ParticipantsColumn];
        var detailsAt = columns[ParticipantsDetailsColumn];
        var header = grid[headerRow];

        // Every column strictly between Participants and Participants (details) is a people
        // category — the set varies per Bookeo account, so it is positional, never a fixed list.
        var categoryColumns = new List<(int Index, string Name)>();
        for (var i = participantsAt + 1; i < detailsAt; i++)
        {
            if (Text(Cell(header, i)) is { } name)
            {
                categoryColumns.Add((i, name));
            }
        }

        var rows = new List<BookeoParsedRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var r = headerRow + 1; r < grid.Count; r++)
        {
            var cells = grid[r];
            if (cells.All(c => Text(c) is null))
            {
                continue; // Trailing blank rows are not bookings.
            }

            var row = ParseRow(r + 1, cells, columns, categoryColumns);
            if (row.BookingNumber is { } number && !seen.Add(number))
            {
                row = row with { Problems = [.. row.Problems, $"Booking number {number} appears more than once in this file."] };
            }

            rows.Add(row);
        }

        return Result.Success<IReadOnlyList<BookeoParsedRow>>(rows);
    }

    private static (int Row, Dictionary<string, int> Columns, IReadOnlyList<string> Missing) FindHeader(
        IReadOnlyList<IReadOnlyList<object?>> grid)
    {
        IReadOnlyList<string> bestMissing = RequiredColumns;
        for (var r = 0; r < Math.Min(grid.Count, HeaderScanRows); r++)
        {
            var columns = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var c = 0; c < grid[r].Count; c++)
            {
                if (Text(grid[r][c]) is { } name)
                {
                    columns.TryAdd(name, c);
                }
            }

            var missing = RequiredColumns.Where(name => !columns.ContainsKey(name)).ToList();
            if (missing.Count == 0 && columns[ParticipantsColumn] < columns[ParticipantsDetailsColumn])
            {
                return (r, columns, []);
            }

            if (missing.Count < bestMissing.Count)
            {
                bestMissing = missing.Count == 0 ? [ParticipantsDetailsColumn] : missing;
            }
        }

        return (-1, [], bestMissing);
    }

    private static BookeoParsedRow ParseRow(
        int rowNumber,
        IReadOnlyList<object?> cells,
        IReadOnlyDictionary<string, int> columns,
        IReadOnlyList<(int Index, string Name)> categoryColumns)
    {
        var problems = new List<string>();
        object? Get(string column) => columns.TryGetValue(column, out var i) ? Cell(cells, i) : null;

        var bookingNumber = BookingNumber(Get(BookingNumberColumn));
        if (bookingNumber is null)
        {
            problems.Add("The booking number is blank.");
        }

        var start = ReadDateTime(Get(StartColumn), out var startBad);
        if (start is null)
        {
            problems.Add(startBad ? "The start date/time is not a date." : "The start date/time is blank.");
        }

        var end = ReadDateTime(Get(EndColumn), out var endBad);
        if (endBad)
        {
            problems.Add("The end date/time is not a date.");
        }

        var status = (BookeoText.Clean(Text(Get(StatusColumn))) ?? string.Empty).ToLowerInvariant();
        var isCanceled = status.Contains("cancel", StringComparison.Ordinal) || Text(Get(CanceledColumn)) is not null;

        var participants = ReadInt(Get(ParticipantsColumn), out var participantsBad);
        if (participantsBad || participants < 0)
        {
            problems.Add("Participants is not a whole number.");
        }
        else if (participants == 0 && !isCanceled)
        {
            problems.Add("The booking has no participants.");
        }

        var categories = new List<BookeoCategoryCount>();
        foreach (var (index, name) in categoryColumns)
        {
            var count = ReadInt(Cell(cells, index), out var bad);
            if (!bad && count > 0)
            {
                categories.Add(new BookeoCategoryCount(name, count));
            }
        }

        var productCode = BookeoText.Clean(Text(Get(ProductCodeColumn)));
        if (productCode is null)
        {
            problems.Add("The product code is blank.");
        }

        var gross = ReadMoney(Get(TotalGrossColumn), "Total gross", problems);
        var paid = ReadMoney(Get(TotalPaidColumn), "Total paid", problems);
        var due = ReadMoney(Get(TotalDueColumn), "Total due", problems);

        return new BookeoParsedRow
        {
            RowNumber = rowNumber,
            BookingNumber = bookingNumber,
            ServiceDate = start is { } s ? DateOnly.FromDateTime(s) : null,
            WindowStart = start is { } s2 ? TimeOnly.FromDateTime(s2) : null,
            WindowEnd = end is { } e ? TimeOnly.FromDateTime(e) : null,
            FirstName = BookeoText.Clean(Text(Get(FirstNameColumn))),
            LastName = BookeoText.Clean(Text(Get(LastNameColumn))),
            CustomerEmail = BookeoText.Clean(Text(Get(EmailColumn))),
            CustomerPhone = BookeoText.Clean(Text(Get(PhoneColumn))),
            Participants = Math.Max(participants, 0),
            Categories = categories,
            Passengers = BookeoParticipantsDetailsParser.Parse(Text(Get(ParticipantsDetailsColumn), trim: false)),
            ProductName = BookeoText.Clean(Text(Get(TripColumn))) ?? productCode,
            ProductCode = productCode,
            Destination = BookeoText.NormalizeDestination(Text(Get(DestinationsColumn))),
            UnitText = BookeoText.Clean(Text(Get(UnitColumn))),
            Status = status,
            IsCanceled = isCanceled,
            TotalGrossCad = gross,
            TotalPaidCad = paid,
            TotalDueCad = due,
            Problems = problems,
        };
    }

    private static object? Cell(IReadOnlyList<object?> cells, int index) =>
        index >= 0 && index < cells.Count ? cells[index] : null;

    /// <summary>A cell as text: null for blank. Whole numbers print without a decimal point.</summary>
    public static string? Text(object? cell, bool trim = true)
    {
        var text = cell switch
        {
            null or DBNull => null,
            string s => s,
            double d when d == Math.Floor(d) && Math.Abs(d) < 1e16 => d.ToString("F0", CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => cell.ToString(),
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return trim ? text.Trim() : text;
    }

    private static string? BookingNumber(object? cell) => cell switch
    {
        // A 16-digit number stored numerically loses its last digits past 2^53; the text form is
        // the best available and Bookeo itself exports these as text.
        double d => d.ToString("F0", CultureInfo.InvariantCulture),
        _ => BookeoText.Clean(Text(cell))?.TrimStart('#'),
    };

    /// <summary>
    /// Bookeo's Start/End: an Excel serial (46300.3333 = 2026-10-05 08:00), a DateTime cell, or a
    /// "yyyy-MM-dd HH:mm" string — all local wall-clock time, no time-zone conversion. Rounded to
    /// the minute, because a serial's fraction is never exact (08:00 reads back as 07:59:59.99).
    /// </summary>
    public static DateTime? ReadDateTime(object? cell, out bool unreadable)
    {
        unreadable = false;
        DateTime? value = cell switch
        {
            null or DBNull => null,
            double serial => FromSerial(serial),
            DateTime dt => dt,
            string s when string.IsNullOrWhiteSpace(s) => null,
            string s => ParseDateText(s.Trim()),
            _ => null,
        };

        if (value is null && Text(cell) is not null)
        {
            unreadable = true;
            return null;
        }

        return value is { } v ? RoundToMinute(v) : null;
    }

    private static DateTime? FromSerial(double serial)
    {
        // OADate covers 0100-01-01 … 9999-12-31; anything outside is not a booking time.
        if (serial is < 1 or > 2958465)
        {
            return null;
        }

        return DateTime.FromOADate(serial);
    }

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd",
    ];

    private static DateTime? ParseDateText(string text)
    {
        if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        // A serial that arrived as text.
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
            ? FromSerial(serial)
            : null;
    }

    private static DateTime RoundToMinute(DateTime value)
    {
        var ticks = (value.Ticks + (TimeSpan.TicksPerMinute / 2)) / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute;
        return new DateTime(ticks, DateTimeKind.Unspecified);
    }

    private static int ReadInt(object? cell, out bool unreadable)
    {
        unreadable = false;
        switch (cell)
        {
            case double d when d == Math.Floor(d) && Math.Abs(d) < int.MaxValue:
                return (int)d;
            case double:
                unreadable = true;
                return 0;
            default:
                var text = Text(cell);
                if (text is null)
                {
                    return 0;
                }

                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    return value;
                }

                unreadable = true;
                return 0;
        }
    }

    /// <summary>A CAD amount to the cent. Blank is zero; text tolerates "$" and thousands separators.</summary>
    private static decimal ReadMoney(object? cell, string column, List<string> problems)
    {
        switch (cell)
        {
            case double d when !double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) < 1e12:
                return Math.Round((decimal)d, 2, MidpointRounding.AwayFromZero);
            case decimal m:
                return Math.Round(m, 2, MidpointRounding.AwayFromZero);
        }

        var text = Text(cell);
        if (text is null)
        {
            return 0m;
        }

        var cleaned = text.Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("CAD", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        }

        problems.Add($"{column} is not an amount.");
        return 0m;
    }
}
