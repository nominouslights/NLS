using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.BookeoImports;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.BookeoImports.Mappings;

/// <summary>Lists the tenant's Bookeo product → route mappings, with each route's current name.</summary>
public sealed record GetBookeoProductMappingsQuery(Guid TenantId) : IQuery<IReadOnlyList<BookeoProductMappingResponse>>;

/// <summary>One product mapping to create or update, keyed by (productCode, destination).</summary>
public sealed record BookeoProductMappingInput(
    string? ProductCode,
    string? ProductName,
    string? Destination,
    Guid RouteId,
    TripDirection? Direction,
    ResidentStopRole ResidentStopRole);

/// <summary>
/// Upserts product mappings by (productCode, destination — case-insensitive, null = any) and
/// returns the full list. All-or-nothing: one invalid entry saves none.
/// </summary>
public sealed record UpsertBookeoProductMappingsCommand(
    Guid TenantId,
    IReadOnlyList<BookeoProductMappingInput> Mappings) : ICommand<IReadOnlyList<BookeoProductMappingResponse>>;

public sealed record DeleteBookeoProductMappingCommand(Guid TenantId, Guid MappingId) : ICommand;

/// <summary>Lists the tenant's Bookeo unit-text → vehicle mappings, with each vehicle's unit number.</summary>
public sealed record GetBookeoUnitMappingsQuery(Guid TenantId) : IQuery<IReadOnlyList<BookeoUnitMappingResponse>>;

public sealed record BookeoUnitMappingInput(string? UnitText, Guid VehicleId);

/// <summary>Upserts unit mappings by normalized unit text and returns the full list. All-or-nothing.</summary>
public sealed record UpsertBookeoUnitMappingsCommand(
    Guid TenantId,
    IReadOnlyList<BookeoUnitMappingInput> Mappings) : ICommand<IReadOnlyList<BookeoUnitMappingResponse>>;

public sealed record DeleteBookeoUnitMappingCommand(Guid TenantId, Guid MappingId) : ICommand;

/// <summary>The most recent uploads (newest first), committed or not — the History tab.</summary>
public sealed record GetBookeoImportBatchesQuery(Guid TenantId, int Take) : IQuery<IReadOnlyList<BookeoImportBatchResponse>>;
