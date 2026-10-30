using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.BookeoImports;

/// <summary>
/// Pins a Bookeo <c>Unit</c> text ("Ford Transit 150") to one fleet vehicle — the first thing the
/// import's vehicle match tries. <see cref="UnitText"/> is stored normalized
/// (<see cref="BookeoText.NormalizeUnit"/>); unique per tenant.
/// </summary>
public sealed class BookeoUnitMapping : Entity, ITenantScoped
{
    private BookeoUnitMapping()
    {
        UnitText = null!;
    }

    public Guid TenantId { get; private set; }
    public string UnitText { get; private set; }
    public Guid VehicleId { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<BookeoUnitMapping> Create(Guid tenantId, string? unitText, Guid vehicleId)
    {
        var mapping = new BookeoUnitMapping { TenantId = tenantId };
        var result = mapping.Update(unitText, vehicleId);
        return result.IsFailure ? Result.Failure<BookeoUnitMapping>(result.Error) : Result.Success(mapping);
    }

    public Result Update(string? unitText, Guid vehicleId)
    {
        var normalized = BookeoText.NormalizeUnit(unitText);
        if (normalized is null)
        {
            return Result.Failure(BookeoImportErrors.UnitTextRequired);
        }

        if (vehicleId == Guid.Empty)
        {
            return Result.Failure(BookeoImportErrors.VehicleNotFound);
        }

        UnitText = normalized;
        VehicleId = vehicleId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}
