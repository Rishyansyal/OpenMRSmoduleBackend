using Application.Reminders;

namespace Infrastructure.Reminders;

public class ReminderMessageRenderer : IReminderMessageRenderer
{
    public string Render(ReminderMessageContext context, string? templateBody)
    {
        var timeStr = context.EncounterStart.ToLocalTime().ToString(
            "dddd d MMMM 'om' HH:mm",
            new System.Globalization.CultureInfo("nl-NL"));
        var serviceType = context.ServiceType ?? "afspraak";
        var location = string.IsNullOrWhiteSpace(context.Location) ? "locatie onbekend" : context.Location!;
        var instructions = string.IsNullOrWhiteSpace(context.Instructions) ? "" : context.Instructions!;

        var body = templateBody ?? (context.ReminderWindow == "24h"
            ? "Herinnering: u heeft morgen een {type} op {tijd} bij {locatie}.{instructies} Neem contact op bij vragen."
            : "Herinnering: u heeft over ongeveer 1 uur een {type} op {tijd} bij {locatie}.{instructies}");

        var instructionsBlock = string.IsNullOrEmpty(instructions) ? "" : $" Belangrijk: {instructions}.";

        return body
            .Replace("{type}", serviceType)
            .Replace("{tijd}", timeStr)
            .Replace("{locatie}", location)
            .Replace("{instructies}", instructionsBlock);
    }
}
