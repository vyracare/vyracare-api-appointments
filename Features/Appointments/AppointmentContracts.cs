using Vyracare.Api.Appointments.Features.Appointments.Shared.Domain;

namespace Vyracare.Api.Appointments.Features.Appointments;

public sealed record CreateAppointmentRequest(
    string PatientId,
    string EmployeeId,
    string ProceedingId,
    DateTime StartsAt,
    DateTime EndsAt,
    AppointmentStatus Status = AppointmentStatus.Scheduled,
    DateTime? FollowUpDueAt = null);

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
