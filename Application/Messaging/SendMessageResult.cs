namespace Application.Messaging;

public record SendMessageResult(
    bool Success,
    string? MessageId,
    string? Error,
    string[] FailedRecipients);
