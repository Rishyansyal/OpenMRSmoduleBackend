namespace Application.Messaging.Commands;

public record SendReminderCommand(
    string EncounterId,
    string PatientId,
    string ReminderWindow,
    DateTime EncounterStart,
    string? ServiceType,
    string Provider);
