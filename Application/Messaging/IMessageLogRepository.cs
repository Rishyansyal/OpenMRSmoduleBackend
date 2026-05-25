using Domain;

namespace Application.Messaging;

public interface IMessageLogRepository
{
    Task LogAsync(MessageLog entry, CancellationToken ct = default);

    /// <summary>
    /// Recente berichten voor één gebruiker. Filtert op <see cref="MessageLog.SentByUserId"/>
    /// zodat een zorgmedewerker alleen zijn/haar eigen verzendgeschiedenis ziet (IDOR-bescherming).
    /// </summary>
    Task<IEnumerable<MessageLog>> GetRecentByUserAsync(string userId, int count = 50, CancellationToken ct = default);

    /// <summary>
    /// Controleert of het opgegeven <paramref name="providerMessageId"/> daadwerkelijk
    /// door <paramref name="userId"/> is verzonden. Gebruikt door de status-endpoint om
    /// te voorkomen dat een gebruiker andermans tracking-id kan opvragen.
    /// </summary>
    Task<bool> UserOwnsProviderMessageIdAsync(string userId, string providerMessageId, CancellationToken ct = default);
}
