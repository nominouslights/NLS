using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Drivers;

/// <summary>
/// Published when an out-of-service declaration is recorded or lifted — routing key
/// <c>drivers.driver-out-of-service-changed</c>. Upsert-shaped on <see cref="RecordId"/>;
/// <see cref="Status"/> is "Active" or "Lifted". <see cref="UntilUtc"/> is the instant the
/// declaration ends — the lift instant once lifted, the declared end otherwise, null while
/// open-ended. A consumer treats the driver as out of service while
/// <c>Status == Active &amp;&amp; (UntilUtc is null || now &lt; UntilUtc)</c>. Nobody consumes it
/// yet; Trips' <c>driver_lookup</c> replica is the planned consumer.
/// </summary>
public sealed record DriverOutOfServiceChangedIntegrationEvent(
    Guid RecordId,
    Guid TenantId,
    Guid DriverId,
    string Status,
    DateTimeOffset DeclaredAtUtc,
    DateTimeOffset? UntilUtc,
    string Reason) : IntegrationEvent;
