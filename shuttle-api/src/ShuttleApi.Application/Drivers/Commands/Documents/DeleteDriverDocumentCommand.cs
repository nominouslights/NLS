using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Drivers.Commands.Documents;

public sealed record DeleteDriverDocumentCommand(Guid DriverId, Guid DocumentId) : ICommand;
