using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Vehicles;

namespace NorthernLink.Fleet.Application.Vehicles.GetRetirementCertificate;

/// <summary>Fetches the retirement certificate issued for a vehicle, if any.</summary>
public sealed record GetRetirementCertificateQuery(Guid TenantId, Guid VehicleId) : IQuery<RetirementCertificateResponse>;
