using NorthernLink.Shared.Kernel;
using NorthernLink.Clients.Domain.Contracts.Events;

namespace NorthernLink.Clients.Domain.Contracts;

/// <summary>
/// A client's billing contract — the source of truth invoicing prices from. Period is
/// <see cref="StartDate"/>..<see cref="EndDate"/> <b>inclusive</b>; a null end date means
/// evergreen (open-ended). <see cref="ClientName"/> is a denormalized snapshot taken at
/// creation (the create handler looks it up) so the full-snapshot
/// <c>ContractChangedIntegrationEvent</c> never needs a join. <see cref="BudgetCode"/>
/// implements the platform's tag-at-creation budget-code rule for anything touching money.
/// Every state change is public contract for Billing, so create/update/terminate all raise
/// events the mapper turns into <c>ContractChangedIntegrationEvent</c>.
/// </summary>
public sealed class Contract : AggregateRoot, ITenantScoped
{
    private Contract()
    {
        // EF Core materialization only.
        ClientName = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid ClientId { get; private set; }
    public string ClientName { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public BillingModel BillingModel { get; private set; }
    public decimal? RatePerRoundTripCad { get; private set; }
    public bool GstApplicable { get; private set; }
    public string? BudgetCode { get; private set; }
    public BillingFrequency BillingFrequency { get; private set; }
    public int NetTermsDays { get; private set; }
    public string? DefaultPoNumber { get; private set; }
    public ContractStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<Contract> Create(
        Guid tenantId,
        Guid clientId,
        string clientName,
        DateOnly startDate,
        DateOnly? endDate,
        BillingModel billingModel,
        decimal? ratePerRoundTripCad,
        bool gstApplicable,
        string? budgetCode,
        BillingFrequency billingFrequency,
        int netTermsDays,
        string? defaultPoNumber)
    {
        if (ValidateTerms(startDate, endDate, billingModel, ratePerRoundTripCad, netTermsDays) is { } error)
        {
            return Result.Failure<Contract>(error);
        }

        var now = DateTimeOffset.UtcNow;
        var contract = new Contract
        {
            TenantId = tenantId,
            ClientId = clientId,
            ClientName = clientName.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            BillingModel = billingModel,
            RatePerRoundTripCad = ratePerRoundTripCad,
            GstApplicable = gstApplicable,
            BudgetCode = Clean(budgetCode),
            BillingFrequency = billingFrequency,
            NetTermsDays = netTermsDays,
            DefaultPoNumber = Clean(defaultPoNumber),
            Status = ContractStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        contract.Raise(new ContractActivatedDomainEvent(contract.Id, tenantId));
        return Result.Success(contract);
    }

    public Result Update(
        DateOnly startDate,
        DateOnly? endDate,
        BillingModel billingModel,
        decimal? ratePerRoundTripCad,
        bool gstApplicable,
        string? budgetCode,
        BillingFrequency billingFrequency,
        int netTermsDays,
        string? defaultPoNumber)
    {
        if (Status != ContractStatus.Active)
        {
            return Result.Failure(ContractErrors.NotActive);
        }

        if (ValidateTerms(startDate, endDate, billingModel, ratePerRoundTripCad, netTermsDays) is { } error)
        {
            return Result.Failure(error);
        }

        StartDate = startDate;
        EndDate = endDate;
        BillingModel = billingModel;
        RatePerRoundTripCad = ratePerRoundTripCad;
        GstApplicable = gstApplicable;
        BudgetCode = Clean(budgetCode);
        BillingFrequency = billingFrequency;
        NetTermsDays = netTermsDays;
        DefaultPoNumber = Clean(defaultPoNumber);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new ContractUpdatedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    public Result Terminate()
    {
        if (Status != ContractStatus.Active)
        {
            return Result.Failure(ContractErrors.NotActive);
        }

        Status = ContractStatus.Terminated;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new ContractTerminatedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    /// <summary>
    /// Whether this contract's inclusive period intersects the given inclusive period.
    /// Null end dates are open-ended, so an evergreen contract overlaps everything from
    /// its start date forward — and a same-day boundary (one ends the day the other
    /// starts) counts as an overlap.
    /// </summary>
    public bool Overlaps(DateOnly otherStart, DateOnly? otherEnd) =>
        (EndDate is not { } end || otherStart <= end)
        && (otherEnd is not { } oEnd || StartDate <= oEnd);

    /// <summary>Whether this contract's period contains the given date (inclusive).</summary>
    public bool Covers(DateOnly date) =>
        StartDate <= date && (EndDate is not { } end || date <= end);

    private static Error? ValidateTerms(
        DateOnly startDate,
        DateOnly? endDate,
        BillingModel billingModel,
        decimal? ratePerRoundTripCad,
        int netTermsDays)
    {
        if (endDate is { } end && end < startDate)
        {
            return ContractErrors.InvalidPeriod;
        }

        if (billingModel == BillingModel.RoundTripRate)
        {
            if (ratePerRoundTripCad is not { } rate)
            {
                return ContractErrors.RateRequired;
            }

            if (rate <= 0)
            {
                return ContractErrors.InvalidRate;
            }
        }
        else if (ratePerRoundTripCad is not null)
        {
            return ContractErrors.RateNotAllowed;
        }

        if (netTermsDays <= 0)
        {
            return ContractErrors.InvalidNetTerms;
        }

        return null;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
