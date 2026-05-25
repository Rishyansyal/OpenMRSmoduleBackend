namespace Domain;

public class MessageTemplate
{
    /// <summary>Sleutel: "24h" of "1h"</summary>
    public string Window { get; set; } = string.Empty;

    /// <summary>Berichttekst. Gebruik {type} voor afspraaktype en {tijd} voor datum/tijd.</summary>
    public string Body { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
