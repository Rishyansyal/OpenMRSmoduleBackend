using System.Diagnostics.Metrics;

namespace Infrastructure.Observability;

public sealed class MessagingMetrics : IDisposable
{
    public const string MeterName = "OpenMRS.Messaging";

    private readonly Meter _meter;
    private readonly Counter<long> _messagesSent;
    private readonly Counter<long> _remindersSent;
    private readonly Counter<long> _dataRetentionDeleted;
    private readonly Histogram<double> _messageDuration;

    public MessagingMetrics()
    {
        _meter = new Meter(MeterName, "1.0");

        _messagesSent = _meter.CreateCounter<long>(
            "messaging.messages_sent",
            description: "Aantal verstuurde berichten per provider en type");

        _remindersSent = _meter.CreateCounter<long>(
            "messaging.reminders_sent",
            description: "Aantal verstuurde afspraakherinneringen per venster");

        _dataRetentionDeleted = _meter.CreateCounter<long>(
            "data_retention.records_deleted",
            description: "Aantal verwijderde records per data-retentie run");

        _messageDuration = _meter.CreateHistogram<double>(
            "messaging.send_duration_ms",
            unit: "ms",
            description: "Verzendtijd per provider");
    }

    public void RecordMessageSent(string provider, string type, bool success) =>
        _messagesSent.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("type", type),
            new KeyValuePair<string, object?>("success", success));

    public void RecordReminderSent(string window, bool success) =>
        _remindersSent.Add(1,
            new KeyValuePair<string, object?>("window", window),
            new KeyValuePair<string, object?>("success", success));

    public void RecordDataRetentionRun(int reminderDeleted, int messageDeleted)
    {
        _dataRetentionDeleted.Add(reminderDeleted,
            new KeyValuePair<string, object?>("table", "reminder_logs"));
        _dataRetentionDeleted.Add(messageDeleted,
            new KeyValuePair<string, object?>("table", "message_logs"));
    }

    public void RecordSendDuration(string provider, double milliseconds) =>
        _messageDuration.Record(milliseconds,
            new KeyValuePair<string, object?>("provider", provider));

    public void Dispose() => _meter.Dispose();
}
