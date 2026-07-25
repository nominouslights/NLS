using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Application.Contracts;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Read side — queries clients.rm_contracts and maps to the public contract.</summary>
internal sealed class ContractReadService(ClientsDbContext context) : IContractReadService
{
    public async Task<IReadOnlyList<ContractResponse>> GetForClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        var contracts = await context.ContractReadModels
            .AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);

        return contracts.Select(c => new ContractResponse(
            c.Id,
            c.ClientId,
            c.ClientName,
            c.StartDate,
            c.EndDate,
            c.BillingModel,
            c.RatePerRoundTripCad,
            c.GstApplicable,
            c.BudgetCode,
            c.BillingFrequency,
            c.NetTermsDays,
            c.DefaultPoNumber,
            c.Status,
            c.CreatedAtUtc,
            c.UpdatedAtUtc)).ToList();
    }
}
