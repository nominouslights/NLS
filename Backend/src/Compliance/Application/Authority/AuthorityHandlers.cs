using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Signatures;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Authority;

/// <summary>The one-per-tenant authority, opened on first write.</summary>
internal static class CarrierAuthorityLoader
{
    public static async Task<CarrierAuthority> GetOrOpenAsync(
        ICarrierAuthorityRepository authorities, Guid tenantId, Guid? actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var authority = await authorities.GetAsync(cancellationToken);
        if (authority is null)
        {
            authority = CarrierAuthority.Open(tenantId, actorId, now);
            authorities.Add(authority);
        }

        return authority;
    }

    /// <summary>Saves only when the aggregate changed — a no-op command writes nothing.</summary>
    public static Task SaveIfChangedAsync(CarrierAuthority authority, IComplianceRecordRepository unitOfWork, CancellationToken cancellationToken) =>
        authority.DomainEvents.Count > 0 ? unitOfWork.SaveChangesAsync(cancellationToken) : Task.CompletedTask;
}

/// <summary>Handles <see cref="GetCarrierAuthorityQuery"/> — NL-SP-02 ss. 1–2.</summary>
public sealed class GetCarrierAuthorityQueryHandler(ICarrierAuthorityRepository authorities)
    : IQueryHandler<GetCarrierAuthorityQuery, CarrierAuthorityResponse>
{
    public async Task<Result<CarrierAuthorityResponse>> Handle(GetCarrierAuthorityQuery query, CancellationToken cancellationToken)
    {
        var authority = await authorities.GetAsync(cancellationToken);
        return Result.Success(authority is null ? CarrierAuthorityResponse.Empty : CarrierAuthorityResponse.From(authority, query.Today));
    }
}

/// <summary>
/// Handles <see cref="RecordSfcCommand"/> — HTA s. 312.2(1); MTI SFC pages; NL-SP-02 s. 2. A new
/// certificate is an R20 record: the aggregate's event becomes a record-produced integration event
/// in this module's outbox, naming the certificate it supersedes.
/// </summary>
public sealed class RecordSfcCommandHandler(ICarrierAuthorityRepository authorities, IComplianceRecordRepository unitOfWork)
    : ICommandHandler<RecordSfcCommand>
{
    public async Task<Result> Handle(RecordSfcCommand command, CancellationToken cancellationToken)
    {
        if (command.IssuedOn is not { } issuedOn || command.ExpiresOn is not { } expiresOn)
        {
            return Result.Failure(CarrierAuthorityErrors.SfcDatesRequired);
        }

        var now = DateTimeOffset.UtcNow;
        var authority = await CarrierAuthorityLoader.GetOrOpenAsync(authorities, command.TenantId, command.ActorId, now, cancellationToken);
        var result = authority.RecordSfc(command.Number, issuedOn, expiresOn, command.ActorId, now);
        if (result.IsFailure)
        {
            return result;
        }

        await CarrierAuthorityLoader.SaveIfChangedAsync(authority, unitOfWork, cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Handles <see cref="RecordNscNumberCommand"/> — M.R. 132/2024 s. 9; NL-SP-02 s. 2. The authority
/// is the system of record; the settings row's print-header copy is updated in the <b>same</b>
/// SaveChanges (settings are created with the defaults when the tenant has none yet).
/// </summary>
public sealed class RecordNscNumberCommandHandler(
    ICarrierAuthorityRepository authorities,
    ITenantComplianceSettingsRepository settings,
    IComplianceRecordRepository unitOfWork)
    : ICommandHandler<RecordNscNumberCommand>
{
    public async Task<Result> Handle(RecordNscNumberCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var authority = await CarrierAuthorityLoader.GetOrOpenAsync(authorities, command.TenantId, command.ActorId, now, cancellationToken);
        var recorded = authority.RecordNscNumber(command.NscNumber, command.ActorId, now);
        if (recorded.IsFailure)
        {
            return Result.Failure(recorded.Error);
        }

        var row = await settings.GetAsync(cancellationToken);
        if (row is null)
        {
            row = TenantComplianceSettings.CreateDefault(command.TenantId, command.ActorId);
            settings.Add(row);
        }

        row.CopyNscNumberFromAuthority(authority.NscNumber!, command.ActorId);

        if (authority.DomainEvents.Count > 0 || row.DomainEvents.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

/// <summary>Handles <see cref="SetSafetyRatingCommand"/> — MTI SFC ratings; NL-SP-02 s. 2.</summary>
public sealed class SetSafetyRatingCommandHandler(ICarrierAuthorityRepository authorities, IComplianceRecordRepository unitOfWork)
    : ICommandHandler<SetSafetyRatingCommand>
{
    public async Task<Result> Handle(SetSafetyRatingCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var authority = await CarrierAuthorityLoader.GetOrOpenAsync(authorities, command.TenantId, command.ActorId, now, cancellationToken);
        var result = authority.SetSafetyRating(command.Rating, command.AsOf, command.ActorId, now);
        if (result.IsFailure)
        {
            return result;
        }

        await CarrierAuthorityLoader.SaveIfChangedAsync(authority, unitOfWork, cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Handles <see cref="DeclareIntraProvincialCommand"/> — NL-SP-02 s. 1 and the Declaration. The
/// signature is stamped with the signed-in user and the server's clock; the statement is kept verbatim.
/// </summary>
public sealed class DeclareIntraProvincialCommandHandler(ICarrierAuthorityRepository authorities, IComplianceRecordRepository unitOfWork)
    : ICommandHandler<DeclareIntraProvincialCommand>
{
    public async Task<Result> Handle(DeclareIntraProvincialCommand command, CancellationToken cancellationToken)
    {
        if (command.DeclaredOn is not { } declaredOn)
        {
            return Result.Failure(CarrierAuthorityErrors.DeclarationDateRequired);
        }

        var now = DateTimeOffset.UtcNow;
        var input = command.Signature;
        var signature = SignatureBlock.Create(
            input.Role, input.SignerName, command.ActorId, input.Phone, input.Email, declaredOn, now, input.Statement, input.ImageKey);
        if (signature.IsFailure)
        {
            return Result.Failure(signature.Error);
        }

        var authority = await CarrierAuthorityLoader.GetOrOpenAsync(authorities, command.TenantId, command.ActorId, now, cancellationToken);
        var result = authority.DeclareIntraProvincial(declaredOn, signature.Value, command.ActorId, now);
        if (result.IsFailure)
        {
            return result;
        }

        await CarrierAuthorityLoader.SaveIfChangedAsync(authority, unitOfWork, cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Handles <see cref="AttachSfcCertificateCommand"/> — NL-SP-02 s. 2 (a copy of the SFC is kept and
/// carried). No authority yet means no certificate, so this never opens one.
/// </summary>
public sealed class AttachSfcCertificateCommandHandler(ICarrierAuthorityRepository authorities, IComplianceRecordRepository unitOfWork)
    : ICommandHandler<AttachSfcCertificateCommand>
{
    public async Task<Result> Handle(AttachSfcCertificateCommand command, CancellationToken cancellationToken)
    {
        var authority = await authorities.GetAsync(cancellationToken);
        if (authority is null)
        {
            return Result.Failure(CarrierAuthorityErrors.SfcNotCurrent);
        }

        var result = authority.AttachSfcCertificate(command.CertificateId, command.Attachment, command.ActorId, DateTimeOffset.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Handles <see cref="GetSfcAttachmentQuery"/> — current or superseded certificates alike (R20 keeps both).</summary>
public sealed class GetSfcAttachmentQueryHandler(ICarrierAuthorityRepository authorities)
    : IQueryHandler<GetSfcAttachmentQuery, AttachmentRef>
{
    public async Task<Result<AttachmentRef>> Handle(GetSfcAttachmentQuery query, CancellationToken cancellationToken)
    {
        var authority = await authorities.GetAsync(cancellationToken);
        if (authority?.FindSfc(query.CertificateId) is not { } certificate)
        {
            return Result.Failure<AttachmentRef>(CarrierAuthorityErrors.SfcNotFound);
        }

        return certificate.Attachment is { } attachment
            ? Result.Success(attachment)
            : Result.Failure<AttachmentRef>(AttachmentErrors.NotAttached);
    }
}

/// <summary>Handles <see cref="SetOperatingRoutesCommand"/> — NL-SP-02 s. 1.</summary>
public sealed class SetOperatingRoutesCommandHandler(ICarrierAuthorityRepository authorities, IComplianceRecordRepository unitOfWork)
    : ICommandHandler<SetOperatingRoutesCommand>
{
    public async Task<Result> Handle(SetOperatingRoutesCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var authority = await CarrierAuthorityLoader.GetOrOpenAsync(authorities, command.TenantId, command.ActorId, now, cancellationToken);
        var result = authority.SetOperatingRoutes(command.Markdown, command.ActorId, now);
        if (result.IsFailure)
        {
            return result;
        }

        await CarrierAuthorityLoader.SaveIfChangedAsync(authority, unitOfWork, cancellationToken);
        return Result.Success();
    }
}
