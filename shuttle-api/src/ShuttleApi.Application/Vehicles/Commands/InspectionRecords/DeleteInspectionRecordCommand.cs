using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Commands.InspectionRecords;

public sealed record DeleteInspectionRecordCommand(Guid VehicleId, Guid RecordId) : ICommand;
