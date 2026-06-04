namespace Application.Reminders;

public record ReminderLogOverview(
    Guid Id,
    string EncounterReferenceHash,
    string ReminderWindow,
    string Provider,
    bool Success,
    string? ErrorCode,
    DateTime EncounterStart,
    DateTime SentAt);
