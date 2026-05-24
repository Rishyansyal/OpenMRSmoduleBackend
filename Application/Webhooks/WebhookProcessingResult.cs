namespace Application.Webhooks;

public record WebhookProcessingResult(
    string EventId,
    bool Accepted,
    bool Duplicate,
    string Message);
