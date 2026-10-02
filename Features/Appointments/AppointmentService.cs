using Microsoft.Extensions.Options;
using Vyracare.Api.Appointments.Common.Configuration;
using Vyracare.Api.Appointments.Features.Appointments.Shared.Domain;
using Vyracare.Api.Appointments.Features.Appointments.Shared.Ports;

namespace Vyracare.Api.Appointments.Features.Appointments;

public sealed class AppointmentService
{
    private readonly IAppointmentRepository _repository;
    private readonly AppointmentOptions _options;

    public AppointmentService(IAppointmentRepository repository, IOptions<AppointmentOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    public async Task<Appointment> CreateAsync(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId) ||
            string.IsNullOrWhiteSpace(request.EmployeeId) ||
            string.IsNullOrWhiteSpace(request.ProceedingId))
        {
            throw new ArgumentException("Paciente, profissional e procedimento sao obrigatorios.");
        }

        var startsAt = EnsureUtc(request.StartsAt);
        var endsAt = EnsureUtc(request.EndsAt);
        if (endsAt <= startsAt) throw new ArgumentException("O termino deve ser posterior ao inicio.");
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            throw new ArgumentException("O telefone do paciente e obrigatorio.");
        if (request.ReminderOffsetValue is <= 0)
            throw new ArgumentException("A antecedencia da notificacao deve ser maior que zero.");
        if (request.ReminderOffsetValue.HasValue && !request.ReminderOffsetUnit.HasValue)
            throw new ArgumentException("A unidade da notificacao e obrigatoria.");

        var reminderAt = request.ReminderOffsetValue.HasValue
            ? startsAt.Subtract(request.ReminderOffsetUnit == ReminderOffsetUnit.Days
                ? TimeSpan.FromDays(request.ReminderOffsetValue.Value)
                : TimeSpan.FromHours(request.ReminderOffsetValue.Value))
            : (DateTime?)null;

        var now = DateTime.UtcNow;
        var appointment = new Appointment
        {
            PatientId = request.PatientId.Trim(),
            EmployeeId = request.EmployeeId.Trim(),
            ProceedingId = request.ProceedingId.Trim(),
            PatientName = (request.PatientName ?? request.PatientId).Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            EmployeeName = (request.EmployeeName ?? request.EmployeeId).Trim(),
            ProceedingName = (request.ProceedingName ?? request.ProceedingId).Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = request.Status,
            ConfirmedAt = request.Status == AppointmentStatus.Confirmed ? now : null,
            FollowUpDueAt = request.FollowUpDueAt.HasValue ? EnsureUtc(request.FollowUpDueAt.Value) : null,
            ReminderOffsetValue = request.ReminderOffsetValue,
            ReminderOffsetUnit = request.ReminderOffsetUnit,
            ReminderAt = reminderAt,
            CreatedAt = now,
            UpdatedAt = now
        };
        return await _repository.AddAsync(appointment, cancellationToken);
    }

    public async Task<IReadOnlyCollection<AppointmentListItemResponse>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var appointments = await _repository.ListAsync(
            fromUtc?.ToUniversalTime(),
            toUtc?.ToUniversalTime(),
            cancellationToken);
        return appointments.Select(ToListItem).ToArray();
    }

    public Task<Appointment?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public async Task<Appointment?> UpdateStatusAsync(
        string id,
        UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var appointment = await _repository.GetByIdAsync(id, cancellationToken);
        if (appointment is null) return null;

        var now = DateTime.UtcNow;
        appointment.Status = request.Status;
        appointment.UpdatedAt = now;
        if (request.Status == AppointmentStatus.Confirmed && appointment.ConfirmedAt is null)
        {
            appointment.ConfirmedAt = now;
        }

        await _repository.ReplaceAsync(appointment, cancellationToken);
        return appointment;
    }

    public async Task<IReadOnlyCollection<AppointmentNotificationResponse>> ListDueNotificationsAsync(
        CancellationToken cancellationToken)
    {
        var appointments = await _repository.ListDueNotificationsAsync(DateTime.UtcNow, cancellationToken);
        return appointments
            .Where(x => x.Id is not null && x.ReminderAt.HasValue)
            .Select(x => new AppointmentNotificationResponse(
                x.Id!,
                $"Atendimento de {x.PatientName}",
                $"{x.PatientName} sera atendido por {x.EmployeeName} em {FormatLocalDate(x.StartsAt)}.",
                x.StartsAt,
                x.ReminderAt!.Value))
            .ToArray();
    }

    public async Task<Appointment?> AcknowledgeNotificationAsync(string id, CancellationToken cancellationToken)
    {
        var appointment = await _repository.GetByIdAsync(id, cancellationToken);
        if (appointment is null) return null;
        appointment.NotificationSentAt ??= DateTime.UtcNow;
        appointment.UpdatedAt = DateTime.UtcNow;
        await _repository.ReplaceAsync(appointment, cancellationToken);
        return appointment;
    }

    public async Task<DashboardSummaryResponse> GetDashboardSummaryAsync(
        DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var date = referenceDate ?? DateOnly.FromDateTime(localNow);

        var dayStartUtc = ToUtc(date.ToDateTime(TimeOnly.MinValue), timeZone);
        var nextDayUtc = ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);
        var confirmationStartUtc = DateTime.UtcNow.AddHours(-2);
        var followUpEndUtc = ToUtc(date.AddDays(_options.FollowUpWindowDays + 1).ToDateTime(TimeOnly.MinValue), timeZone);

        var daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        var weekStart = date.AddDays(-daysFromMonday);
        var weekStartUtc = ToUtc(weekStart.ToDateTime(TimeOnly.MinValue), timeZone);
        var weekEndUtc = ToUtc(weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue), timeZone);

        var todayTask = _repository.CountActiveBetweenAsync(dayStartUtc, nextDayUtc, cancellationToken);
        var confirmedTask = _repository.CountConfirmedBetweenAsync(
            dayStartUtc,
            nextDayUtc,
            confirmationStartUtc,
            DateTime.UtcNow,
            cancellationToken);
        var returnsTask = _repository.CountPendingFollowUpsAsync(dayStartUtc, followUpEndUtc, cancellationToken);
        var bookedMinutesTask = _repository.SumBookedMinutesAsync(weekStartUtc, weekEndUtc, cancellationToken);
        await Task.WhenAll(todayTask, confirmedTask, returnsTask, bookedMinutesTask);

        var availableMinutes = Math.Max(0, _options.WeeklyAvailableMinutes);
        var percentage = availableMinutes == 0
            ? 0
            : (int)Math.Round(bookedMinutesTask.Result * 100d / availableMinutes, MidpointRounding.AwayFromZero);

        return new DashboardSummaryResponse(
            date,
            _options.TimeZone,
            new AppointmentsTodayMetric(todayTask.Result, confirmedTask.Result),
            new PendingReturnsMetric(returnsTask.Result, _options.FollowUpWindowDays),
            new WeeklyOccupancyMetric(percentage, bookedMinutesTask.Result, availableMinutes));
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static DateTime ToUtc(DateTime localDateTime, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), timeZone);

    private AppointmentListItemResponse ToListItem(Appointment appointment) => new(
        appointment.Id,
        appointment.PatientId,
        appointment.PatientName,
        appointment.PhoneNumber,
        appointment.EmployeeId,
        appointment.EmployeeName,
        appointment.ProceedingId,
        appointment.ProceedingName,
        appointment.StartsAt,
        appointment.EndsAt,
        appointment.Status,
        ResolveScheduleStatus(appointment),
        appointment.ReminderAt,
        appointment.NotificationSentAt);

    private string ResolveScheduleStatus(Appointment appointment)
    {
        if (appointment.Status == AppointmentStatus.Completed) return "Completed";
        if (appointment.Status == AppointmentStatus.Cancelled) return "Cancelled";
        if (appointment.Status == AppointmentStatus.NoShow) return "NoShow";

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var now = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, timeZone);
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(appointment.StartsAt, timeZone);
        if (appointment.EndsAt < now) return "Overdue";
        if (localStart.Date == localNow.Date) return "Today";
        if (appointment.StartsAt <= now.AddHours(24)) return "Approaching";
        return "Scheduled";
    }

    private string FormatLocalDate(DateTime utcDate)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        return TimeZoneInfo.ConvertTimeFromUtc(utcDate, timeZone).ToString("dd/MM/yyyy 'as' HH:mm");
    }
}
