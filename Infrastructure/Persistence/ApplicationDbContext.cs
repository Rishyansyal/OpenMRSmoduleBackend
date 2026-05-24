using Application.Security;
using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IEncryptionService encryption)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<AppointmentNotification> AppointmentNotifications => Set<AppointmentNotification>();
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();
    public DbSet<OrganizationIntegrationConfig> OrganizationIntegrationConfigs => Set<OrganizationIntegrationConfig>();
    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();
    public DbSet<ScheduledReminder> ScheduledReminders => Set<ScheduledReminder>();
    public DbSet<WebhookEventLog> WebhookEventLogs => Set<WebhookEventLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var encryptConverter = new ValueConverter<string, string>(
            v => encryption.Encrypt(v),
            v => encryption.Decrypt(v));

        builder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Email).HasColumnName("email").IsRequired().HasConversion(encryptConverter);
            e.Property(u => u.EmailHash).HasColumnName("email_hash").IsRequired();
            e.HasIndex(u => u.EmailHash).IsUnique();
            e.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
        });

        builder.Entity<ReminderLog>(e =>
        {
            e.ToTable("reminder_logs");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.EncounterId).HasColumnName("encounter_id").IsRequired().HasConversion(encryptConverter);
            e.Property(r => r.EncounterIdHash).HasColumnName("encounter_id_hash").IsRequired();
            e.Property(r => r.ReminderWindow).HasColumnName("reminder_window").IsRequired();
            e.Property(r => r.Provider).HasColumnName("provider").IsRequired();
            e.Property(r => r.Success).HasColumnName("success");
            e.Property(r => r.ErrorCode).HasColumnName("error_code");
            e.Property(r => r.EncounterStart).HasColumnName("encounter_start");
            e.Property(r => r.SentAt).HasColumnName("sent_at");
            e.HasIndex(r => new { r.EncounterIdHash, r.ReminderWindow });
        });

        builder.Entity<AppointmentNotification>(e =>
        {
            e.ToTable("appointment_notifications");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.OrganizationId).HasColumnName("organization_id").IsRequired();
            e.Property(a => a.EncounterId).HasColumnName("encounter_id").IsRequired();
            e.Property(a => a.Status).HasColumnName("status").IsRequired();
            e.Property(a => a.StartUtc).HasColumnName("start_utc");
            e.Property(a => a.EndUtc).HasColumnName("end_utc");
            e.Property(a => a.IsCancelled).HasColumnName("is_cancelled");
            e.Property(a => a.PatientIdEncrypted).HasColumnName("patient_id_encrypted").IsRequired();
            e.Property(a => a.PatientDisplayEncrypted).HasColumnName("patient_display_encrypted");
            e.Property(a => a.ServiceTypeEncrypted).HasColumnName("service_type_encrypted");
            e.Property(a => a.LocationEncrypted).HasColumnName("location_encrypted");
            e.Property(a => a.InstructionsEncrypted).HasColumnName("instructions_encrypted");
            e.Property(a => a.LastEventId).HasColumnName("last_event_id").IsRequired();
            e.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc");
            e.Property(a => a.UpdatedAtUtc).HasColumnName("updated_at_utc");
            e.HasIndex(a => new { a.OrganizationId, a.EncounterId }).IsUnique();
        });

        builder.Entity<ScheduledReminder>(e =>
        {
            e.ToTable("scheduled_reminders");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.AppointmentNotificationId).HasColumnName("appointment_notification_id");
            e.Property(r => r.OrganizationId).HasColumnName("organization_id").IsRequired();
            e.Property(r => r.EncounterId).HasColumnName("encounter_id").IsRequired();
            e.Property(r => r.ReminderWindow).HasColumnName("reminder_window").IsRequired();
            e.Property(r => r.ScheduledForUtc).HasColumnName("scheduled_for_utc");
            e.Property(r => r.Provider).HasColumnName("provider").IsRequired();
            e.Property(r => r.Status).HasColumnName("status").IsRequired();
            e.Property(r => r.LastErrorCode).HasColumnName("last_error_code");
            e.Property(r => r.SentAtUtc).HasColumnName("sent_at_utc");
            e.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc");
            e.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");
            e.HasOne(r => r.AppointmentNotification)
                .WithMany(a => a.ScheduledReminders)
                .HasForeignKey(r => r.AppointmentNotificationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.Status, r.ScheduledForUtc });
            e.HasIndex(r => new { r.AppointmentNotificationId, r.ReminderWindow });
        });

        builder.Entity<WebhookEventLog>(e =>
        {
            e.ToTable("webhook_event_logs");
            e.HasKey(w => w.Id);
            e.Property(w => w.Id).HasColumnName("id");
            e.Property(w => w.EventId).HasColumnName("event_id").IsRequired();
            e.Property(w => w.EventType).HasColumnName("event_type").IsRequired();
            e.Property(w => w.OrganizationId).HasColumnName("organization_id").IsRequired();
            e.Property(w => w.ResourceType).HasColumnName("resource_type").IsRequired();
            e.Property(w => w.ResourceId).HasColumnName("resource_id").IsRequired();
            e.Property(w => w.PayloadSha256).HasColumnName("payload_sha256").IsRequired();
            e.Property(w => w.Duplicate).HasColumnName("duplicate");
            e.Property(w => w.Processed).HasColumnName("processed");
            e.Property(w => w.ErrorCode).HasColumnName("error_code");
            e.Property(w => w.EventTimestamp).HasColumnName("event_timestamp");
            e.Property(w => w.ReceivedAtUtc).HasColumnName("received_at_utc");
            e.HasIndex(w => w.EventId).IsUnique();
            e.HasIndex(w => w.ReceivedAtUtc);
        });

        builder.Entity<OrganizationIntegrationConfig>(e =>
        {
            e.ToTable("organization_integration_configs");
            e.HasKey(o => o.Id);
            e.Property(o => o.Id).HasColumnName("id");
            e.Property(o => o.OrganizationId).HasColumnName("organization_id").IsRequired();
            e.Property(o => o.DefaultProvider).HasColumnName("default_provider").IsRequired();
            e.Property(o => o.TimeZoneId).HasColumnName("time_zone_id").IsRequired();
            e.Property(o => o.CreatedAtUtc).HasColumnName("created_at_utc");
            e.Property(o => o.UpdatedAtUtc).HasColumnName("updated_at_utc");
            e.HasIndex(o => o.OrganizationId).IsUnique();
        });

        builder.Entity<MessageLog>(e =>
        {
            e.ToTable("message_logs");
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasColumnName("id");
            e.Property(m => m.Provider).HasColumnName("provider").IsRequired();
            e.Property(m => m.MessageType).HasColumnName("message_type").IsRequired();
            e.Property(m => m.RecipientCount).HasColumnName("recipient_count");
            e.Property(m => m.FailedCount).HasColumnName("failed_count");
            e.Property(m => m.ProviderMessageId).HasColumnName("provider_message_id");
            e.Property(m => m.Success).HasColumnName("success");
            e.Property(m => m.ErrorCode).HasColumnName("error_code");
            e.Property(m => m.SentAt).HasColumnName("sent_at");
            e.Property(m => m.SentByUserId).HasColumnName("sent_by_user_id").IsRequired();
            e.HasIndex(m => m.SentAt);
        });
    }
}
