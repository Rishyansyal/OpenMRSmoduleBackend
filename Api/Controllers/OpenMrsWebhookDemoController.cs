using System.Text.Json;
using Application.Auth;
using Application.Organizations;
using Infrastructure.Webhooks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Api.Controllers;

[ApiController]
[Route("api/demo/webhooks/openmrs")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class OpenMrsWebhookDemoController(IOrganizationConfigRepository organizationConfigs) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("signed-appointment")]
    public async Task<IActionResult> CreateSignedAppointment([FromQuery] string? organizationId, CancellationToken ct)
    {
        var organization = string.IsNullOrWhiteSpace(organizationId)
            ? await organizationConfigs.GetDefaultAsync(ct)
            : await organizationConfigs.GetByIdAsync(organizationId, ct);

        if (organization is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "OPENMRS_ORGANIZATION_NOT_CONFIGURED",
                message = "OpenMRS organization is not configured."
            });
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var eventId = $"api-demo-{Guid.NewGuid():N}";
        var encounterId = $"enc-api-demo-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var body = JsonSerializer.Serialize(new
        {
            encounterId,
            patientId = "patient-api-demo",
            start = start.ToString("O"),
            status = "scheduled",
            patientDisplay = "Demo Patient",
            serviceType = "Controle",
            location = "Demo Room",
            instructions = "Demonstratie webhook flow via de API"
        }, JsonOptions);

        var signature = OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, organization.WebhookSecret);

        return Ok(new
        {
            webhookPath = "/api/webhooks/openmrs/appointments",
            eventId,
            encounterId,
            body,
            headers = new Dictionary<string, string>
            {
                ["X-OpenMRS-Event-Id"] = eventId,
                ["X-OpenMRS-Event-Type"] = "encounter.created",
                ["X-OpenMRS-Timestamp"] = timestamp,
                ["X-OpenMRS-Organization-Id"] = organization.OrganizationId,
                ["X-OpenMRS-Signature"] = $"sha256={signature}"
            }
        });
    }
}
