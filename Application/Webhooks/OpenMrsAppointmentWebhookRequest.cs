namespace Application.Webhooks;

public record OpenMrsAppointmentWebhookRequest(
    string EncounterId,
    string PatientId,
    DateTime Start,
    string Status,
    DateTime? End = null,
    string? PatientDisplay = null,
    string? ServiceType = null,
    string? Location = null,
    string? Instructions = null);
