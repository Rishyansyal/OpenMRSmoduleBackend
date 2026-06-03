namespace Application.Reminders;

public interface IReminderMessageRenderer
{
    string Render(ReminderMessageContext context, string? templateBody);
}
