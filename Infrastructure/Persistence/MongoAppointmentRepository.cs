using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Vyracare.Api.Appointments.Common.Tenancy;
using Vyracare.Api.Appointments.Features.Appointments.Shared.Domain;
using Vyracare.Api.Appointments.Features.Appointments.Shared.Ports;

namespace Vyracare.Api.Appointments.Infrastructure.Persistence;

public sealed class MongoAppointmentRepository : IAppointmentRepository
{
    private readonly IMongoCollection<AppointmentDocument> _collection;
    private readonly string _tenantId;

    public MongoAppointmentRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _collection = database.GetCollection<AppointmentDocument>("appointments");
        _tenantId = tenantContext.TenantId;
    }

    public async Task<Appointment> AddAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        appointment.TenantId = _tenantId;
        var document = ToDocument(appointment);
        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        appointment.Id = document.Id;
        return appointment;
    }

    public async Task<IReadOnlyCollection<Appointment>> ListAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var filter = Builders<AppointmentDocument>.Filter.Eq(x => x.TenantId, _tenantId);
        if (fromUtc.HasValue) filter &= Builders<AppointmentDocument>.Filter.Gte(x => x.StartsAt, fromUtc.Value);
        if (toUtc.HasValue) filter &= Builders<AppointmentDocument>.Filter.Lt(x => x.StartsAt, toUtc.Value);
        var documents = await _collection.Find(filter).SortBy(x => x.StartsAt).ToListAsync(cancellationToken);
        return documents.Select(ToDomain).ToArray();
    }

    public async Task<Appointment?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out _)) return null;
        var document = await _collection.Find(x => x.TenantId == _tenantId && x.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToDomain(document);
    }

    public Task ReplaceAsync(Appointment appointment, CancellationToken cancellationToken) =>
        _collection.ReplaceOneAsync(x => x.TenantId == _tenantId && x.Id == appointment.Id, ToDocument(appointment), cancellationToken: cancellationToken);

    public async Task<IReadOnlyCollection<Appointment>> ListDueNotificationsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var documents = await _collection.Find(
                x => x.TenantId == _tenantId && x.ReminderAt != null && x.ReminderAt <= nowUtc &&
                     x.NotificationSentAt == null && x.StartsAt >= nowUtc &&
                     x.Status != AppointmentStatus.Cancelled && x.Status != AppointmentStatus.Completed)
            .SortBy(x => x.ReminderAt)
            .ToListAsync(cancellationToken);
        return documents.Select(ToDomain).ToArray();
    }

    public async Task<IReadOnlyCollection<Appointment>> ListUnreadNotificationsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var documents = await _collection.Find(
                x => x.TenantId == _tenantId && x.ReminderAt != null && x.ReminderAt <= nowUtc &&
                     x.NotificationReadAt == null && x.StartsAt >= nowUtc &&
                     x.Status != AppointmentStatus.Cancelled && x.Status != AppointmentStatus.Completed)
            .SortBy(x => x.ReminderAt)
            .ToListAsync(cancellationToken);
        return documents.Select(ToDomain).ToArray();
    }

    public Task<long> CountActiveBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        _collection.CountDocumentsAsync(
            x => x.TenantId == _tenantId && x.StartsAt >= fromUtc && x.StartsAt < toUtc && x.Status != AppointmentStatus.Cancelled,
            cancellationToken: cancellationToken);

    public Task<long> CountConfirmedBetweenAsync(
        DateTime appointmentFromUtc,
        DateTime appointmentToUtc,
        DateTime confirmedFromUtc,
        DateTime confirmedToUtc,
        CancellationToken cancellationToken) =>
        _collection.CountDocumentsAsync(
            x => x.TenantId == _tenantId && x.StartsAt >= appointmentFromUtc && x.StartsAt < appointmentToUtc &&
                 x.Status == AppointmentStatus.Confirmed &&
                 x.ConfirmedAt >= confirmedFromUtc && x.ConfirmedAt < confirmedToUtc,
            cancellationToken: cancellationToken);

    public Task<long> CountPendingFollowUpsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        _collection.CountDocumentsAsync(
            x => x.TenantId == _tenantId && x.FollowUpDueAt >= fromUtc && x.FollowUpDueAt < toUtc && x.FollowUpScheduledAt == null,
            cancellationToken: cancellationToken);

    public async Task<int> SumBookedMinutesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var appointments = await _collection.Find(
            x => x.TenantId == _tenantId && x.StartsAt >= fromUtc && x.StartsAt < toUtc && x.Status != AppointmentStatus.Cancelled)
            .Project(x => new { x.StartsAt, x.EndsAt })
            .ToListAsync(cancellationToken);
        return appointments.Sum(x => Math.Max(0, (int)(x.EndsAt - x.StartsAt).TotalMinutes));
    }

    private static AppointmentDocument ToDocument(Appointment value) => new()
    {
        Id = value.Id,
        TenantId = value.TenantId,
        PatientId = value.PatientId,
        EmployeeId = value.EmployeeId,
        ProceedingId = value.ProceedingId,
        PatientName = value.PatientName,
        PhoneNumber = value.PhoneNumber,
        EmployeeName = value.EmployeeName,
        ProceedingName = value.ProceedingName,
        StartsAt = value.StartsAt,
        EndsAt = value.EndsAt,
        Status = value.Status,
        ConfirmedAt = value.ConfirmedAt,
        FollowUpDueAt = value.FollowUpDueAt,
        FollowUpScheduledAt = value.FollowUpScheduledAt,
        ReminderOffsetValue = value.ReminderOffsetValue,
        ReminderOffsetUnit = value.ReminderOffsetUnit,
        ReminderAt = value.ReminderAt,
        NotificationSentAt = value.NotificationSentAt,
        NotificationReadAt = value.NotificationReadAt,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };

    private static Appointment ToDomain(AppointmentDocument value) => new()
    {
        Id = value.Id,
        TenantId = value.TenantId,
        PatientId = value.PatientId,
        EmployeeId = value.EmployeeId,
        ProceedingId = value.ProceedingId,
        PatientName = value.PatientName,
        PhoneNumber = value.PhoneNumber,
        EmployeeName = value.EmployeeName,
        ProceedingName = value.ProceedingName,
        StartsAt = value.StartsAt,
        EndsAt = value.EndsAt,
        Status = value.Status,
        ConfirmedAt = value.ConfirmedAt,
        FollowUpDueAt = value.FollowUpDueAt,
        FollowUpScheduledAt = value.FollowUpScheduledAt,
        ReminderOffsetValue = value.ReminderOffsetValue,
        ReminderOffsetUnit = value.ReminderOffsetUnit,
        ReminderAt = value.ReminderAt,
        NotificationSentAt = value.NotificationSentAt,
        NotificationReadAt = value.NotificationReadAt,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };

    private sealed class AppointmentDocument
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string? Id { get; set; }
        [BsonElement("tenantId")] public string TenantId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string ProceedingId { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string ProceedingName { get; set; } = string.Empty;
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        [BsonRepresentation(BsonType.String)] public AppointmentStatus Status { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? FollowUpDueAt { get; set; }
        public DateTime? FollowUpScheduledAt { get; set; }
        public int? ReminderOffsetValue { get; set; }
        [BsonRepresentation(BsonType.String)] public ReminderOffsetUnit? ReminderOffsetUnit { get; set; }
        public DateTime? ReminderAt { get; set; }
        public DateTime? NotificationSentAt { get; set; }
        public DateTime? NotificationReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
