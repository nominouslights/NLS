using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Integration;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// In-memory replicas with the EF repositories' semantics: upserts match on (key, tenant) and
/// overwrite every event-carried column but never <see cref="VehicleLookup.RegulatedOverride"/>;
/// request-path reads are scoped to <see cref="QueryFilterTenantId"/>.
/// </summary>
internal sealed class InMemoryLookupStore : IVehicleLookupRepository, IDriverLookupRepository, IUserLookupRepository
{
    public List<VehicleLookup> Vehicles { get; } = [];
    public List<DriverLookup> Drivers { get; } = [];
    public List<UserLookup> Users { get; } = [];

    public Guid QueryFilterTenantId { get; set; } = TestCompliance.TenantId;

    public Task<VehicleLookup?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Vehicles.FirstOrDefault(v => v.TenantId == QueryFilterTenantId && v.VehicleId == vehicleId));

    public Task<IReadOnlyList<VehicleLookup>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VehicleLookup>>(Vehicles.Where(v => v.TenantId == QueryFilterTenantId).ToList());

    public Task UpsertAsync(VehicleLookup vehicle, CancellationToken cancellationToken = default)
    {
        var existing = Vehicles.FirstOrDefault(v => v.VehicleId == vehicle.VehicleId && v.TenantId == vehicle.TenantId);
        if (existing is null)
        {
            vehicle.RegulatedOverride = null;
            Vehicles.Add(vehicle);
        }
        else
        {
            existing.UnitNumber = vehicle.UnitNumber;
            existing.Status = vehicle.Status;
            existing.SeatingCapacity = vehicle.SeatingCapacity;
            existing.RequiredLicenceClass = vehicle.RequiredLicenceClass;
            existing.UpdatedAtUtc = vehicle.UpdatedAtUtc;
        }

        return Task.CompletedTask;
    }

    public async Task<bool> SetRegulatedOverrideAsync(Guid vehicleId, bool? regulatedOverride, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(vehicleId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        existing.RegulatedOverride = regulatedOverride;
        return true;
    }

    Task<DriverLookup?> IDriverLookupRepository.GetAsync(Guid driverId, CancellationToken cancellationToken) =>
        Task.FromResult(Drivers.FirstOrDefault(d => d.TenantId == QueryFilterTenantId && d.DriverId == driverId));

    public Task<DriverLookup?> UpsertAsync(DriverLookup driver, CancellationToken cancellationToken = default)
    {
        var existing = Drivers.FirstOrDefault(d => d.DriverId == driver.DriverId && d.TenantId == driver.TenantId);
        if (existing is null)
        {
            Drivers.Add(driver);
            return Task.FromResult<DriverLookup?>(null);
        }

        var before = new DriverLookup
        {
            DriverId = existing.DriverId, TenantId = existing.TenantId, Name = existing.Name,
            Status = existing.Status, UserId = existing.UserId, UpdatedAtUtc = existing.UpdatedAtUtc,
        };
        existing.Name = driver.Name;
        existing.Status = driver.Status;
        existing.UserId = driver.UserId;
        existing.UpdatedAtUtc = driver.UpdatedAtUtc;
        return Task.FromResult<DriverLookup?>(before);
    }

    Task<UserLookup?> IUserLookupRepository.GetAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(u => u.TenantId == QueryFilterTenantId && u.UserId == userId));

    public Task UpsertAsync(UserLookup user, CancellationToken cancellationToken = default)
    {
        var existing = Users.FirstOrDefault(u => u.UserId == user.UserId && u.TenantId == user.TenantId);
        if (existing is null)
        {
            Users.Add(user);
        }
        else
        {
            existing.Email = user.Email;
            existing.FullName = user.FullName;
            existing.Role = user.Role;
            existing.UpdatedAtUtc = user.UpdatedAtUtc;
        }

        return Task.CompletedTask;
    }

    /// <summary>Seeds one vehicle as Fleet's event would.</summary>
    public VehicleLookup AddVehicle(Guid vehicleId, string unit = "101", int seating = 15, string status = "Active")
    {
        var vehicle = new VehicleLookup
        {
            VehicleId = vehicleId, TenantId = QueryFilterTenantId, UnitNumber = unit, Status = status,
            SeatingCapacity = seating, RequiredLicenceClass = "2", UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        Vehicles.Add(vehicle);
        return vehicle;
    }
}
