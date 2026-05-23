namespace Application.OpenMrs;

public record PatientContact(
    string Id,
    string DisplayName,
    string? Phone,
    string? Email);
