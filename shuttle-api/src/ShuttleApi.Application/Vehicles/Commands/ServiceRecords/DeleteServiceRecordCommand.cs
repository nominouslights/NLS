using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Commands.ServiceRecords;

public sealed record DeleteServiceRecordCommand(Guid VehicleId, Guid RecordId) : ICommand;
