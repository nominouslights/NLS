using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Setup;

public sealed record GetSetupStatusQuery : IQuery<SetupStatusResult>;

public sealed record SetupStatusResult(bool IsSetupComplete);
