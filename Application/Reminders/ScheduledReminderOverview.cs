namespace Application.Reminders;

public record ScheduledReminderOverview(
    Guid Id,
    string OrganizationId,
    string EncounterReferenceHash,
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
    string? QueueMessageId,
    DateTime? QueuedAtUtc,
    DateTime? ConsumedAtUtc,
    string? ProviderMessageId);
