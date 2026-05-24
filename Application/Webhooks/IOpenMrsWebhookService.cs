namespace Application.Webhooks;

public interface IOpenMrsWebhookService
{
    Task<WebhookProcessingResult> ProcessAppointmentAsync(
        string eventId,
        string eventType,
        string organizationId,
        DateTimeOffset eventTimestamp,
        OpenMrsAppointmentWebhookRequest payload,
        CancellationToken ct = default);
}
