using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Vehicles;

/// <summary>
/// Vehicle Identification Number — exactly 17 characters, uppercase letters and digits,
/// excluding I, O, and Q (ISO 3779). Input is trimmed and uppercased before validation.
/// </summary>
public sealed record Vin(string Value)
{
    public const int Length = 17;

    public static Result<Vin> Create(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();

        if (normalized is null || normalized.Length != Length || !normalized.All(IsAllowedCharacter))
        {
            return Result.Failure<Vin>(VehicleErrors.InvalidVin);
        }

        return Result.Success(new Vin(normalized));
    }

    private static bool IsAllowedCharacter(char c) =>
        c is (>= '0' and <= '9') or (>= 'A' and <= 'Z' and not ('I' or 'O' or 'Q'));

    public override string ToString() => Value;
}
