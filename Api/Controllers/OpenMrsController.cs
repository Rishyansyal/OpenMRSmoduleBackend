using Application.OpenMrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OpenMrsController(IOpenMrsService openMrsService) : ControllerBase
{
    [HttpGet("patients")]
    public async Task<IActionResult> SearchPatients([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Zoekterm 'q' is verplicht" });

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
}
