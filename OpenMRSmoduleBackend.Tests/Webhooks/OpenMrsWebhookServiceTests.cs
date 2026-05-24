using Application.Webhooks;
using Application.Security;
using Infrastructure.Persistence;
using Infrastructure.Reminders;
using Infrastructure.Security;
using Infrastructure.Webhooks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenMRSmoduleBackend.Tests.Webhooks;

public class OpenMrsWebhookServiceTests
{
    [Fact]
    public async Task ProcessAppointmentAsync_NewFutureAppointment_SchedulesBothReminderWindows()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var service = CreateService(fixture.Db);
        var start = DateTime.UtcNow.AddDays(3);

        var result = await service.ProcessAppointmentAsync(
            "evt-1",
            "CREATED",
            "org-1",
            DateTimeOffset.UtcNow,
            new OpenMrsAppointmentWebhookRequest("enc-1", "patient-1", start, "planned", ServiceType: "Controle"));

        Assert.True(result.Accepted);
        Assert.False(result.Duplicate);
        Assert.Equal(1, await fixture.Db.AppointmentNotifications.CountAsync());
        Assert.Equal(2, await fixture.Db.ScheduledReminders.CountAsync());
        Assert.Contains(await fixture.Db.ScheduledReminders.ToListAsync(), r => r.ReminderWindow == "24h");
        Assert.Contains(await fixture.Db.ScheduledReminders.ToListAsync(), r => r.ReminderWindow == "1h");
    }

    [Fact]
    public async Task ProcessAppointmentAsync_DuplicateEvent_DoesNotScheduleAgain()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var service = CreateService(fixture.Db);
        var payload = new OpenMrsAppointmentWebhookRequest(
            "enc-1",
            "patient-1",
            DateTime.UtcNow.AddDays(3),
            "planned");

        await service.ProcessAppointmentAsync("evt-1", "CREATED", "org-1", DateTimeOffset.UtcNow, payload);
        var duplicate = await service.ProcessAppointmentAsync("evt-1", "CREATED", "org-1", DateTimeOffset.UtcNow, payload);

        Assert.True(duplicate.Duplicate);
        Assert.Equal(2, await fixture.Db.ScheduledReminders.CountAsync());
        Assert.Equal(1, await fixture.Db.WebhookEventLogs.CountAsync());
    }

    [Fact]
    public async Task ProcessAppointmentAsync_CancelledAppointment_CancelsPendingReminders()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var service = CreateService(fixture.Db);
        var start = DateTime.UtcNow.AddDays(3);

        await service.ProcessAppointmentAsync(
            "evt-1",
            "CREATED",
            "org-1",
            DateTimeOffset.UtcNow,
            new OpenMrsAppointmentWebhookRequest("enc-1", "patient-1", start, "planned"));

        await service.ProcessAppointmentAsync(
            "evt-2",
            "VOIDED",
            "org-1",
            DateTimeOffset.UtcNow,
            new OpenMrsAppointmentWebhookRequest("enc-1", "patient-1", start, "cancelled"));

        var reminders = await fixture.Db.ScheduledReminders.ToListAsync();
        Assert.All(reminders, r => Assert.Equal("cancelled", r.Status));
    }

    private static OpenMrsWebhookService CreateService(ApplicationDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:EncryptionKey"] = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray())
            })
            .Build();

        return new OpenMrsWebhookService(
            db,
            new FieldEncryptionService(config),
            Options.Create(new ReminderOptions { DefaultProvider = "swiftsend" }));
    }

    private sealed class DbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Db { get; }

        public static async Task<DbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection()
                .AddSingleton(CreateEncryptionConfiguration())
                .AddScoped<IFieldEncryptionService, FieldEncryptionService>()
                .AddDbContext<ApplicationDbContext>(dbOptions => dbOptions.UseSqlite(connection))
                .BuildServiceProvider();

            var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            return new DbFixture(connection, db, scope, services);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            _scope.Dispose();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private readonly IServiceScope _scope;
        private readonly ServiceProvider _services;

        private DbFixture(
            SqliteConnection connection,
            ApplicationDbContext db,
            IServiceScope scope,
            ServiceProvider services)
        {
            _connection = connection;
            Db = db;
            _scope = scope;
            _services = services;
        }
    }

    private static IConfiguration CreateEncryptionConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:EncryptionKey"] = Convert.ToBase64String(
                    Enumerable.Range(0, 32).Select(i => (byte)i).ToArray())
            })
            .Build();
}
