using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Stops;

/// <summary>
/// A structured postal address for a stop. City, Province, and Country are required;
/// Street and PostalCode are optional (some communities/hubs have no civic address).
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

        if (string.IsNullOrWhiteSpace(country))
        {
            return Result.Failure<Address>(StopErrors.CountryRequired);
        }

        return Result.Success(new Address(
            Clean(street),
            city.Trim(),
            province.Trim(),
            Clean(postalCode),
            country.Trim()));
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
