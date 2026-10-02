using Microsoft.Extensions.Options;
using Vyracare.Api.Appointments.Common.Configuration;
using Vyracare.Api.Appointments.Features.Appointments;
using Vyracare.Api.Appointments.Features.Appointments.Shared.Domain;
using Vyracare.Api.Appointments.Features.Appointments.Shared.Ports;
using Xunit;

namespace Vyracare.Api.Appointments.Tests;

public sealed class AppointmentServiceTests
{
    [Fact]
    public async Task DashboardSummaryCombinesRepositoryMetrics()
    {
        var repository = new FakeAppointmentRepository
        {
            ActiveCount = 18,
            ConfirmedCount = 4,
            PendingFollowUpsCount = 6,
            BookedMinutes = 1968
        };
        var service = CreateService(repository);

        var result = await service.GetDashboardSummaryAsync(new DateOnly(2026, 9, 30), CancellationToken.None);

        Assert.Equal(18, result.AppointmentsToday.Total);
        Assert.Equal(4, result.AppointmentsToday.ConfirmedLastTwoHours);
        Assert.Equal(6, result.PendingReturns.Total);
        Assert.Equal(3, result.PendingReturns.WindowDays);
        Assert.Equal(82, result.WeeklyOccupancy.Percentage);
    }

    [Fact]
    public async Task CreateRejectsAnInvalidTimeRange()
    {
        var service = CreateService(new FakeAppointmentRepository());
        var startsAt = new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);
        var request = new CreateAppointmentRequest("patient", "employee", "procedure", startsAt, startsAt);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateCalculatesReminderAndKeepsContactSnapshots()
    {
        var repository = new FakeAppointmentRepository();
        var service = CreateService(repository);
        var startsAt = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
        var request = new CreateAppointmentRequest(
            "patient-id",
            "employee-id",
            "procedure-id",
            startsAt,
            startsAt.AddHours(1),
            PatientName: "Maria Silva",
            PhoneNumber: "11999999999",
            EmployeeName: "Dra. Ana",
            ProceedingName: "Consulta",
            ReminderOffsetValue: 2,
            ReminderOffsetUnit: ReminderOffsetUnit.Days);

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.Equal("Maria Silva", result.PatientName);
        Assert.Equal("11999999999", result.PhoneNumber);
        Assert.Equal(startsAt.AddDays(-2), result.ReminderAt);
    }

    [Fact]
    public async Task DueNotificationCanBeAcknowledged()
    {
        var appointment = new Appointment
        {
            Id = "507f1f77bcf86cd799439011",
            PatientName = "Maria Silva",
            EmployeeName = "Dra. Ana",
            StartsAt = DateTime.UtcNow.AddHours(2),
            EndsAt = DateTime.UtcNow.AddHours(3),
            ReminderAt = DateTime.UtcNow.AddMinutes(-1)
        };
        var repository = new FakeAppointmentRepository { StoredAppointment = appointment };
        var service = CreateService(repository);

        var notifications = await service.ListDueNotificationsAsync(CancellationToken.None);
        var acknowledged = await service.AcknowledgeNotificationAsync(appointment.Id, CancellationToken.None);

        Assert.Single(notifications);
        Assert.NotNull(acknowledged?.NotificationSentAt);
    }

    private static AppointmentService CreateService(IAppointmentRepository repository) =>
        new(repository, Options.Create(new AppointmentOptions
        {
            TimeZone = "America/Sao_Paulo",
            FollowUpWindowDays = 3,
            WeeklyAvailableMinutes = 2400
        }));

    private sealed class FakeAppointmentRepository : IAppointmentRepository
    {
        public long ActiveCount { get; init; }
        public long ConfirmedCount { get; init; }
        public long PendingFollowUpsCount { get; init; }
        public int BookedMinutes { get; init; }
        public Appointment? StoredAppointment { get; init; }

        public Task<Appointment> AddAsync(Appointment appointment, CancellationToken cancellationToken) => Task.FromResult(appointment);
        public Task<IReadOnlyCollection<Appointment>> ListAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Appointment>>(StoredAppointment is null ? [] : [StoredAppointment]);
        public Task<Appointment?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(StoredAppointment);
        public Task ReplaceAsync(Appointment appointment, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Appointment>> ListDueNotificationsAsync(DateTime nowUtc, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Appointment>>(StoredAppointment is null ? [] : [StoredAppointment]);
        public Task<long> CountActiveBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) => Task.FromResult(ActiveCount);
        public Task<long> CountConfirmedBetweenAsync(DateTime appointmentFromUtc, DateTime appointmentToUtc, DateTime confirmedFromUtc, DateTime confirmedToUtc, CancellationToken cancellationToken) => Task.FromResult(ConfirmedCount);
        public Task<long> CountPendingFollowUpsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) => Task.FromResult(PendingFollowUpsCount);
        public Task<int> SumBookedMinutesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) => Task.FromResult(BookedMinutes);
    }
}
