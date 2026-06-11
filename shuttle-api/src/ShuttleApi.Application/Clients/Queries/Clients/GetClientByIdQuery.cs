using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record GetClientByIdQuery(Guid Id) : IQuery<ClientDetailResult>;

public sealed record ClientDetailResult(
    Guid Id,
    string BusinessName,
    string ServiceType,
    string PrimaryContactName,
    string PrimaryContactTitle,
    string Phone,
    string Email,
    string StreetAddress,
    string City,
    string Province,
    string PostalCode,
    string? GstHstNumber,
    string PreferredPaymentMethod,
    int NetPaymentTerms,
    decimal OutstandingBalance,
    string? ComplianceNotes,
    bool IsMinesite,
    bool IsActive,
    DateTime CreatedAt,
    ContractSummaryResult? ActiveContract,
    string? Industry,
    string? ProjectSite,
    IReadOnlyList<string> NotificationEmails,
    IReadOnlyList<string> TripDepartureArrivalEmails,
    IReadOnlyList<string> PassengerBookingEmails);

public sealed record ContractSummaryResult(
    Guid Id,
    DateTime StartDate,
    DateTime EndDate,
    bool IsExpiringSoon,
    string? Notes,
    IReadOnlyList<RateLineResult> RateLines);

public sealed record RateLineResult(
    Guid Id,
    string BillingCode,
    string Description,
    string VehicleType,
    int? MaxDistanceKm,
    bool CargoIncluded,
    decimal DayRate);
