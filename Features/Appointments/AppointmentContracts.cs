using Vyracare.Api.Appointments.Features.Appointments.Shared.Domain;

namespace Vyracare.Api.Appointments.Features.Appointments;

public sealed record CreateAppointmentRequest(
    string PatientId,
    string EmployeeId,
    string ProceedingId,
    DateTime StartsAt,
    DateTime EndsAt,
    AppointmentStatus Status = AppointmentStatus.Scheduled,
    DateTime? FollowUpDueAt = null,
    string? PatientName = null,
    string? PhoneNumber = null,
    string? EmployeeName = null,
    string? ProceedingName = null,
    int? ReminderOffsetValue = null,
    ReminderOffsetUnit? ReminderOffsetUnit = null);

public sealed record UpdateAppointmentStatusRequest(AppointmentStatus Status);

public sealed record AppointmentsTodayMetric(long Total, long ConfirmedLastTwoHours);
public sealed record PendingReturnsMetric(long Total, int WindowDays);
public sealed record WeeklyOccupancyMetric(int Percentage, int BookedMinutes, int AvailableMinutes);
public sealed record DashboardSummaryResponse(
    DateOnly ReferenceDate,
    string TimeZone,
    AppointmentsTodayMetric AppointmentsToday,
    PendingReturnsMetric PendingReturns,
    WeeklyOccupancyMetric WeeklyOccupancy);

public sealed record AppointmentListItemResponse(
    string? Id,
    string PatientId,
    string PatientName,
    string PhoneNumber,
    string EmployeeId,
    string EmployeeName,
    string ProceedingId,
    string ProceedingName,
    DateTime StartsAt,
    DateTime EndsAt,
    AppointmentStatus Status,
    string ScheduleStatus,
    DateTime? ReminderAt,
    DateTime? NotificationSentAt);

public sealed record AppointmentNotificationResponse(
    string AppointmentId,
    string Title,
    string Message,
    DateTime StartsAt,
    DateTime ReminderAt);
