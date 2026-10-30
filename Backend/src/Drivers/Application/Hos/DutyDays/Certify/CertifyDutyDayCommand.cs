using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.DutyDays.Certify;

/// <summary>
/// The driver certifies their own day (s.84). The endpoint proves the caller IS this driver
/// (no dispatch override — nobody certifies on a driver's behalf) before dispatching; the
/// certifier recorded is the token's user id.
/// </summary>
public sealed record CertifyDutyDayCommand(Guid TenantId, Guid DriverId, DateOnly LocalDate) : ICommand;
