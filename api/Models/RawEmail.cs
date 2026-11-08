namespace EmailTriage.Api.Models;

/// Raw, immutable copy of the original inbound email.
/// Never mutated after insert — always the "view original" source of truth.
public class RawEmail
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string GraphMessageId { get; set; } = string.Empty;
    public string? InternetMessageId { get; set; }

    public string From { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;

    public string BodyText { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }

    public DateTime ReceivedAt { get; set; }
    public string? RawJson { get; set; } // full Graph payload, for forensic/debug use

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket? Ticket { get; set; }
}
