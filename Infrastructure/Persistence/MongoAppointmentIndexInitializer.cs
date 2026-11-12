using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Vyracare.Api.Appointments.Infrastructure.Persistence;

/** Creates the tenant-first indexes used by agenda and notification queries. */
public sealed class MongoAppointmentIndexInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MongoAppointmentIndexInitializer> _logger;

    public MongoAppointmentIndexInitializer(
        IServiceScopeFactory scopeFactory,
        ILogger<MongoAppointmentIndexInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var appointments = database.GetCollection<BsonDocument>("appointments");

        try
        {
            await appointments.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys
                    .Ascending("tenantId").Ascending("StartsAt")),
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys
                    .Ascending("tenantId").Ascending("Status").Ascending("ReminderAt").Ascending("NotificationReadAt"))
            ], cancellationToken);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            _logger.LogWarning(exception, "Nao foi possivel reconciliar os indices de agenda durante a inicializacao.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
