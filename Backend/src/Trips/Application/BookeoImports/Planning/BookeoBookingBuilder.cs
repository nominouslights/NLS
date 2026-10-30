using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NorthernLink.Trips.Application.BookeoImports.Parsing;
using NorthernLink.Trips.Domain.BookeoImports;

namespace NorthernLink.Trips.Application.BookeoImports.Planning;

/// <summary>
/// Turns a readable parsed row into the booking's importable content — the
/// <see cref="BookeoBookingSnapshot"/> the ledger stores and a re-upload is diffed against —
/// including the placeholder passengers a short <c>Participants (details)</c> list is topped up
/// with, and the content hash. Deterministic: the same row always yields the same hash.
/// </summary>
public static class BookeoBookingBuilder
{
    /// <summary>The snapshot plus how many passengers had to be invented as placeholders.</summary>
    public sealed record Built(BookeoBookingSnapshot Snapshot, int PlaceholderCount);

    public static Built Build(BookeoParsedRow row)
    {
        var customerName = row.CustomerName;
        var passengers = row.Passengers
            .Select(p => new BookeoBookingPassenger
            {
                Name = p.Name,
                Email = p.Email,
                Phone = p.Phone,
                Category = p.Category,
            })
            .ToList();

        var placeholders = 0;
        if (passengers.Count < row.Participants)
        {
            // Whatever the details block did not name: hand each placeholder the category whose
            // column count is still unaccounted for, in column order.
            var remaining = row.Categories
                .Select(c => (c.Category, Left: c.Count - row.Passengers.Count(p =>
                    string.Equals(p.Category, c.Category, StringComparison.OrdinalIgnoreCase))))
                .Where(c => c.Left > 0)
                .SelectMany(c => Enumerable.Repeat(c.Category, c.Left))
                .ToList();

            while (passengers.Count < row.Participants)
            {
                placeholders++;
                passengers.Add(new BookeoBookingPassenger
                {
                    Name = $"{customerName} guest {placeholders}",
                    Category = placeholders - 1 < remaining.Count
                        ? remaining[placeholders - 1]
                        : row.Categories.FirstOrDefault()?.Category ?? passengers.FirstOrDefault()?.Category,
                });
            }
        }

        var snapshot = new BookeoBookingSnapshot(
            row.BookingNumber!,
            row.ProductCode!,
            row.ProductName ?? row.ProductCode!,
            row.Destination,
            row.ServiceDate!.Value,
            row.WindowStart!.Value,
            row.WindowEnd,
            row.Status,
            row.Participants,
            passengers,
            customerName,
            row.CustomerEmail,
            row.CustomerPhone,
            row.TotalGrossCad,
            row.TotalPaidCad,
            row.TotalDueCad,
            row.UnitText,
            ContentHash: string.Empty);

        return new Built(snapshot with { ContentHash = Hash(snapshot) }, placeholders);
    }

    /// <summary>SHA-256 over the snapshot's content (everything but the hash itself), hex.</summary>
    public static string Hash(BookeoBookingSnapshot snapshot)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            snapshot.BookingNumber,
            snapshot.ProductCode,
            snapshot.ProductName,
            snapshot.Destination,
            ServiceDate = snapshot.ServiceDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            WindowStart = snapshot.WindowStart.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            WindowEnd = snapshot.WindowEnd?.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            snapshot.BookeoStatus,
            snapshot.Participants,
            Passengers = snapshot.Passengers.Select(p => new { p.Name, p.Email, p.Phone, p.Category }).ToList(),
            snapshot.CustomerName,
            snapshot.CustomerEmail,
            snapshot.CustomerPhone,
            TotalGrossCad = snapshot.TotalGrossCad.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            TotalPaidCad = snapshot.TotalPaidCad.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            TotalDueCad = snapshot.TotalDueCad.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            snapshot.UnitText,
        });

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Which parts of a booking differ between the ledger and the new row — "time", "product",
    /// "passengers", "status", "unit", "totals", and "customer" for a contact-only edit.
    /// </summary>
    public static IReadOnlyList<string> ChangedFields(BookeoBookingSnapshot before, BookeoBookingSnapshot after)
    {
        var fields = new List<string>();
        if (before.ServiceDate != after.ServiceDate || before.WindowStart != after.WindowStart || before.WindowEnd != after.WindowEnd)
        {
            fields.Add("time");
        }

        if (before.ProductCode != after.ProductCode
            || before.ProductName != after.ProductName
            || !BookeoText.SameDestination(before.Destination, after.Destination))
        {
            fields.Add("product");
        }

        if (before.Participants != after.Participants || !before.Passengers.SequenceEqual(after.Passengers))
        {
            fields.Add("passengers");
        }

        if (before.BookeoStatus != after.BookeoStatus)
        {
            fields.Add("status");
        }

        if (!string.Equals(BookeoText.NormalizeUnit(before.UnitText), BookeoText.NormalizeUnit(after.UnitText), StringComparison.Ordinal))
        {
            fields.Add("unit");
        }

        if (before.TotalGrossCad != after.TotalGrossCad || before.TotalPaidCad != after.TotalPaidCad || before.TotalDueCad != after.TotalDueCad)
        {
            fields.Add("totals");
        }

        if (fields.Count == 0)
        {
            fields.Add("customer");
        }

        return fields;
    }

    /// <summary>
    /// Splits the tax-inclusive gross across the passengers: round(gross / n, 2) each, with the
    /// remainder (positive or negative, a cent or two) on passenger 1 — so the fares always sum
    /// to exactly the gross.
    /// </summary>
    public static IReadOnlyList<decimal> SplitFare(decimal totalGross, int passengers)
    {
        if (passengers <= 0)
        {
            return [];
        }

        var each = Math.Round(totalGross / passengers, 2, MidpointRounding.AwayFromZero);
        var fares = Enumerable.Repeat(each, passengers).ToArray();
        fares[0] = totalGross - (each * (passengers - 1));
        return fares;
    }
}
