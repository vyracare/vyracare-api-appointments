namespace Vyracare.Api.Appointments.Features.Appointments.Shared.Domain;

public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    Completed,
    Cancelled,
    NoShow
}

public enum ReminderOffsetUnit
{
    Hours,
    Days
}

public sealed class Appointment
{
    public string? Id { get; set; }
    public string PatientId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string ProceedingId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string ProceedingName { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public AppointmentStatus Status { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? FollowUpDueAt { get; set; }
    public DateTime? FollowUpScheduledAt { get; set; }
    public int? ReminderOffsetValue { get; set; }
    public ReminderOffsetUnit? ReminderOffsetUnit { get; set; }
    public DateTime? ReminderAt { get; set; }
    public DateTime? NotificationSentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
