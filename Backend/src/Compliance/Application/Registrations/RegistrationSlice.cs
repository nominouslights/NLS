using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Authority;
using NorthernLink.Compliance.Application.RuleSets;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Registrations;
using NorthernLink.Compliance.Domain.Signatures;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Registrations;

public sealed record RecordVehicleRegistrationCommand(Guid TenantId, Guid VehicleId, VehicleRegistrationDetails Details, Guid? ActorId) : ICommand<Guid>;

public sealed record RenewVehicleRegistrationCommand(Guid TenantId, Guid RegistrationId, VehicleRegistrationDetails Details, Guid? ActorId) : ICommand<Guid>;

public sealed record AttachRegistrationDocumentCommand(Guid TenantId, Guid RegistrationId, string Slot, AttachmentRef Attachment, Guid? ActorId) : ICommand;

public sealed record GetVehicleRegistrationQuery(Guid TenantId, Guid RegistrationId) : IQuery<VehicleRegistrationResponse>;

public sealed record ListVehicleRegistrationsQuery(Guid TenantId, Guid? VehicleId, bool IncludeSuperseded) : IQuery<IReadOnlyList<VehicleRegistrationResponse>>;

public sealed record GetRegistrationAttachmentQuery(Guid TenantId, Guid RegistrationId, string Slot) : IQuery<AttachmentRef>;

public sealed record VehicleRegistrationResponse(
    Guid Id,
    Guid VehicleId,
    string VehicleUnitNumber,
    string Plate,
    string VehicleClass,
    int TermMonths,
    DateOnly EffectiveOn,
    DateOnly ExpiresOn,
    AttachmentResponse? Document,
    AuthorityDocumentStatus Status,
    Guid? SupersedesId,
    Guid? SupersededById,
    DateOnly? SupersededOn,
    Guid? ComplianceRecordId,
    string? DocumentNumber,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static VehicleRegistrationResponse From(VehicleRegistration r) => new(
        r.Id, r.VehicleId, r.VehicleUnitNumber, r.Plate, r.VehicleClass, r.TermMonths, r.EffectiveOn, r.ExpiresOn,
        AttachmentResponse.From(r.Document), r.Status, r.SupersedesId, r.SupersededById, r.SupersededOn,
        r.ComplianceRecordId, r.DocumentNumber, r.CreatedAtUtc, r.UpdatedAtUtc);
}

internal static class RegistrationTerms
{
    /// <summary><c>authority.registrationTermMonths</c> of the rule set in force today.</summary>
    public static async Task<int> DefaultTermMonthsAsync(ActiveRuleSetResolver rules, CancellationToken cancellationToken)
    {
        var active = await rules.ResolveAsync(DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        return active.Document.EffectiveAuthority.RegistrationTermMonths;
    }
}

/// <summary>Handles <see cref="RecordVehicleRegistrationCommand"/> — NL-SP-02 s. 5.3; M.R. 76/94 s. 3(2)(a)(i); R20.</summary>
public sealed class RecordVehicleRegistrationCommandHandler(
    IVehicleRegistrationRepository registrations,
    IVehicleLookupRepository vehicles,
    ActiveRuleSetResolver rules,
    IComplianceRecordRepository unitOfWork)
    : ICommandHandler<RecordVehicleRegistrationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RecordVehicleRegistrationCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await vehicles.GetAsync(command.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure<Guid>(ComplianceVehicleErrors.NotFound);
        }

        if (await registrations.GetCurrentForVehicleAsync(command.VehicleId, cancellationToken) is not null)
        {
            return Result.Failure<Guid>(RegistrationErrors.CurrentExists);
        }

        var term = await RegistrationTerms.DefaultTermMonthsAsync(rules, cancellationToken);
        var registration = VehicleRegistration.Record(
            command.TenantId, command.VehicleId, vehicle.UnitNumber, command.Details, term, command.ActorId, DateTimeOffset.UtcNow);
        if (registration.IsFailure)
        {
            return Result.Failure<Guid>(registration.Error);
        }

        registrations.Add(registration.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(registration.Value.Id);
    }
}

/// <summary>Handles <see cref="RenewVehicleRegistrationCommand"/> — the successor and the superseded predecessor save together.</summary>
public sealed class RenewVehicleRegistrationCommandHandler(
    IVehicleRegistrationRepository registrations,
    ActiveRuleSetResolver rules,
    IComplianceRecordRepository unitOfWork)
    : ICommandHandler<RenewVehicleRegistrationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RenewVehicleRegistrationCommand command, CancellationToken cancellationToken)
    {
        var current = await registrations.GetByIdAsync(command.RegistrationId, cancellationToken);
        if (current is null)
        {
            return Result.Failure<Guid>(RegistrationErrors.NotFound);
        }

        var term = await RegistrationTerms.DefaultTermMonthsAsync(rules, cancellationToken);
        var successor = current.Renew(command.Details, term, command.ActorId, DateTimeOffset.UtcNow);
        if (successor.IsFailure)
        {
            return Result.Failure<Guid>(successor.Error);
        }

        registrations.Add(successor.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(successor.Value.Id);
    }
}

public sealed class AttachRegistrationDocumentCommandHandler(IVehicleRegistrationRepository registrations, IComplianceRecordRepository unitOfWork)
    : ICommandHandler<AttachRegistrationDocumentCommand>
{
    public async Task<Result> Handle(AttachRegistrationDocumentCommand command, CancellationToken cancellationToken)
    {
        var registration = await registrations.GetByIdAsync(command.RegistrationId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure(RegistrationErrors.NotFound);
        }

        var result = registration.Attach(command.Slot, command.Attachment, command.ActorId, DateTimeOffset.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class GetVehicleRegistrationQueryHandler(IVehicleRegistrationRepository registrations)
    : IQueryHandler<GetVehicleRegistrationQuery, VehicleRegistrationResponse>
{
    public async Task<Result<VehicleRegistrationResponse>> Handle(GetVehicleRegistrationQuery query, CancellationToken cancellationToken)
    {
        var registration = await registrations.GetByIdAsync(query.RegistrationId, cancellationToken);
        return registration is null
            ? Result.Failure<VehicleRegistrationResponse>(RegistrationErrors.NotFound)
            : Result.Success(VehicleRegistrationResponse.From(registration));
    }
}

public sealed class ListVehicleRegistrationsQueryHandler(IVehicleRegistrationRepository registrations)
    : IQueryHandler<ListVehicleRegistrationsQuery, IReadOnlyList<VehicleRegistrationResponse>>
{
    public async Task<Result<IReadOnlyList<VehicleRegistrationResponse>>> Handle(ListVehicleRegistrationsQuery query, CancellationToken cancellationToken)
    {
        var rows = await registrations.ListAsync(query.VehicleId, query.IncludeSuperseded, cancellationToken);
        return Result.Success<IReadOnlyList<VehicleRegistrationResponse>>(rows.Select(VehicleRegistrationResponse.From).ToList());
    }
}

public sealed class GetRegistrationAttachmentQueryHandler(IVehicleRegistrationRepository registrations)
    : IQueryHandler<GetRegistrationAttachmentQuery, AttachmentRef>
{
    public async Task<Result<AttachmentRef>> Handle(GetRegistrationAttachmentQuery query, CancellationToken cancellationToken)
    {
        var registration = await registrations.GetByIdAsync(query.RegistrationId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<AttachmentRef>(RegistrationErrors.NotFound);
        }

        var slot = registration.AttachmentIn(query.Slot);
        if (slot.IsFailure)
        {
            return Result.Failure<AttachmentRef>(slot.Error);
        }

        return slot.Value is { } attachment
            ? Result.Success(attachment)
            : Result.Failure<AttachmentRef>(AttachmentErrors.NotAttached);
    }
}
