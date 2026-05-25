namespace Application.Reminders;

public record ScheduledReminderDispatch(
    Guid ScheduledReminderId,
    string OrganizationId,
    string EncounterId,
    string PatientId,
    string ReminderWindow,
    DateTime EncounterStart,
    string? ServiceType,
    string Provider,
    string? Location = null,
    string? Instructions = null);
