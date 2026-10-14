using Microsoft.EntityFrameworkCore;
using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Integration;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Insurance;
using NorthernLink.Compliance.Domain.Registrations;

namespace NorthernLink.Compliance.Infrastructure.Persistence;

/// <summary>The tenant's single authority row (tenant query filter + RLS); saves go through the record repository's unit of work.</summary>
internal sealed class CarrierAuthorityRepository(ComplianceDbContext context) : ICarrierAuthorityRepository
{
    public Task<CarrierAuthority?> GetAsync(CancellationToken cancellationToken = default) =>
        context.CarrierAuthorities.FirstOrDefaultAsync(cancellationToken);

    public void Add(CarrierAuthority authority) => context.CarrierAuthorities.Add(authority);
}

internal sealed class InsurancePolicyRepository(ComplianceDbContext context) : IInsurancePolicyRepository
{
    public Task<InsurancePolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.InsurancePolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<InsurancePolicy?> GetCurrentForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        context.InsurancePolicies.FirstOrDefaultAsync(
            p => p.VehicleId == vehicleId && p.Status == AuthorityDocumentStatus.Current, cancellationToken);

    public async Task<IReadOnlyList<InsurancePolicy>> ListAsync(Guid? vehicleId, bool includeSuperseded, CancellationToken cancellationToken = default)
    {
        var query = context.InsurancePolicies.AsNoTracking();
        if (vehicleId is { } id)
        {
            query = query.Where(p => p.VehicleId == id);
        }

        if (!includeSuperseded)
        {
            query = query.Where(p => p.Status == AuthorityDocumentStatus.Current);
        }

        return await query.OrderBy(p => p.VehicleUnitNumber).ThenByDescending(p => p.EffectiveOn).ToListAsync(cancellationToken);
    }

    public void Add(InsurancePolicy policy) => context.InsurancePolicies.Add(policy);
}

internal sealed class VehicleRegistrationRepository(ComplianceDbContext context) : IVehicleRegistrationRepository
{
    public Task<VehicleRegistration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.VehicleRegistrations.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<VehicleRegistration?> GetCurrentForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        context.VehicleRegistrations.FirstOrDefaultAsync(
            r => r.VehicleId == vehicleId && r.Status == AuthorityDocumentStatus.Current, cancellationToken);

    public async Task<IReadOnlyList<VehicleRegistration>> ListAsync(Guid? vehicleId, bool includeSuperseded, CancellationToken cancellationToken = default)
    {
        var query = context.VehicleRegistrations.AsNoTracking();
        if (vehicleId is { } id)
        {
            query = query.Where(r => r.VehicleId == id);
        }

        if (!includeSuperseded)
        {
            query = query.Where(r => r.Status == AuthorityDocumentStatus.Current);
        }

        return await query.OrderBy(r => r.VehicleUnitNumber).ThenByDescending(r => r.EffectiveOn).ToListAsync(cancellationToken);
    }

    public void Add(VehicleRegistration registration) => context.VehicleRegistrations.Add(registration);
}

/// <summary>
/// Replica upserts over <see cref="ComplianceDbContext"/> (the Budgeting user_lookup precedent).
/// Request-path reads go through the tenant query filter. The upserts run from integration
/// handlers: the polling consumer resolves their scope inside the event's ambient tenant, but the
/// upsert still bypasses the filter and matches on (key, tenant) explicitly, so a handler driven
/// any other way cannot silently insert a duplicate. RLS scopes the statement to the event's
/// tenant either way. Every event-carried column is reassigned on update — the event is a full
/// snapshot — and nothing else is: <c>regulated_override</c> is Compliance's own.
/// </summary>
internal sealed class VehicleLookupRepository(ComplianceDbContext context) : IVehicleLookupRepository
{
    public Task<VehicleLookup?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        context.VehicleLookups.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == vehicleId, cancellationToken);

    public async Task<IReadOnlyList<VehicleLookup>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.VehicleLookups.AsNoTracking().OrderBy(v => v.UnitNumber).ToListAsync(cancellationToken);

    public async Task UpsertAsync(VehicleLookup vehicle, CancellationToken cancellationToken = default)
    {
        var existing = await context.VehicleLookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.VehicleId == vehicle.VehicleId && v.TenantId == vehicle.TenantId, cancellationToken);

        if (existing is null)
        {
            vehicle.RegulatedOverride = null;
            context.VehicleLookups.Add(vehicle);
        }
        else
        {
            existing.UnitNumber = vehicle.UnitNumber;
            existing.Status = vehicle.Status;
            existing.SeatingCapacity = vehicle.SeatingCapacity;
            existing.RequiredLicenceClass = vehicle.RequiredLicenceClass;
            existing.UpdatedAtUtc = vehicle.UpdatedAtUtc;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SetRegulatedOverrideAsync(Guid vehicleId, bool? regulatedOverride, CancellationToken cancellationToken = default)
    {
        // Request path: the query filter scopes this to the caller's tenant.
        var existing = await context.VehicleLookups.FirstOrDefaultAsync(v => v.VehicleId == vehicleId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (existing.RegulatedOverride != regulatedOverride)
        {
            existing.RegulatedOverride = regulatedOverride;
            await context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}

internal sealed class DriverLookupRepository(ComplianceDbContext context) : IDriverLookupRepository
{
    public Task<DriverLookup?> GetAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        context.DriverLookups.AsNoTracking().FirstOrDefaultAsync(d => d.DriverId == driverId, cancellationToken);

    public async Task<DriverLookup?> UpsertAsync(DriverLookup driver, CancellationToken cancellationToken = default)
    {
        var existing = await context.DriverLookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.DriverId == driver.DriverId && d.TenantId == driver.TenantId, cancellationToken);

        DriverLookup? before = null;
        if (existing is null)
        {
            context.DriverLookups.Add(driver);
        }
        else
        {
            before = new DriverLookup
            {
                DriverId = existing.DriverId,
                TenantId = existing.TenantId,
                Name = existing.Name,
                Status = existing.Status,
                UserId = existing.UserId,
                UpdatedAtUtc = existing.UpdatedAtUtc,
            };
            existing.Name = driver.Name;
            existing.Status = driver.Status;
            existing.UserId = driver.UserId;
            existing.UpdatedAtUtc = driver.UpdatedAtUtc;
        }

        await context.SaveChangesAsync(cancellationToken);
        return before;
    }
}

internal sealed class UserLookupRepository(ComplianceDbContext context) : IUserLookupRepository
{
    public Task<UserLookup?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.UserLookups.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

    public async Task UpsertAsync(UserLookup user, CancellationToken cancellationToken = default)
    {
        var existing = await context.UserLookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == user.UserId && u.TenantId == user.TenantId, cancellationToken);

        if (existing is null)
        {
            context.UserLookups.Add(user);
        }
        else
        {
            existing.Email = user.Email;
            existing.FullName = user.FullName;
            existing.Role = user.Role;
            existing.UpdatedAtUtc = user.UpdatedAtUtc;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
