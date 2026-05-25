using System.ComponentModel.DataAnnotations;

namespace Application.OpenMrs;

public record CreateVisitRequest(
    [Required][MaxLength(100)] string PatientId,
    [Required] DateTime StartUtc,
    [Required][MaxLength(100)] string VisitTypeId,
    DateTime? EndUtc = null,
    [MaxLength(100)] string? LocationId = null,
    [MaxLength(100)] string? ServiceTypeOverride = null,
    [MaxLength(256)] string? LocationOverride = null,
    [MaxLength(2000)] string? Instructions = null);

public record CreateVisitResult(
    string VisitId,
    DateTime StartUtc,
    string? ServiceType,
    string? Location,
    int ScheduledReminderCount);

public record OpenMrsReferenceItem(string Id, string Display);
