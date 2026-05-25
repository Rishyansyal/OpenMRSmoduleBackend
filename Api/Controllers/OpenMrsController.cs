using System.Security.Cryptography;
using System.Text;
using Application.OpenMrs;
using Application.Webhooks;
using Infrastructure.OpenMrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OpenMrsController(
    IOpenMrsService openMrsService,
    IOpenMrsWebhookService webhookService,
    IOptions<OpenMrsPollerOptions> pollerOptions) : ControllerBase
{
    [HttpGet("patients")]
    public async Task<IActionResult> SearchPatients([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Zoekterm 'q' is verplicht" });

        if (q.Length > 100)
            return BadRequest(new { error = "Zoekterm 'q' mag maximaal 100 tekens lang zijn." });

        var patients = await openMrsService.SearchPatientsAsync(q, ct);
        return Ok(patients);
    }

    [HttpGet("patients/{id}")]
    public async Task<IActionResult> GetPatient(string id, CancellationToken ct)
    {
        var patient = await openMrsService.GetPatientAsync(id, ct);
        return patient is null ? NotFound() : Ok(patient);
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> GetUpcomingAppointments(CancellationToken ct)
    {
        var appointments = await openMrsService.GetUpcomingAppointmentsAsync(ct);
        return Ok(appointments);
    }

    [HttpGet("visit-types")]
    public async Task<IActionResult> GetVisitTypes(CancellationToken ct) =>
        Ok(await openMrsService.GetVisitTypesAsync(ct));

    [HttpGet("locations")]
    public async Task<IActionResult> GetLocations(CancellationToken ct) =>
        Ok(await openMrsService.GetLocationsAsync(ct));

    [HttpPost("visits")]
    public async Task<IActionResult> CreateVisit([FromBody] CreateVisitRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        UpcomingAppointment appointment;
        try
        {
            appointment = await openMrsService.CreateVisitAsync(request, ct);
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
            organizationId: pollerOptions.Value.OrganizationId,
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
