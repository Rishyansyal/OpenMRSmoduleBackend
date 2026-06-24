using System.Security.Cryptography;
using System.Text;
using Application.OpenMrs;
using Application.Organizations;
using Application.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OpenMrsController(
    IOpenMrsService openMrsService,
    IOpenMrsWebhookService webhookService,
    IOrganizationConfigRepository organizationConfigs) : ControllerBase
{
    [HttpGet("patients")]
    public async Task<IActionResult> SearchPatients(
        [FromQuery] string q,
        [FromQuery] string? organizationId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Zoekterm 'q' is verplicht" });

        if (q.Length > 100)
            return BadRequest(new { error = "Zoekterm 'q' mag maximaal 100 tekens lang zijn." });

        var org = await ResolveOrganizationIdAsync(organizationId, ct);
        if (org is null) return BadRequest(new { error = "OPENMRS_ORGANIZATION_NOT_CONFIGURED" });

        var patients = await openMrsService.SearchPatientsAsync(org, q, ct);
        return Ok(patients);
    }

    [HttpGet("patients/{id}")]
    public async Task<IActionResult> GetPatient(
        string id,
        [FromQuery] string? organizationId,
        CancellationToken ct)
    {
        var org = await ResolveOrganizationIdAsync(organizationId, ct);
        if (org is null) return BadRequest(new { error = "OPENMRS_ORGANIZATION_NOT_CONFIGURED" });

        var patient = await openMrsService.GetPatientAsync(org, id, ct);
        return patient is null ? NotFound() : Ok(patient);
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> GetUpcomingAppointments(
        [FromQuery] string? organizationId,
        CancellationToken ct)
    {
        var org = await ResolveOrganizationIdAsync(organizationId, ct);
        if (org is null) return BadRequest(new { error = "OPENMRS_ORGANIZATION_NOT_CONFIGURED" });

        var appointments = await openMrsService.GetUpcomingAppointmentsAsync(org, ct);
        return Ok(appointments);
    }

    [HttpGet("visit-types")]
    public async Task<IActionResult> GetVisitTypes([FromQuery] string? organizationId, CancellationToken ct)
    {
        var org = await ResolveOrganizationIdAsync(organizationId, ct);
        if (org is null) return BadRequest(new { error = "OPENMRS_ORGANIZATION_NOT_CONFIGURED" });
        return Ok(await openMrsService.GetVisitTypesAsync(org, ct));
    }

    [HttpGet("locations")]
    public async Task<IActionResult> GetLocations([FromQuery] string? organizationId, CancellationToken ct)
    {
        var org = await ResolveOrganizationIdAsync(organizationId, ct);
        if (org is null) return BadRequest(new { error = "OPENMRS_ORGANIZATION_NOT_CONFIGURED" });
        return Ok(await openMrsService.GetLocationsAsync(org, ct));
    }

    [HttpPost("visits")]
    public async Task<IActionResult> CreateVisit(
        [FromBody] CreateVisitRequest request,
        [FromQuery] string? organizationId,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var org = await ResolveOrganizationIdAsync(organizationId, ct);
        if (org is null) return BadRequest(new { error = "OPENMRS_ORGANIZATION_NOT_CONFIGURED" });

        UpcomingAppointment appointment;
        try
        {
            appointment = await openMrsService.CreateVisitAsync(org, request, ct);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "OPENMRS_CREATE_FAILED", message = ex.Message });
        }

        var payload = new OpenMrsAppointmentWebhookRequest(
            EncounterId: appointment.Id,
            PatientId: appointment.PatientId,
            Start: appointment.Start,
            Status: string.IsNullOrWhiteSpace(appointment.Status) ? "planned" : appointment.Status,
            End: appointment.End,
            PatientDisplay: appointment.PatientDisplay,
            ServiceType: appointment.ServiceType,
            Location: appointment.Location,
            Instructions: appointment.Instructions);

        var eventId = BuildEventId(payload);
        var result = await webhookService.ProcessAppointmentAsync(
            eventId,
            eventType: "MANUAL",
            organizationId: org,
            eventTimestamp: DateTimeOffset.UtcNow,
            payload: payload,
            ct);

        var reminderCount = result.Duplicate ? 0 : CountFutureReminders(appointment.Start);

        return Ok(new CreateVisitResult(
            appointment.Id,
            appointment.Start,
            appointment.ServiceType,
            appointment.Location,
            reminderCount));
    }

    private async Task<string?> ResolveOrganizationIdAsync(string? organizationId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(organizationId))
            return await organizationConfigs.GetByIdAsync(organizationId, ct) is null ? null : organizationId;

        return (await organizationConfigs.GetDefaultAsync(ct))?.OrganizationId;
    }

    private static int CountFutureReminders(DateTime startUtc)
    {
        var now = DateTime.UtcNow;
        var count = 0;
        if (startUtc.AddHours(-24) > now) count++;
        if (startUtc.AddHours(-1) > now) count++;
        return count;
    }

    private static string BuildEventId(OpenMrsAppointmentWebhookRequest payload)
    {
        var canonical = string.Join("|",
            payload.EncounterId,
            payload.PatientId,
            payload.Start.ToUniversalTime().ToString("O"),
            payload.Status,
            payload.ServiceType ?? "",
            payload.Location ?? "",
            payload.Instructions ?? "");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
        return $"manual-{payload.EncounterId}-{hash[..16]}";
    }
}
