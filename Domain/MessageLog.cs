namespace Domain;

// Sla alleen meta-informatie op — geen PII (geen inhoud, geen adressen).
// Voldoet aan de eis: max 1 jaar bewaren, voldoende voor factuurcontrole.
public class MessageLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Provider { get; init; } = "";
    public string MessageType { get; init; } = "";
    public int RecipientCount { get; init; }
    public int FailedCount { get; init; }
    public string? ProviderMessageId { get; init; }
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public DateTime SentAt { get; init; } = DateTime.UtcNow;
    public string SentByUserId { get; init; } = "";
}
