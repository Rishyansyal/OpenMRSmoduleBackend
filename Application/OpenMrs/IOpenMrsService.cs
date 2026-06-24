namespace Application.OpenMrs;

public interface IOpenMrsService
{
    Task<IEnumerable<PatientContact>> SearchPatientsAsync(string organizationId, string query, CancellationToken ct = default);
    Task<PatientContact?> GetPatientAsync(string organizationId, string patientId, CancellationToken ct = default);
    Task<IEnumerable<UpcomingAppointment>> GetUpcomingAppointmentsAsync(string organizationId, CancellationToken ct = default);
    Task<IEnumerable<UpcomingAppointment>> GetAppointmentsInRangeAsync(string organizationId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<UpcomingAppointment> CreateVisitAsync(string organizationId, CreateVisitRequest request, CancellationToken ct = default);
    Task<IEnumerable<OpenMrsReferenceItem>> GetVisitTypesAsync(string organizationId, CancellationToken ct = default);
    Task<IEnumerable<OpenMrsReferenceItem>> GetLocationsAsync(string organizationId, CancellationToken ct = default);
}
