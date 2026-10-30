using System.Reflection;
using System.Text.Json;
using NorthernLink.Trips.Application.BookeoImports;
using NorthernLink.Trips.Application.BookeoImports.Parsing;
using NorthernLink.Trips.Domain.BookeoImports;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The platform never computes or stores tax (QuickBooks owns it). Bookeo's export carries
/// <c>GST</c> and <c>Total net</c>; these tests pin that neither survives the parser — not as a
/// member of the parsed row, the ledger or any import DTO, and not as a key in the parsed_rows
/// jsonb the batch stores.
/// </summary>
public class BookeoTaxRuleTests
{
    private static readonly string[] TaxWords = ["gst", "hst", "pst", "tax", "net"];

    /// <summary>Whole PascalCase words only — "TripsToUpdate" contains "pst" but is not a tax.</summary>
    private static bool LooksLikeTax(string name) =>
        System.Text.RegularExpressions.Regex.Matches(name, "[A-Z][a-z]*|[a-z]+")
            .Select(m => m.Value)
            .Any(word => TaxWords.Contains(word, StringComparer.OrdinalIgnoreCase));

    [Theory]
    [InlineData(typeof(BookeoParsedRow))]
    [InlineData(typeof(BookeoParsedPassenger))]
    [InlineData(typeof(BookeoCategoryCount))]
    [InlineData(typeof(BookeoBooking))]
    [InlineData(typeof(BookeoBookingSnapshot))]
    [InlineData(typeof(BookeoBookingPassenger))]
    [InlineData(typeof(BookeoImportRow))]
    [InlineData(typeof(BookeoImportSummary))]
    [InlineData(typeof(BookeoImportCommitResult))]
    public void No_import_type_has_a_tax_member(Type type)
    {
        var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(LooksLikeTax)
            .ToList();

        Assert.True(members.Count == 0, $"{type.Name} has tax-like members: {string.Join(", ", members)}");
    }

    [Fact]
    public void The_parsed_row_members_are_exactly_the_pinned_tax_free_set()
    {
        var members = typeof(BookeoParsedRow).GetProperties().Select(p => p.Name).Order().ToList();

        Assert.Equal(
            [
                "BookingNumber", "Categories", "CustomerEmail", "CustomerName", "CustomerPhone", "Destination",
                "FirstName", "IsCanceled", "IsReadable", "LastName", "Participants", "Passengers", "Problems",
                "ProductCode", "ProductName", "RowNumber", "ServiceDate", "Status", "TotalDueCad", "TotalGrossCad",
                "TotalPaidCad", "UnitText", "WindowEnd", "WindowStart",
            ],
            members);
    }

    [Theory]
    [InlineData("bookeo_sample.xls")]
    [InlineData("bookeo_sample.xlsx")]
    public async Task The_stored_parsed_rows_json_for_the_fixture_has_no_GST_or_net_key_or_value(string fixture)
    {
        var bed = new BookeoTestBed();
        await bed.PreviewFixtureAsync(fixture);
        var json = bed.Repo.Batches.Single().ParsedRowsJson;

        var keys = new List<string>();
        Collect(JsonDocument.Parse(json).RootElement, keys);

        Assert.NotEmpty(keys);
        Assert.DoesNotContain(keys, k => k.Contains("gst", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(keys, k => k.Contains("net", StringComparison.OrdinalIgnoreCase));

        // The fixture's tax figures (228.57 net / 11.43 GST on booking 001) appear nowhere.
        Assert.DoesNotContain("228.57", json, StringComparison.Ordinal);
        Assert.DoesNotContain("11.43", json, StringComparison.Ordinal);
        Assert.Contains("240", json, StringComparison.Ordinal); // The tax-inclusive gross is kept.
    }

    private static void Collect(JsonElement element, List<string> keys)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    keys.Add(property.Name);
                    Collect(property.Value, keys);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, keys);
                }

                break;
        }
    }
}
