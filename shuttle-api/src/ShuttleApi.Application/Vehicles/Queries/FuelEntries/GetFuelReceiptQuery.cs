using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Queries.FuelEntries;

public sealed record GetFuelReceiptQuery(Guid VehicleId, Guid EntryId)
    : IQuery<FuelReceiptResult?>;

public sealed record FuelReceiptResult(byte[] Bytes, string ContentType, string FileName);
