using System.Text.Json;
using Infrastructure.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("api/demo/webhooks/openmrs")]
[Authorize]
public class OpenMrsWebhookDemoController(IOptions<OpenMrsWebhookOptions> options) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("signed-appointment")]
    public IActionResult CreateSignedAppointment()
    {
        var secret = options.Value.Secret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "WEBHOOK_SECRET_NOT_CONFIGURED",
                message = "OpenMRS webhook secret is not configured."
            });
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var eventId = $"frontend-demo-{Guid.NewGuid():N}";
        var encounterId = $"enc-frontend-demo-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var body = JsonSerializer.Serialize(new
        {
            encounterId,
            patientId = "patient-frontend-demo",
            start = start.ToString("O"),
            status = "scheduled",
            patientDisplay = "Demo Patient",
            serviceType = "Controle",
            location = "Demo Room",
            instructions = "Demonstratie webhook flow vanuit de frontend"
        }, JsonOptions);

        var signature = OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, secret);

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
                ["X-OpenMRS-Organization-Id"] = "lu1",
                ["X-OpenMRS-Signature"] = $"sha256={signature}"
            }
        });
    }
}
