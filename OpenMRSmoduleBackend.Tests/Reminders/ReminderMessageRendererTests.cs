using Application.Reminders;
using Infrastructure.Reminders;

namespace OpenMRSmoduleBackend.Tests.Reminders;

public class ReminderMessageRendererTests
{
    [Fact]
    public void Render_UsesDefault24HourTemplate()
    {
        var renderer = new ReminderMessageRenderer();
        var start = new DateTime(2026, 6, 4, 10, 30, 0, DateTimeKind.Local);

        var message = renderer.Render(
            new ReminderMessageContext("24h", start, "Controle", "Polikliniek A", "Neem uw medicatie mee"),
            templateBody: null);

        Assert.Equal(
            "Herinnering: u heeft morgen een Controle op donderdag 4 juni om 10:30 bij Polikliniek A. Belangrijk: Neem uw medicatie mee. Neem contact op bij vragen.",
            message);
    }

    [Fact]
    public void Render_UsesDefaultOneHourTemplate()
    {
        var renderer = new ReminderMessageRenderer();
        var start = new DateTime(2026, 6, 4, 10, 30, 0, DateTimeKind.Local);

        var message = renderer.Render(
            new ReminderMessageContext("1h", start, "Controle", "Polikliniek A", null),
            templateBody: null);

        Assert.Equal(
            "Herinnering: u heeft over ongeveer 1 uur een Controle op donderdag 4 juni om 10:30 bij Polikliniek A.",
            message);
    }
}
