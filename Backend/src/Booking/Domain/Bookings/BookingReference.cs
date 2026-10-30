using System.Security.Cryptography;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.Bookings;

/// <summary>
/// The human-readable booking reference printed on passes and quoted by customers —
/// <c>NL-</c> plus six characters from a 30-symbol alphabet that drops the look-alikes
/// I/L/O/U/0/1 (so a reference read over the phone or off a printout can't be mistyped).
/// Nine characters total, ~729M values per tenant. Immutable once stamped on a booking;
/// uniqueness is per tenant and enforced by the <c>(tenant_id, reference)</c> unique index,
/// with the create handler's collision guard in front of it. <see cref="Generate"/> is the
/// only RNG in the module and lives here so the aggregate stays deterministic and testable;
/// <see cref="Create"/> trims and uppercases input before validating.
/// </summary>
public sealed record BookingReference
{
    public const string Prefix = "NL-";
    public const int BodyLength = 6;
    public const int Length = 9;

    /// <summary>A–Z and 2–9 minus I, L, O, U, 0, 1 — 30 symbols with no look-alike pairs.</summary>
    public const string Alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

    private BookingReference(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>A fresh cryptographically random reference (uniqueness is the caller's job).</summary>
    public static BookingReference Generate() =>
        new(Prefix + new string(RandomNumberGenerator.GetItems<char>(Alphabet, BodyLength)));

    public static Result<BookingReference> Create(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();

        if (normalized is null
            || normalized.Length != Length
            || !normalized.StartsWith(Prefix, StringComparison.Ordinal)
            || !normalized[Prefix.Length..].All(IsAllowedCharacter))
        {
            return Result.Failure<BookingReference>(BookingErrors.InvalidReference);
        }

        return Result.Success(new BookingReference(normalized));
    }

    private static bool IsAllowedCharacter(char c) => Alphabet.Contains(c);

    public override string ToString() => Value;
}
