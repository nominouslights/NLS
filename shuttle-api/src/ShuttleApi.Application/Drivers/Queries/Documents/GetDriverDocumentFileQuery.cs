using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Drivers.Queries.Documents;

public sealed record GetDriverDocumentFileQuery(Guid DriverId, Guid DocumentId) : IQuery<DriverDocumentFileResult>;

public sealed record DriverDocumentFileResult(string FileName, string ContentType, byte[] Data);
