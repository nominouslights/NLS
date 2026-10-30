namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>Lifecycle of a <see cref="DriverOutOfService"/> declaration. Member names travel on the wire.</summary>
public enum OutOfServiceStatus
{
    /// <summary>In force until <c>UntilUtc</c> passes or compliance lifts it.</summary>
    Active,

    /// <summary>Lifted by a compliance officer (early lifts carry a reason).</summary>
    Lifted,
}
