using Microsoft.Extensions.Logging;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Contracts;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Clients;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Billing.Application.Integration;

/// <summary>
/// Maintains the <c>contract_snapshots</c> replica: every <c>clients.contract-changed</c>
/// event upserts the full billing-relevant snapshot keyed on ContractId, so replaying the
/// same event (delivery is at-least-once) converges on the same row — idempotent by
/// construction. Runs outside any HTTP request, so the tenant comes from the event
/// payload; <see cref="AmbientTenant.Push"/> makes the RLS session variable follow it on
/// the live server (the lookup still filters tenant explicitly, Fleet-consumer style,
/// because the context's query-filter tenant was captured before the push).
/// </summary>
public sealed class ContractChangedIntegrationEventHandler(
    IContractSnapshotRepository repository,
    ILogger<ContractChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<ContractChangedIntegrationEvent>
{
    public async Task Handle(ContractChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using var tenantScope = AmbientTenant.Push(integrationEvent.TenantId);

        var snapshot = await repository.GetByIdAsync(
            integrationEvent.TenantId, integrationEvent.ContractId, cancellationToken);

        var isNew = snapshot is null;
        if (snapshot is null)
        {
            snapshot = new ContractSnapshot
            {
                Id = integrationEvent.ContractId,
                TenantId = integrationEvent.TenantId,
            };
            repository.Add(snapshot);
        }

        snapshot.ClientId = integrationEvent.ClientId;
        snapshot.ClientName = integrationEvent.ClientName;
        snapshot.StartDate = integrationEvent.StartDate;
        snapshot.EndDate = integrationEvent.EndDate;
        snapshot.BillingModel = integrationEvent.BillingModel;
        snapshot.RatePerRoundTripCad = integrationEvent.RatePerRoundTripCad;
        snapshot.GstApplicable = integrationEvent.GstApplicable;
        snapshot.BudgetCode = integrationEvent.BudgetCode;
        snapshot.BillingFrequency = integrationEvent.BillingFrequency;
        snapshot.NetTermsDays = integrationEvent.NetTermsDays;
        snapshot.DefaultPoNumber = integrationEvent.DefaultPoNumber;
        snapshot.Status = integrationEvent.Status;
        snapshot.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Billing {Action} contract snapshot {ContractId} ({EventId}): client {ClientName}, model {BillingModel}, status {Status}",
            isNew ? "inserted" : "updated",
            integrationEvent.ContractId,
            integrationEvent.EventId,
            integrationEvent.ClientName,
            integrationEvent.BillingModel,
            integrationEvent.Status);
    }
}
