using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Stops;

/// <summary>
/// A structured postal address for a stop. City, Province, and Country are required;
/// Street and PostalCode are optional (some communities/hubs have no civic address).
/// <see cref="Province"/> is always stored as its two-letter code — "Manitoba", "MB" and "Man."
/// all become "MB" on the way in (<see cref="CanadianProvince.Normalize"/>), and anything that is
/// not a Canadian province or territory is refused. That code is what the Manitoba-only
/// operating-area rule compares against when the stop is placed on a route.
/// Value object: structural equality, private construction via <see cref="Create"/>.
/// </summary>
public sealed record Address
{
    private Address(string? street, string city, string province, string? postalCode, string country)
    {
        Street = street;
        City = city;
        Province = province;
        PostalCode = postalCode;
        Country = country;
    }

    public string? Street { get; }
    public string City { get; }

    /// <summary>The province/territory code ("MB"). Rows written before normalisation may still hold a full name until the backfill runs.</summary>
    public string Province { get; }

    public string? PostalCode { get; }
    public string Country { get; }

    public static Result<Address> Create(
        string? street,
        string city,
        string province,
        string? postalCode,
        string country)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return Result.Failure<Address>(StopErrors.CityRequired);
        }

        if (string.IsNullOrWhiteSpace(province))
        {
            return Result.Failure<Address>(StopErrors.ProvinceRequired);
        }

        var provinceCode = CanadianProvince.Normalize(province);
        if (provinceCode is null)
        {
            return Result.Failure<Address>(StopErrors.UnknownProvince(province));
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            return Result.Failure<Address>(StopErrors.CountryRequired);
        }

        return Result.Success(new Address(
            Clean(street),
            city.Trim(),
            provinceCode,
            Clean(postalCode),
            country.Trim()));
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
