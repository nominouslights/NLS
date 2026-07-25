using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Documents;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Read side — queries the read model through fleet.v_vehicle_documents and maps to the public contract.</summary>
internal sealed class VehicleDocumentReadService(FleetDbContext context) : IVehicleDocumentReadService
{
    public async Task<IReadOnlyList<VehicleDocumentResponse>> GetForVehicleAsync(
        Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var documents = await context.VehicleDocumentReadModels
            .AsNoTracking()
            .Where(d => d.VehicleId == vehicleId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        return documents.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<VehicleDocumentResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var documents = await context.VehicleDocumentReadModels
            .AsNoTracking()
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        return documents.Select(ToResponse).ToList();
    }

    private static VehicleDocumentResponse ToResponse(VehicleDocumentReadModel d) => new(
        d.Id,
        d.VehicleId,
        d.Number,
        d.Type,
        d.FileName,
        d.FileSizeKb,
        d.UploadedBy,
        d.UploadedAt,
        d.Expiry,
        d.Note);
}
