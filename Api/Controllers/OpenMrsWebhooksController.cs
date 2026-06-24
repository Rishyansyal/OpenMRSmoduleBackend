using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Application.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/webhooks/openmrs")]
[AllowAnonymous]
public class OpenMrsWebhooksController(
    IOpenMrsWebhookSignatureValidator signatureValidator,
    IOpenMrsWebhookService webhookService) : ControllerBase
{
    private const int MaxWebhookBodyBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [HttpPost("appointments")]
    [Consumes("application/json")]
    public async Task<IActionResult> ReceiveAppointment(CancellationToken ct)
    {
        if (Request.ContentLength > MaxWebhookBodyBytes)
            return BadRequest(new { error = "BODY_TOO_LARGE", message = "Webhook body is too large." });

        var body = await ReadBodyAsync(ct);
        if (body is null)
            return BadRequest(new { error = "BODY_TOO_LARGE", message = "Webhook body is too large." });

        var eventId = Request.Headers["X-OpenMRS-Event-Id"].FirstOrDefault();
        var eventType = Request.Headers["X-OpenMRS-Event-Type"].FirstOrDefault();
        var organizationId = Request.Headers["X-OpenMRS-Organization-Id"].FirstOrDefault();

        var validation = await signatureValidator.ValidateAsync(
            organizationId,
            Request.Headers["X-OpenMRS-Timestamp"].FirstOrDefault(),
            Request.Headers["X-OpenMRS-Signature"].FirstOrDefault(),
            body,
            ct);

        if (!validation.IsValid)
        {
            var status = validation.ErrorCode == "INVALID_SIGNATURE"
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status400BadRequest;
            return StatusCode(status, new { error = validation.ErrorCode, message = validation.ErrorMessage });
        }

        if (string.IsNullOrWhiteSpace(eventId) ||
            string.IsNullOrWhiteSpace(eventType) ||
            string.IsNullOrWhiteSpace(organizationId))
        {
            return BadRequest(new
            {
                error = "MISSING_REQUIRED_HEADERS",
                message = "X-OpenMRS-Event-Id, X-OpenMRS-Event-Type and X-OpenMRS-Organization-Id are required."
            });
        }

        OpenMrsAppointmentWebhookRequest? payload;
        try
        {
            payload = JsonSerializer.Deserialize<OpenMrsAppointmentWebhookRequest>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "INVALID_JSON", message = "Webhook body is not valid JSON." });
        }

        if (payload is null)
            return BadRequest(new { error = "EMPTY_BODY", message = "Webhook body is required." });

        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(
                payload,
                new ValidationContext(payload),
                validationResults,
                validateAllProperties: true))
        {
            return BadRequest(new
            {
                error = "INVALID_PAYLOAD",
                message = "Webhook body failed validation.",
                details = validationResults.Select(result => result.ErrorMessage)
            });
        }

        try
        {
            var result = await webhookService.ProcessAppointmentAsync(
                eventId,
                eventType,
                organizationId,
                validation.Timestamp!.Value,
                payload,
                ct);

            return result.Duplicate
                ? Ok(result)
                : Accepted(string.Empty, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "INVALID_PAYLOAD", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "WEBHOOK_PROCESSING_NOT_CONFIGURED",
                message = ex.Message
            });
        }
    }

    private async Task<string?> ReadBodyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var builder = new StringBuilder();
        var buffer = new char[8192];

        while (true)
        {
            var read = await reader.ReadAsync(buffer, ct);
            if (read == 0)
                break;

            builder.Append(buffer, 0, read);
            if (builder.Length > MaxWebhookBodyBytes)
                return null;
        }

        var body = builder.ToString();
        Request.Body.Position = 0;
        return Encoding.UTF8.GetByteCount(body) <= MaxWebhookBodyBytes ? body : null;
    }
}
