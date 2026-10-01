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

        var now = DateTime.UtcNow;
        var appointment = new Appointment
        {
            PatientId = request.PatientId.Trim(),
            EmployeeId = request.EmployeeId.Trim(),
            ProceedingId = request.ProceedingId.Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = request.Status,
            ConfirmedAt = request.Status == AppointmentStatus.Confirmed ? now : null,
            FollowUpDueAt = request.FollowUpDueAt.HasValue ? EnsureUtc(request.FollowUpDueAt.Value) : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        return await _repository.AddAsync(appointment, cancellationToken);
    }

    public Task<IReadOnlyCollection<Appointment>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken) =>
        _repository.ListAsync(fromUtc?.ToUniversalTime(), toUtc?.ToUniversalTime(), cancellationToken);

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
}
