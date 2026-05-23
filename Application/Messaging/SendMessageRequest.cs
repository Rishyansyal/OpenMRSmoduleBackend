namespace Application.Messaging;

public record SendMessageRequest(
    string[] Recipients,
    string Content,
    string Type,
    string? Subject = null);
