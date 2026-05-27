using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Commands.ServiceRecords;

public sealed record CompleteServiceRecordCommand(
    Guid VehicleId,
    Guid RecordId,
    DateTime CompletedDate,
    decimal? ActualCostDollars,
    int? OdometerAtService) : ICommand;
