using System.ComponentModel.DataAnnotations;

namespace Application.Webhooks;

public record OpenMrsAppointmentWebhookRequest(
    [Required][MaxLength(100)] string EncounterId,
    [Required][MaxLength(100)] string PatientId,
    [Required] DateTime Start,
    [Required][MaxLength(50)] string Status,
    DateTime? End = null,
    [MaxLength(256)] string? PatientDisplay = null,
    [MaxLength(100)] string? ServiceType = null,
    [MaxLength(256)] string? Location = null,
    [MaxLength(2000)] string? Instructions = null);
