namespace Application.Organizations;

public record OrganizationRuntimeConfig(
    string OrganizationId,
    string OpenMrsBaseUrl,
    string OpenMrsUsername,
    string OpenMrsPassword,
    string WebhookSecret,
    string DefaultProvider,
    string TimeZoneId,
    bool Enabled,
    bool PollerEnabled,
    int PollerIntervalMinutes,
    int PollerLookaheadHours,
    int MaxDeliveryAttempts,
    int RetryBaseDelaySeconds,
    int RetryMaxDelayMinutes);

