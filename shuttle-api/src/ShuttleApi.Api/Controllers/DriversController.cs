using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Drivers.Commands.Documents;
using ShuttleApi.Application.Drivers.Commands.Drivers;
using ShuttleApi.Application.Drivers.Commands.Roster;
using ShuttleApi.Application.Drivers.Queries.Documents;
using ShuttleApi.Application.Drivers.Queries.Drivers;
using ShuttleApi.Application.Drivers.Queries.Roster;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Api.Controllers;

[Authorize]
public sealed class DriversController(ISender sender) : BaseApiController(sender)
{
    // ── Drivers CRUD ─────────────────────────────────────────────────────────

    [HttpGet]
    [Route("api/drivers")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetDriversQuery(), cancellationToken));

    [HttpGet]
    [Route("api/drivers/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetDriverByIdQuery(id), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/drivers")]
    public async Task<IActionResult> Create([FromBody] CreateDriverCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/drivers/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDriverRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateDriverCommand(
            id,
            request.EmployeeId,
            request.FirstName,
            request.LastName,
            request.Phone,
            request.Email,
            request.HireDate,
            request.IsActive), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete]
    [Route("api/drivers/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteDriverCommand(id), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch]
    [Route("api/drivers/{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetDriverStatusRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new SetDriverStatusCommand(id, request.Status), cancellationToken);
        return NoContent();
    }

    // ── Documents ────────────────────────────────────────────────────────────

    [HttpGet]
    [Route("api/drivers/{id:guid}/documents")]
    public async Task<IActionResult> GetDocuments(Guid id, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetDriverDocumentsQuery(id), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/drivers/{id:guid}/documents")]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> UploadDocument(
        Guid id,
        IFormFile file,
        [FromForm] DocumentType documentType,
        [FromForm] DateTime? expiryDate,
        [FromForm] DateTime? testDate,
        [FromForm] TestResult? testResultValue,
        [FromForm] string? testedBy,
        [FromForm] string? licenseNumber,
        [FromForm] LicenseClass? licenseClass,
        [FromForm] DateTime? issuedDate,
        [FromForm] string? licenseProvince,
        [FromForm] CheckResult? checkResultValue,
        [FromForm] string? issuingAuthority,
        [FromForm] int? violationCount,
        [FromForm] int? atFaultAccidentCount,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);
        var fileData = ms.ToArray();

        var result = await Sender.Send(new UploadDriverDocumentCommand(
            id,
            documentType,
            file.FileName,
            file.ContentType,
            fileData,
            expiryDate,
            testDate,
            testResultValue,
            testedBy,
            licenseNumber,
            licenseClass,
            issuedDate,
            licenseProvince,
            checkResultValue,
            issuingAuthority,
            violationCount,
            atFaultAccidentCount,
            notes), cancellationToken);

        return Ok(result);
    }

    [HttpGet]
    [Route("api/drivers/{driverId:guid}/documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(
        Guid driverId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetDriverDocumentFileQuery(driverId, documentId), cancellationToken);
        return File(result.Data, result.ContentType, result.FileName);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete]
    [Route("api/drivers/{driverId:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(
        Guid driverId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteDriverDocumentCommand(driverId, documentId), cancellationToken);
        return NoContent();
    }

    // ── Roster ───────────────────────────────────────────────────────────────

    [HttpGet]
    [Route("api/drivers/roster")]
    public async Task<IActionResult> GetFleetRoster(
        [FromQuery] DateOnly rangeStart,
        [FromQuery] DateOnly rangeEnd,
        CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetFleetRosterQuery(rangeStart, rangeEnd), cancellationToken));

    [HttpGet]
    [Route("api/drivers/{id:guid}/roster")]
    public async Task<IActionResult> GetDriverRoster(
        Guid id,
        [FromQuery] DateOnly rangeStart,
        [FromQuery] DateOnly rangeEnd,
        CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetDriverRosterQuery(id, rangeStart, rangeEnd), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/drivers/{id:guid}/roster")]
    public async Task<IActionResult> UpsertRosterEntry(
        Guid id,
        [FromBody] UpsertRosterEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new UpsertRosterEntryCommand(
            id,
            request.EntryDate,
            request.Status,
            request.ShiftStart,
            request.ShiftEnd), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete]
    [Route("api/drivers/{id:guid}/roster/{entryId:guid}")]
    public async Task<IActionResult> DeleteRosterEntry(
        Guid id,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteRosterEntryCommand(id, entryId), cancellationToken);
        return NoContent();
    }
}

// ── Inline request records ────────────────────────────────────────────────────

public sealed record UpdateDriverRequest(
    string EmployeeId,
    string FirstName,
    string LastName,
    string Phone,
    string Email,
    DateTime HireDate,
    bool IsActive);

public sealed record SetDriverStatusRequest(DriverStatus Status);

public sealed record UpsertRosterEntryRequest(
    DateOnly EntryDate,
    RosterStatus Status,
    TimeOnly? ShiftStart,
    TimeOnly? ShiftEnd);
