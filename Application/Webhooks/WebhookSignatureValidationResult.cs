namespace Application.Webhooks;

public record WebhookSignatureValidationResult(
    bool IsValid,
    DateTimeOffset? Timestamp,
    string? ErrorCode,
    string? ErrorMessage);
