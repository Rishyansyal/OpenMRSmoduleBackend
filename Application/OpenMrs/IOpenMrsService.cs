namespace Application.OpenMrs;

public interface IOpenMrsService
{
    Task<PatientContact?> GetPatientAsync(string organizationId, string patientId, CancellationToken ct = default);
    Task<IEnumerable<UpcomingAppointment>> GetAppointmentsInRangeAsync(string organizationId, DateTime from, DateTime to, CancellationToken ct = default);
}
