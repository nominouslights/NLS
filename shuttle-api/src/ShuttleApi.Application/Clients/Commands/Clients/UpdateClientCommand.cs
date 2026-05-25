using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Application.Clients;

public sealed record UpdateClientCommand(
    Guid Id,
    string BusinessName,
    ServiceType ServiceType,
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
    string? ComplianceNotes,
    bool IsMinesite,
    bool IsActive,
    string? Industry,
    string? ProjectSite) : ICommand;
