using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;

namespace NorthernLink.Fleet.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="RetirementCertificate"/> into <c>fleet.rm_retirement_certificates</c>.
///
/// The one projection that can't use <see cref="FleetProjection{TSource,TRead}"/>: certificates
/// are created inline during a vehicle's retirement and carry no journal of their own, so this
/// rides the <b>vehicle</b> aggregate's journal rows. The incoming aggregate id is therefore a
/// vehicle id, and it fans out to that vehicle's certificates rather than mapping one-to-one.
/// </summary>
internal sealed class RetirementCertificateProjection : IProjection<FleetDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(Vehicle));

    public async Task ApplyAsync(FleetDbContext context, Guid vehicleId, CancellationToken cancellationToken)
    {
        var certificates = await context.RetirementCertificates
            .IgnoreQueryFilters()
            .Where(certificate => certificate.VehicleId == vehicleId)
            .ToListAsync(cancellationToken);

        var rows = await context.RetirementCertificateReadModels
            .IgnoreQueryFilters()
            .Where(row => row.VehicleId == vehicleId)
            .ToListAsync(cancellationToken);

        Sync(context, certificates, rows);
    }

    public async Task RebuildAllAsync(FleetDbContext context, CancellationToken cancellationToken)
    {
        var certificates = await context.RetirementCertificates
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        var rows = await context.RetirementCertificateReadModels
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        Sync(context, certificates, rows);
    }

    /// <summary>Upserts every certificate in <paramref name="certificates"/> and drops any read
    /// row in <paramref name="rows"/> that no longer has a source certificate.</summary>
    private static void Sync(
        FleetDbContext context,
        List<RetirementCertificate> certificates,
        List<RetirementCertificateReadModel> rows)
    {
        var rowsById = rows.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();

        foreach (var certificate in certificates)
        {
            seen.Add(certificate.Id);

            if (rowsById.TryGetValue(certificate.Id, out var existing))
            {
                Map(certificate, existing);
            }
            else
            {
                var fresh = new RetirementCertificateReadModel();
                Map(certificate, fresh);
                context.RetirementCertificateReadModels.Add(fresh);
            }
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.RetirementCertificateReadModels.Remove(row);
            }
        }
    }

    private static void Map(RetirementCertificate source, RetirementCertificateReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.VehicleId = source.VehicleId;
        row.CertificateNumber = source.CertificateNumber;
        row.Vin = source.Vin;
        row.UnitNumber = source.UnitNumber;
        row.Make = source.Make;
        row.Model = source.Model;
        row.Year = source.Year;
        row.FinalOdometerKm = source.FinalOdometerKm;
        row.RetirementReason = source.RetirementReason;
        row.RetiredAtUtc = source.RetiredAtUtc;
        row.IssuedAtUtc = source.IssuedAtUtc;
    }
}
