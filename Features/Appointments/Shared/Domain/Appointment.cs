namespace Vyracare.Api.Appointments.Features.Appointments.Shared.Domain;

public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    Completed,
    Cancelled,
    NoShow
}

public sealed class Appointment
{
    public string? Id { get; set; }
    public string PatientId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string ProceedingId { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public AppointmentStatus Status { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? FollowUpDueAt { get; set; }
    public DateTime? FollowUpScheduledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
