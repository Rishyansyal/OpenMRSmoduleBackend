namespace Application.Messaging;

public record MessageStatusResult(
    string TrackingId,
    string Status,
    DateTime? SubmittedAt,
    DateTime? ProcessedAt,
    string? ErrorDetails);
