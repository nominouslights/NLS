using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.Setup;

/// <summary>Anonymous check of whether first-run setup (create the first admin) is still open.</summary>
public sealed record GetSetupStatusQuery : IQuery<SetupStatusResponse>;
