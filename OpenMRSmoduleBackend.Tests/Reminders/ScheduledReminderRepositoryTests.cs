using Application.Security;
using Domain;
using Infrastructure.Persistence;
using Infrastructure.Reminders;
using Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenMRSmoduleBackend.Tests.Reminders;

public class ScheduledReminderRepositoryTests
{
    [Fact]
    public async Task RecordDeliveryAttemptAsync_RetryableFailure_SchedulesRetry()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var reminder = await fixture.SeedReminderAsync();

        await fixture.Repository.RecordDeliveryAttemptAsync(
            reminder.Id,
            success: false,
            retryable: true,
            providerMessageId: null,
            errorCode: "SEND_RETRYABLE");

        var updated = await fixture.Db.ScheduledReminders.SingleAsync();
        Assert.Equal(ScheduledReminderStatus.RetryWait, updated.Status);
        Assert.Equal(1, updated.AttemptCount);
        Assert.NotNull(updated.NextAttemptAtUtc);
        Assert.True(updated.NextAttemptAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task RecordDeliveryAttemptAsync_WhenMaxAttemptsReached_DeadLettersReminder()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var reminder = await fixture.SeedReminderAsync(maxAttempts: 1);

        await fixture.Repository.RecordDeliveryAttemptAsync(
            reminder.Id,
            success: false,
            retryable: true,
            providerMessageId: null,
            errorCode: "SEND_RETRYABLE");

        var updated = await fixture.Db.ScheduledReminders.SingleAsync();
        Assert.Equal(ScheduledReminderStatus.DeadLettered, updated.Status);
        Assert.Null(updated.NextAttemptAtUtc);
    }

    [Fact]
    public async Task RecordQueuePublishAsync_StoresQueueEvidence()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var reminder = await fixture.SeedReminderAsync();
        var messageId = Guid.NewGuid();

        await fixture.Repository.RecordQueuePublishAsync(reminder.Id, messageId);

        var updated = await fixture.Db.ScheduledReminders.SingleAsync();
        Assert.Equal(ScheduledReminderStatus.Queued, updated.Status);
        Assert.Equal(messageId.ToString("D"), updated.QueueMessageId);
        Assert.NotNull(updated.QueuedAtUtc);
    }

    [Fact]
    public async Task RecordQueuePublishFailureAsync_MovesQueuedReminderBackToRetryWait()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var reminder = await fixture.SeedReminderAsync();

        await fixture.Repository.RecordQueuePublishAsync(reminder.Id, Guid.NewGuid());
        await fixture.Repository.RecordQueuePublishFailureAsync(reminder.Id, "QUEUE_PUBLISH_FAILED");

        var updated = await fixture.Db.ScheduledReminders.SingleAsync();
        Assert.Equal(ScheduledReminderStatus.RetryWait, updated.Status);
        Assert.Equal("QUEUE_PUBLISH_FAILED", updated.LastErrorCode);
        Assert.Null(updated.QueueMessageId);
        Assert.Null(updated.QueuedAtUtc);
        Assert.NotNull(updated.NextAttemptAtUtc);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsHashedEncounterReference()
    {
        await using var fixture = await DbFixture.CreateAsync();
        await fixture.SeedReminderAsync();

        var overview = Assert.Single(await fixture.Repository.GetRecentAsync());

        Assert.False(string.IsNullOrWhiteSpace(overview.EncounterReferenceHash));
        Assert.NotEqual("enc-1", overview.EncounterReferenceHash);
    }

    private sealed class DbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;
        private readonly IServiceScope _scope;

        public ApplicationDbContext Db { get; }
        public ScheduledReminderRepository Repository { get; }

        public static async Task<DbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:EncryptionKey"] = TestEncryptionKey,
                    ["Encryption:Key"] = TestEncryptionKey
                })
                .Build();

            var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(config)
                .Configure<EncryptionOptions>(o => o.Key = TestEncryptionKey)
                .AddScoped<IEncryptionService, AesEncryptionService>()
                .AddScoped<IFieldEncryptionService, FieldEncryptionService>()
                .AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connection))
                .BuildServiceProvider();

            var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            return new DbFixture(connection, services, scope, db);
        }

        public async Task<ScheduledReminder> SeedReminderAsync(int maxAttempts = 3)
        {
            var fieldEncryption = _scope.ServiceProvider.GetRequiredService<IFieldEncryptionService>();
            var appointment = new AppointmentNotification
            {
                OrganizationId = "org-1",
                EncounterId = "enc-1",
                Status = "planned",
                StartUtc = DateTime.UtcNow.AddHours(2),
                PatientIdEncrypted = fieldEncryption.Encrypt("patient-1"),
                LastEventId = "evt-1"
            };
            var reminder = new ScheduledReminder
            {
                AppointmentNotification = appointment,
                OrganizationId = "org-1",
                EncounterId = "enc-1",
                ReminderWindow = "1h",
                ScheduledForUtc = DateTime.UtcNow.AddMinutes(-1),
                Provider = "swiftsend",
                Status = ScheduledReminderStatus.Sending,
                MaxAttempts = maxAttempts,
                RetryBaseDelaySeconds = 60,
                RetryMaxDelayMinutes = 60
            };
            Db.ScheduledReminders.Add(reminder);
            await Db.SaveChangesAsync();
            return reminder;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            _scope.Dispose();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private DbFixture(
            SqliteConnection connection,
            ServiceProvider services,
            IServiceScope scope,
            ApplicationDbContext db)
        {
            _connection = connection;
            _services = services;
            _scope = scope;
            Db = db;
            Repository = new ScheduledReminderRepository(
                db,
                scope.ServiceProvider.GetRequiredService<IFieldEncryptionService>(),
                scope.ServiceProvider.GetRequiredService<IEncryptionService>());
        }
    }

    private static readonly string TestEncryptionKey = Convert.ToBase64String(
        Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
}

