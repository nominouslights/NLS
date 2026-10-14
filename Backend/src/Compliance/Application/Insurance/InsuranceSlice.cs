using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Authority;
using NorthernLink.Compliance.Application.RuleSets;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Insurance;
using NorthernLink.Compliance.Domain.Signatures;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Insurance;

/// <summary>Records a vehicle's insurance certificate; refused when a Current policy exists (renew it instead).</summary>
public sealed record RecordInsurancePolicyCommand(Guid TenantId, Guid VehicleId, InsurancePolicyDetails Details, Guid? ActorId) : ICommand<Guid>;

/// <summary>Renews the Current policy <paramref name="PolicyId"/>: returns the successor's id.</summary>
public sealed record RenewInsurancePolicyCommand(Guid TenantId, Guid PolicyId, InsurancePolicyDetails Details, Guid? ActorId) : ICommand<Guid>;

/// <summary>Attaches an already-stored file to a policy's slot (<c>certificate</c> / <c>filing-confirmation</c>).</summary>
public sealed record AttachInsuranceDocumentCommand(Guid TenantId, Guid PolicyId, string Slot, AttachmentRef Attachment, Guid? ActorId) : ICommand;

public sealed record GetInsurancePolicyQuery(Guid TenantId, Guid PolicyId) : IQuery<InsurancePolicyResponse>;

public sealed record ListInsurancePoliciesQuery(Guid TenantId, Guid? VehicleId, bool IncludeSuperseded) : IQuery<IReadOnlyList<InsurancePolicyResponse>>;

/// <summary>The stored file in a policy's slot — the download endpoint streams it from object storage.</summary>
public sealed record GetInsuranceAttachmentQuery(Guid TenantId, Guid PolicyId, string Slot) : IQuery<AttachmentRef>;

/// <summary>A policy on the wire. <c>BelowMinimum</c> is true when the limit is under the minimum in force when it was recorded.</summary>
public sealed record InsurancePolicyResponse(
    Guid Id,
    Guid VehicleId,
    string VehicleUnitNumber,
    string Insurer,
    string PolicyNumber,
    decimal LiabilityLimitCad,
    decimal? CargoCoverageCad,
    decimal MinimumLiabilityCad,
    bool BelowMinimum,
    DateOnly EffectiveOn,
    DateOnly ExpiresOn,
    AttachmentResponse? Certificate,
    AttachmentResponse? FilingConfirmation,
    AuthorityDocumentStatus Status,
    Guid? SupersedesId,
    Guid? SupersededById,
    DateOnly? SupersededOn,
    Guid? ComplianceRecordId,
    string? DocumentNumber,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static InsurancePolicyResponse From(InsurancePolicy p) => new(
        p.Id, p.VehicleId, p.VehicleUnitNumber, p.Insurer, p.PolicyNumber, p.LiabilityLimitCad, p.CargoCoverageCad,
        p.MinimumLiabilityCad, p.IsBelowMinimum, p.EffectiveOn, p.ExpiresOn,
        AttachmentResponse.From(p.Certificate), AttachmentResponse.From(p.FilingConfirmation),
        p.Status, p.SupersedesId, p.SupersededById, p.SupersededOn, p.ComplianceRecordId, p.DocumentNumber,
        p.CreatedAtUtc, p.UpdatedAtUtc);
}

/// <summary>The insurance terms of the rule set in force today.</summary>
internal static class InsuranceTermsResolver
{
    public static async Task<InsuranceTerms> ResolveAsync(ActiveRuleSetResolver rules, CancellationToken cancellationToken)
    {
        var active = await rules.ResolveAsync(DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        return new InsuranceTerms(active.Document.Insurance.Insurer, active.Document.Insurance.MinimumLiabilityCad);
    }
}

/// <summary>
/// Handles <see cref="RecordInsurancePolicyCommand"/> — HTA s. 312.2(7); M.R. 132/2024 s. 7(1)(b);
/// NL-SP-02 s. 12.1. The vehicle must be in the replica (its unit number is the record's subject);
/// one Current policy per vehicle (the partial unique index is the backstop).
/// </summary>
public sealed class RecordInsurancePolicyCommandHandler(
    IInsurancePolicyRepository policies,
    IVehicleLookupRepository vehicles,
    ActiveRuleSetResolver rules,
    IComplianceRecordRepository unitOfWork)
    : ICommandHandler<RecordInsurancePolicyCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RecordInsurancePolicyCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await vehicles.GetAsync(command.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure<Guid>(ComplianceVehicleErrors.NotFound);
        }

        if (await policies.GetCurrentForVehicleAsync(command.VehicleId, cancellationToken) is not null)
        {
            return Result.Failure<Guid>(InsuranceErrors.CurrentExists);
        }

        var terms = await InsuranceTermsResolver.ResolveAsync(rules, cancellationToken);
        var policy = InsurancePolicy.Record(
            command.TenantId, command.VehicleId, vehicle.UnitNumber, command.Details, terms, command.ActorId, DateTimeOffset.UtcNow);
        if (policy.IsFailure)
        {
            return Result.Failure<Guid>(policy.Error);
        }

        policies.Add(policy.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(policy.Value.Id);
    }
}

/// <summary>
/// Handles <see cref="RenewInsurancePolicyCommand"/> — R20 "until renewed; superseded copies 2
/// years": the successor and the superseded predecessor save together, and the successor's
/// record-produced event names the predecessor so the registry supersedes its record too.
/// </summary>
public sealed class RenewInsurancePolicyCommandHandler(
    IInsurancePolicyRepository policies,
    ActiveRuleSetResolver rules,
    IComplianceRecordRepository unitOfWork)
    : ICommandHandler<RenewInsurancePolicyCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RenewInsurancePolicyCommand command, CancellationToken cancellationToken)
    {
        var current = await policies.GetByIdAsync(command.PolicyId, cancellationToken);
        if (current is null)
        {
            return Result.Failure<Guid>(InsuranceErrors.NotFound);
        }

        var terms = await InsuranceTermsResolver.ResolveAsync(rules, cancellationToken);
        var successor = current.Renew(command.Details, terms, command.ActorId, DateTimeOffset.UtcNow);
        if (successor.IsFailure)
        {
            return Result.Failure<Guid>(successor.Error);
        }

        policies.Add(successor.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(successor.Value.Id);
    }
}

/// <summary>Handles <see cref="AttachInsuranceDocumentCommand"/> — HTA ss. 312.2(7) (certificate), 312.4 (filing).</summary>
public sealed class AttachInsuranceDocumentCommandHandler(IInsurancePolicyRepository policies, IComplianceRecordRepository unitOfWork)
    : ICommandHandler<AttachInsuranceDocumentCommand>
{
    public async Task<Result> Handle(AttachInsuranceDocumentCommand command, CancellationToken cancellationToken)
    {
        var policy = await policies.GetByIdAsync(command.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Result.Failure(InsuranceErrors.NotFound);
        }

        var result = policy.Attach(command.Slot, command.Attachment, command.ActorId, DateTimeOffset.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class GetInsurancePolicyQueryHandler(IInsurancePolicyRepository policies)
    : IQueryHandler<GetInsurancePolicyQuery, InsurancePolicyResponse>
{
    public async Task<Result<InsurancePolicyResponse>> Handle(GetInsurancePolicyQuery query, CancellationToken cancellationToken)
    {
        var policy = await policies.GetByIdAsync(query.PolicyId, cancellationToken);
        return policy is null
            ? Result.Failure<InsurancePolicyResponse>(InsuranceErrors.NotFound)
            : Result.Success(InsurancePolicyResponse.From(policy));
    }
}

public sealed class ListInsurancePoliciesQueryHandler(IInsurancePolicyRepository policies)
    : IQueryHandler<ListInsurancePoliciesQuery, IReadOnlyList<InsurancePolicyResponse>>
{
    public async Task<Result<IReadOnlyList<InsurancePolicyResponse>>> Handle(ListInsurancePoliciesQuery query, CancellationToken cancellationToken)
    {
        var rows = await policies.ListAsync(query.VehicleId, query.IncludeSuperseded, cancellationToken);
        return Result.Success<IReadOnlyList<InsurancePolicyResponse>>(rows.Select(InsurancePolicyResponse.From).ToList());
    }
}

public sealed class GetInsuranceAttachmentQueryHandler(IInsurancePolicyRepository policies)
    : IQueryHandler<GetInsuranceAttachmentQuery, AttachmentRef>
{
    public async Task<Result<AttachmentRef>> Handle(GetInsuranceAttachmentQuery query, CancellationToken cancellationToken)
    {
        var policy = await policies.GetByIdAsync(query.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Result.Failure<AttachmentRef>(InsuranceErrors.NotFound);
        }

        var slot = policy.AttachmentIn(query.Slot);
        if (slot.IsFailure)
        {
            return Result.Failure<AttachmentRef>(slot.Error);
        }

        return slot.Value is { } attachment
            ? Result.Success(attachment)
            : Result.Failure<AttachmentRef>(AttachmentErrors.NotAttached);
    }
}
