namespace Application.Reminders;

public record ReminderMessageContext(
    string ReminderWindow,
    DateTime EncounterStart,
    string? ServiceType,
    string? Location,
    string? Instructions);
