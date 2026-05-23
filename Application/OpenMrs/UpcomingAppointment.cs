namespace Application.OpenMrs;

public record UpcomingAppointment(
    string Id,
    string Status,
    DateTime Start,
    DateTime? End,
    string PatientId,
    string PatientDisplay,
    string? ServiceType);
