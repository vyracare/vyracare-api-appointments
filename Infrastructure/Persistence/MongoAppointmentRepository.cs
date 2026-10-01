using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Vyracare.Api.Appointments.Features.Appointments.Shared.Domain;
using Vyracare.Api.Appointments.Features.Appointments.Shared.Ports;

namespace Vyracare.Api.Appointments.Infrastructure.Persistence;

public sealed class MongoAppointmentRepository : IAppointmentRepository
{
    private readonly IMongoCollection<AppointmentDocument> _collection;

    public MongoAppointmentRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AppointmentDocument>("appointments");
    }

    public async Task<Appointment> AddAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        var document = ToDocument(appointment);
        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        appointment.Id = document.Id;
        return appointment;
    }

    public async Task<IReadOnlyCollection<Appointment>> ListAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var filter = Builders<AppointmentDocument>.Filter.Empty;
        if (fromUtc.HasValue) filter &= Builders<AppointmentDocument>.Filter.Gte(x => x.StartsAt, fromUtc.Value);
        if (toUtc.HasValue) filter &= Builders<AppointmentDocument>.Filter.Lt(x => x.StartsAt, toUtc.Value);
        var documents = await _collection.Find(filter).SortBy(x => x.StartsAt).ToListAsync(cancellationToken);
        return documents.Select(ToDomain).ToArray();
    }

    public async Task<Appointment?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out _)) return null;
        var document = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToDomain(document);
    }

    public Task ReplaceAsync(Appointment appointment, CancellationToken cancellationToken) =>
        _collection.ReplaceOneAsync(x => x.Id == appointment.Id, ToDocument(appointment), cancellationToken: cancellationToken);

    public Task<long> CountActiveBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        _collection.CountDocumentsAsync(
            x => x.StartsAt >= fromUtc && x.StartsAt < toUtc && x.Status != AppointmentStatus.Cancelled,
            cancellationToken: cancellationToken);

    public Task<long> CountConfirmedBetweenAsync(
        DateTime appointmentFromUtc,
        DateTime appointmentToUtc,
        DateTime confirmedFromUtc,
        DateTime confirmedToUtc,
        CancellationToken cancellationToken) =>
        _collection.CountDocumentsAsync(
            x => x.StartsAt >= appointmentFromUtc && x.StartsAt < appointmentToUtc &&
                 x.Status == AppointmentStatus.Confirmed &&
                 x.ConfirmedAt >= confirmedFromUtc && x.ConfirmedAt < confirmedToUtc,
            cancellationToken: cancellationToken);

    public Task<long> CountPendingFollowUpsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        _collection.CountDocumentsAsync(
            x => x.FollowUpDueAt >= fromUtc && x.FollowUpDueAt < toUtc && x.FollowUpScheduledAt == null,
            cancellationToken: cancellationToken);

    public async Task<int> SumBookedMinutesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var appointments = await _collection.Find(
            x => x.StartsAt >= fromUtc && x.StartsAt < toUtc && x.Status != AppointmentStatus.Cancelled)
            .Project(x => new { x.StartsAt, x.EndsAt })
            .ToListAsync(cancellationToken);
        return appointments.Sum(x => Math.Max(0, (int)(x.EndsAt - x.StartsAt).TotalMinutes));
    }

    private static AppointmentDocument ToDocument(Appointment value) => new()
    {
        Id = value.Id,
        PatientId = value.PatientId,
        EmployeeId = value.EmployeeId,
        ProceedingId = value.ProceedingId,
        StartsAt = value.StartsAt,
        EndsAt = value.EndsAt,
        Status = value.Status,
        ConfirmedAt = value.ConfirmedAt,
        FollowUpDueAt = value.FollowUpDueAt,
        FollowUpScheduledAt = value.FollowUpScheduledAt,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };

    private static Appointment ToDomain(AppointmentDocument value) => new()
    {
        Id = value.Id,
        PatientId = value.PatientId,
        EmployeeId = value.EmployeeId,
        ProceedingId = value.ProceedingId,
        StartsAt = value.StartsAt,
        EndsAt = value.EndsAt,
        Status = value.Status,
        ConfirmedAt = value.ConfirmedAt,
        FollowUpDueAt = value.FollowUpDueAt,
        FollowUpScheduledAt = value.FollowUpScheduledAt,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };

    private sealed class AppointmentDocument
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string? Id { get; set; }
        public string PatientId { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string ProceedingId { get; set; } = string.Empty;
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        [BsonRepresentation(BsonType.String)] public AppointmentStatus Status { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? FollowUpDueAt { get; set; }
        public DateTime? FollowUpScheduledAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
