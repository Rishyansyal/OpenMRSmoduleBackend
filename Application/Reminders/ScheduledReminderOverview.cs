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
    string? LastErrorCode,
    int AttemptCount,
    int MaxAttempts,
    DateTime? LastAttemptAtUtc,
    DateTime? NextAttemptAtUtc,
    string? ProviderMessageId);
