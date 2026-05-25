namespace Application.OpenMrs;

public interface IOpenMrsService
{
    Task<IEnumerable<PatientContact>> SearchPatientsAsync(string query, CancellationToken ct = default);
    Task<PatientContact?> GetPatientAsync(string patientId, CancellationToken ct = default);
    Task<IEnumerable<UpcomingAppointment>> GetUpcomingAppointmentsAsync(CancellationToken ct = default);
    Task<IEnumerable<UpcomingAppointment>> GetEncountersInRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<UpcomingAppointment> CreateVisitAsync(CreateVisitRequest request, CancellationToken ct = default);
    Task<IEnumerable<OpenMrsReferenceItem>> GetVisitTypesAsync(CancellationToken ct = default);
    Task<IEnumerable<OpenMrsReferenceItem>> GetLocationsAsync(CancellationToken ct = default);
}
