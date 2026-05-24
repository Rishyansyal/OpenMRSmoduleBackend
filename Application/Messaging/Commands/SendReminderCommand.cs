namespace Application.Messaging.Commands;

public record SendReminderCommand(
    Guid ScheduledReminderId,
    string OrganizationId,
    string EncounterId,
    string PatientId,
    string ReminderWindow,
    DateTime EncounterStart,
    string? ServiceType,
    string Provider);
