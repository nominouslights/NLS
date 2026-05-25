using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Drivers.Queries.Drivers;

namespace ShuttleApi.Application.Drivers.Queries.Documents;

public sealed record GetDriverDocumentsQuery(Guid DriverId) : IQuery<IReadOnlyList<DriverDocumentResult>>;
