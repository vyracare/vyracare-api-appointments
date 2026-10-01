using Vyracare.Api.Appointments.Features.Appointments.Shared.Domain;

namespace Vyracare.Api.Appointments.Features.Appointments.Shared.Ports;

public interface IAppointmentRepository
{
    Task<Appointment> AddAsync(Appointment appointment, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Appointment>> ListAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task<Appointment?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task ReplaceAsync(Appointment appointment, CancellationToken cancellationToken);
    Task<long> CountActiveBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<long> CountConfirmedBetweenAsync(
        DateTime appointmentFromUtc,
        DateTime appointmentToUtc,
        DateTime confirmedFromUtc,
        DateTime confirmedToUtc,
        CancellationToken cancellationToken);
    Task<long> CountPendingFollowUpsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<int> SumBookedMinutesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}
