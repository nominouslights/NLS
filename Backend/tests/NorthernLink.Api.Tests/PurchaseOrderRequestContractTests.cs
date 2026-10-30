using System.Text.Json;
using System.Text.Json.Serialization;
using NorthernLink.Clients.Infrastructure.Endpoints;
using Xunit;

namespace NorthernLink.Api.Tests;

/// <summary>
/// Wire-contract tests for the purchase-order request bodies.
/// <para>
/// A purchase order now carries negotiated pricing, so the update body's members are
/// <c>required</c>: a caller must send every key, though a value may be null. This pins the
/// distinction that matters — <b>an explicit null clears a rate on purpose, a missing key is
/// rejected</b> — because the failure it prevents is silent. PUT replaces the whole resource, so
/// before this a body that simply left <c>roundTripRateCad</c> out wiped the rate, pricing fell
/// back to the contract's rate, and the result was a plausible-looking figure that someone
/// hand-keys into QuickBooks. A wrong invoice that looks right is worse than a rejected request.
/// </para>
/// <para>
/// These assertions are at the serializer level, mirroring the gateway's JSON configuration in
/// <c>Program.cs</c> (web defaults + a string enum converter). They prove the binding throws on a
/// missing key rather than silently defaulting it. They do NOT execute ASP.NET's mapping of that
/// throw onto a 400 response — minimal APIs surface a binding <see cref="JsonException"/> as a
/// <c>BadHttpRequestException</c> carrying status 400, and proving that end to end needs a booted
/// host, which this project deliberately does not do.
/// </para>
/// </summary>
public class PurchaseOrderRequestContractTests
{
    // Mirrors builder.Services.ConfigureHttpJsonOptions(...) in Program.cs. Required-member
    // enforcement is a System.Text.Json default rather than something these options switch on —
    // they are here so the casing and enum handling match the real gateway.
    private static readonly JsonSerializerOptions GatewayJson = CreateGatewayJson();

    private static JsonSerializerOptions CreateGatewayJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private const string CompleteUpdateBody = """
        {
          "poNumber": "PO-88232",
          "issued": "2026-09-01",
          "expiry": "2027-03-31",
          "amountCad": 20000.00,
          "roundTripRateCad": 1600.00,
          "oneWayRateCad": 900.00,
          "note": "Leaf Rapids corridor"
        }
        """;

    [Fact]
    public void A_complete_update_body_binds_every_field()
    {
        var request = JsonSerializer.Deserialize<UpdatePurchaseOrderRequest>(CompleteUpdateBody, GatewayJson);

        Assert.NotNull(request);
        Assert.Equal("PO-88232", request!.PoNumber);
        Assert.Equal(new DateOnly(2026, 9, 1), request.Issued);
        Assert.Equal(new DateOnly(2027, 3, 31), request.Expiry);
        Assert.Equal(20000.00m, request.AmountCad);
        Assert.Equal(1600.00m, request.RoundTripRateCad);
        Assert.Equal(900.00m, request.OneWayRateCad);
        Assert.Equal("Leaf Rapids corridor", request.Note);
    }

    /// <summary>
    /// The case this whole contract exists for: omitting a rate key is rejected, not treated as
    /// "clear it". Asserted per money field, since each one is a separately negotiated figure.
    /// </summary>
    [Theory]
    [InlineData("roundTripRateCad")]
    [InlineData("oneWayRateCad")]
    [InlineData("amountCad")]
    public void An_update_body_missing_a_money_field_is_rejected(string omittedField)
    {
        var body = WithoutProperty(CompleteUpdateBody, omittedField);

        var exception = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<UpdatePurchaseOrderRequest>(body, GatewayJson));

        // The message names the member, so a 400's detail tells the caller what to send.
        Assert.Contains(omittedField, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("poNumber")]
    [InlineData("issued")]
    [InlineData("expiry")]
    [InlineData("note")]
    public void An_update_body_missing_any_other_field_is_also_rejected(string omittedField)
    {
        var body = WithoutProperty(CompleteUpdateBody, omittedField);

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<UpdatePurchaseOrderRequest>(body, GatewayJson));
    }

    [Fact]
    public void An_explicit_null_still_clears_a_rate()
    {
        // Clearing a term is a legitimate operation — the contract rate then applies. What is
        // forbidden is doing it by omission, where nobody can tell it was meant.
        const string body = """
            {
              "poNumber": "PO-88232",
              "issued": "2026-09-01",
              "expiry": null,
              "amountCad": null,
              "roundTripRateCad": null,
              "oneWayRateCad": null,
              "note": null
            }
            """;

        var request = JsonSerializer.Deserialize<UpdatePurchaseOrderRequest>(body, GatewayJson);

        Assert.NotNull(request);
        Assert.Null(request!.RoundTripRateCad);
        Assert.Null(request.OneWayRateCad);
        Assert.Null(request.AmountCad);
        Assert.Null(request.Expiry);
        Assert.Null(request.Note);
    }

    /// <summary>
    /// The CREATE body is deliberately permissive: recording a PO from nothing but a number and a
    /// date is legitimate, and the terms often arrive later. Pinned so the two bodies cannot be
    /// "tidied" into one and quietly take the update path's guarantee with them.
    /// </summary>
    [Fact]
    public void A_create_body_may_omit_every_optional_field()
    {
        const string body = """
            {
              "poNumber": "PO-90114",
              "issued": "2026-09-01"
            }
            """;

        var request = JsonSerializer.Deserialize<PurchaseOrderRequest>(body, GatewayJson);

        Assert.NotNull(request);
        Assert.Equal("PO-90114", request!.PoNumber);
        Assert.Equal(new DateOnly(2026, 9, 1), request.Issued);
        Assert.Null(request.AmountCad);
        Assert.Null(request.RoundTripRateCad);
        Assert.Null(request.OneWayRateCad);
        Assert.Null(request.Expiry);
        Assert.Null(request.Note);
    }

    /// <summary>Drops one top-level property from a JSON object, leaving the rest untouched.</summary>
    private static string WithoutProperty(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
