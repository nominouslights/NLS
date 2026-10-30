using NorthernLink.Trips.Application.BookeoImports.Parsing;
using NorthernLink.Trips.Application.BookeoImports.Planning;
using NorthernLink.Trips.Infrastructure.BookeoImports;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The Bookeo report parser against the two synthetic fixtures (the real 47 headers, fake people),
/// plus the edge cases a real export throws at it: an unrecognized header, Excel serial dates, the
/// participants-details block format, and the fare split.
/// </summary>
public class BookeoReportParserTests
{
    [Theory]
    [InlineData("bookeo_sample.xls")]
    [InlineData("bookeo_sample.xlsx")]
    public void Both_fixture_formats_parse_to_the_same_five_bookings(string fixture)
    {
        var rows = BookeoTestBed.FixtureRows(fixture);

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.True(r.IsReadable, string.Join(" ", r.Problems)));
        Assert.Equal(
            ["9000000000000001", "9000000000000002", "9000000000000003", "9000000000000004", "9000000000000005"],
            rows.Select(r => r.BookingNumber));

        var first = rows[0];
        Assert.Equal(new DateOnly(2026, 10, 5), first.ServiceDate);
        Assert.Equal(new TimeOnly(8, 0), first.WindowStart);
        Assert.Equal(new TimeOnly(12, 30), first.WindowEnd);
        Assert.Equal("Alex Sample", first.CustomerName);
        Assert.Equal("alex@example.test", first.CustomerEmail);
        Assert.Equal(2, first.Participants);
        Assert.Equal("Shuttle from Thompson", first.ProductName);
        Assert.Equal(BookeoTestBed.FromThompson, first.ProductCode);
        Assert.Equal("Ford Transit 150", first.UnitText);
        Assert.Equal("normal", first.Status);
        Assert.False(first.IsCanceled);
        Assert.Equal(240m, first.TotalGrossCad);
        Assert.Equal(240m, first.TotalPaidCad);
        Assert.Equal(0m, first.TotalDueCad);
        Assert.Null(first.Destination);
        Assert.Equal([new BookeoCategoryCount("Lynn Lake Residents", 2)], first.Categories);
        Assert.Equal(2, first.Passengers.Count);

        var second = rows[1];
        Assert.Equal("pending payment", second.Status);
        Assert.Null(second.UnitText);
        Assert.Equal(100m, second.TotalDueCad);

        var third = rows[2];
        Assert.Equal(new DateOnly(2026, 10, 6), third.ServiceDate);
        Assert.Equal(new TimeOnly(13, 30), third.WindowStart);
        Assert.Equal(new TimeOnly(18, 0), third.WindowEnd);

        var fourth = rows[3];
        Assert.Equal("Leaf Rapids to Lynn Lake", fourth.Destination);
        Assert.Equal(new TimeOnly(11, 0), fourth.WindowStart);
        Assert.Equal(75m, fourth.TotalDueCad);

        var fifth = rows[4];
        Assert.True(fifth.IsCanceled);
        Assert.Equal("canceled", fifth.Status);
    }

    [Fact]
    public void Category_columns_are_every_column_between_Participants_and_Participants_details()
    {
        var rows = BookeoTestBed.FixtureRows();

        var categories = rows.SelectMany(r => r.Categories.Select(c => c.Category)).Distinct().Order().ToList();
        Assert.Equal(["Leaf Rapids Residents", "Leaf Rapids to Lynn Lake", "Lynn Lake Residents"], categories);
    }

    [Fact]
    public void A_sheet_without_the_Bookeo_headers_is_HeaderNotRecognized_listing_what_is_missing()
    {
        IReadOnlyList<IReadOnlyList<object?>> grid =
        [
            ["Booking number", "Start", "First name", "Participants", "Participants (details)", "Trip"],
            ["9000000000000001", 46300.3333, "Alex", 1.0, "", "Shuttle"],
        ];

        var result = BookeoReportParser.Parse(grid);

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.BookeoImport.HeaderNotRecognized", result.Error.Code);
        foreach (var missing in new[] { "End", "Last name", "Product code", "Status", "Total gross", "Total paid", "Total due" })
        {
            Assert.Contains(missing, result.Error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Header_names_are_matched_exactly_not_loosely()
    {
        var header = BookeoReportParser.RequiredColumns.Select(c => (object?)c.ToUpperInvariant()).ToList();

        var result = BookeoReportParser.Parse([header]);

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.BookeoImport.HeaderNotRecognized", result.Error.Code);
    }

    [Theory]
    [InlineData(46300.333333333336, 2026, 10, 5, 8, 0)]
    [InlineData(46300.520833333336, 2026, 10, 5, 12, 30)]
    [InlineData(46301.5625, 2026, 10, 6, 13, 30)]
    [InlineData(46301.458333333336, 2026, 10, 6, 11, 0)]
    public void An_Excel_serial_becomes_local_wall_clock_date_and_time_rounded_to_the_minute(
        double serial, int year, int month, int day, int hour, int minute)
    {
        var value = BookeoReportParser.ReadDateTime(serial, out var unreadable);

        Assert.False(unreadable);
        Assert.Equal(new DateTime(year, month, day, hour, minute, 0), value);
    }

    [Fact]
    public void A_DateTime_cell_or_an_iso_string_is_accepted_too_and_garbage_is_unreadable()
    {
        Assert.Equal(new DateTime(2026, 10, 5, 8, 0, 0), BookeoReportParser.ReadDateTime(new DateTime(2026, 10, 5, 8, 0, 0), out _));
        Assert.Equal(new DateTime(2026, 10, 5, 8, 0, 0), BookeoReportParser.ReadDateTime("2026-10-05 08:00", out _));

        Assert.Null(BookeoReportParser.ReadDateTime("next tuesday", out var unreadable));
        Assert.True(unreadable);
    }

    [Fact]
    public void A_bad_row_is_a_per_row_problem_not_a_whole_file_failure()
    {
        var header = BookeoReportParser.RequiredColumns.Select(c => (object?)c).ToList();
        object?[] good = new object?[header.Count];
        object?[] bad = new object?[header.Count];
        void Set(object?[] row, string column, object? value) => row[header.IndexOf(column)] = value;
        foreach (var row in new[] { good, bad })
        {
            Set(row, "Booking number", row == good ? "9000000000000101" : "9000000000000102");
            Set(row, "Start", row == good ? 46300.3333333333 : "not a date");
            Set(row, "First name", "Alex");
            Set(row, "Last name", "Sample");
            Set(row, "Participants", 1.0);
            Set(row, "Product code", "P1");
            Set(row, "Trip", "Shuttle");
            Set(row, "Status", "normal");
            Set(row, "Total gross", 100.0);
        }

        var result = BookeoReportParser.Parse([header, good, bad]);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value[0].IsReadable);
        Assert.False(result.Value[1].IsReadable);
        Assert.Contains("start", string.Join(" ", result.Value[1].Problems), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Participants_details_blocks_parse_with_email_and_phone_both_optional()
    {
        const string details =
            "### Lynn Lake Residents 1:\nAlex Sample\nalex@example.test\n2045550101 (mobile)\n\n" +
            "### Lynn Lake Residents 2:\nBo Sample\n\n" +
            "### Leaf Rapids to Lynn Lake 3:\nCy Sample\n204-555-0199 (home)\n";

        var passengers = BookeoParticipantsDetailsParser.Parse(details);

        Assert.Equal(3, passengers.Count);
        Assert.Equal(new BookeoParsedPassenger("Lynn Lake Residents", 1, "Alex Sample", "alex@example.test", "2045550101"), passengers[0]);
        Assert.Equal(new BookeoParsedPassenger("Lynn Lake Residents", 2, "Bo Sample", null, null), passengers[1]);
        Assert.Equal(new BookeoParsedPassenger("Leaf Rapids to Lynn Lake", 3, "Cy Sample", null, "204-555-0199"), passengers[2]);
    }

    [Fact]
    public void Details_tolerate_windows_line_endings_and_blank_cells()
    {
        Assert.Empty(BookeoParticipantsDetailsParser.Parse(null));
        Assert.Empty(BookeoParticipantsDetailsParser.Parse("   "));

        var passengers = BookeoParticipantsDetailsParser.Parse("### Adults 1:\r\nDee Sample\r\ndee@example.test\r\n");
        Assert.Equal("Dee Sample", Assert.Single(passengers).Name);
    }

    [Fact]
    public void Fewer_named_passengers_than_participants_are_topped_up_with_customer_guest_placeholders()
    {
        var row = BookeoTestBed.FixtureRow("001") with
        {
            Participants = 4,
            Categories = [new BookeoCategoryCount("Lynn Lake Residents", 3), new BookeoCategoryCount("Leaf Rapids Residents", 1)],
        };

        var built = BookeoBookingBuilder.Build(row);

        Assert.Equal(2, built.PlaceholderCount);
        Assert.Equal(
            ["Alex Sample", "Bo Sample", "Alex Sample guest 1", "Alex Sample guest 2"],
            built.Snapshot.Passengers.Select(p => p.Name));
        // The placeholders take the categories the details block did not account for.
        Assert.Equal("Lynn Lake Residents", built.Snapshot.Passengers[2].Category);
        Assert.Equal("Leaf Rapids Residents", built.Snapshot.Passengers[3].Category);
    }

    [Theory]
    [InlineData(240, 2)]
    [InlineData(100, 3)]
    [InlineData(200, 3)]
    [InlineData(75, 1)]
    [InlineData(0.05, 3)]
    [InlineData(1000, 7)]
    public void The_fare_split_always_sums_exactly_to_the_gross(decimal gross, int passengers)
    {
        var fares = BookeoBookingBuilder.SplitFare(gross, passengers);

        Assert.Equal(passengers, fares.Count);
        Assert.Equal(gross, fares.Sum());
        Assert.All(fares, f => Assert.Equal(decimal.Round(f, 2), f));
        // Everyone but passenger 1 pays the rounded share; the remainder sits on passenger 1.
        Assert.All(fares.Skip(1), f => Assert.Equal(Math.Round(gross / passengers, 2, MidpointRounding.AwayFromZero), f));
    }

    [Fact]
    public void A_file_that_is_not_a_workbook_is_UnsupportedFile()
    {
        using var stream = new MemoryStream("Booking number,Start\n1,2"u8.ToArray());

        var result = new ExcelBookeoWorkbookReader().Read(stream);

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.BookeoImport.UnsupportedFile", result.Error.Code);
    }
}
