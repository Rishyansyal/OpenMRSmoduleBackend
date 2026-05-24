namespace Application.Reminders;

public record ScheduledReminderOverview(
    Guid Id,
    string OrganizationId,
    string EncounterId,
    string ReminderWindow,
    DateTime ScheduledForUtc,
    DateTime EncounterStartUtc,
    string Provider,
    string Status,
    bool AppointmentCancelled,
    string? LastErrorCode);
