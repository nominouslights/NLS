using NorthernLink.Booking.Domain.Bookings;
using Xunit;

namespace NorthernLink.Booking.Tests;

/// <summary>
/// The booking reference value object: the NL-XXXXXX format, the look-alike-free alphabet,
/// and Create's normalization + rejection rules.
/// </summary>
public class BookingReferenceTests
{
    private const string Forbidden = "ILOU01";

    [Fact]
    public void Generate_produces_the_prefix_and_a_six_character_body()
    {
        var reference = BookingReference.Generate();

        Assert.Equal(BookingReference.Length, reference.Value.Length);
        Assert.StartsWith("NL-", reference.Value, StringComparison.Ordinal);
        Assert.Equal(BookingReference.BodyLength, reference.Value.Length - BookingReference.Prefix.Length);
        Assert.True(BookingReference.Create(reference.Value).IsSuccess); // round-trips through Create
    }

    [Fact]
    public void Generate_never_emits_a_look_alike_character()
    {
        for (var i = 0; i < 1_000; i++)
        {
            var body = BookingReference.Generate().Value[BookingReference.Prefix.Length..];

            Assert.All(body, c => Assert.Contains(c, BookingReference.Alphabet));
            Assert.DoesNotContain(body, c => Forbidden.Contains(c));
        }
    }

    [Fact]
    public void Alphabet_is_thirty_symbols_with_no_look_alikes()
    {
        Assert.Equal(30, BookingReference.Alphabet.Length);
        Assert.Equal(30, BookingReference.Alphabet.Distinct().Count());
        Assert.DoesNotContain(BookingReference.Alphabet, c => Forbidden.Contains(c));
    }

    [Fact]
    public void Create_trims_and_uppercases()
    {
        var result = BookingReference.Create("  nl-7k3m2q ");

        Assert.True(result.IsSuccess);
        Assert.Equal("NL-7K3M2Q", result.Value.Value);
    }

    [Fact]
    public void Create_gives_structural_equality()
    {
        Assert.Equal(BookingReference.Create("NL-7K3M2Q").Value, BookingReference.Create("nl-7k3m2q").Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NL-7K3M2")]      // short body
    [InlineData("NL-7K3M2QX")]    // long body
    [InlineData("NK-7K3M2Q")]     // wrong prefix
    [InlineData("7K3M2Q")]        // no prefix
    [InlineData("NL-7K3M2I")]     // forbidden I
    [InlineData("NL-7K3M2O")]     // forbidden O
    [InlineData("NL-7K3M20")]     // forbidden 0
    [InlineData("NL-7K3M21")]     // forbidden 1
    [InlineData("NL-7K3M2L")]     // forbidden L
    [InlineData("NL-7K3M2U")]     // forbidden U
    [InlineData("NL-7K3M-Q")]     // punctuation in the body
    public void Create_rejects_malformed_input(string? value)
    {
        var result = BookingReference.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.InvalidReference, result.Error);
    }
}
