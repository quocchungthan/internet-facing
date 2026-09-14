using System.Threading;
using System.Threading.Tasks;

namespace Shed.Services
{
    /// <summary>
    /// Abstraction for sending transactional email from the application.
    /// Swap implementations in DI for SMTP, SendGrid, Mailgun, etc.
    /// </summary>
    public interface IEmailSender
    {
        /// <summary>Send a plain-text and/or HTML email.</summary>
        Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }

    /// <summary>Represents a single outbound email.</summary>
    public sealed class EmailMessage
    {
        /// <summary>Recipient address(es). At least one is required.</summary>
        public required string[] To { get; init; }

        /// <summary>Optional CC addresses.</summary>
        public string[] Cc { get; init; } = [];

        /// <summary>Optional BCC addresses.</summary>
        public string[] Bcc { get; init; } = [];

        /// <summary>Email subject line.</summary>
        public required string Subject { get; init; }

        /// <summary>Plain-text body. Used when HTML is not set.</summary>
        public string? TextBody { get; init; }

        /// <summary>HTML body. Takes priority over <see cref="TextBody"/> in capable clients.</summary>
        public string? HtmlBody { get; init; }

        /// <summary>Override the "From" address. Defaults to <see cref="SmtpOptions.DefaultFrom"/>.</summary>
        public string? From { get; init; }

        // â”€â”€ Convenience factories â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public static EmailMessage Plain(string to, string subject, string text) =>
            new() { To = [to], Subject = subject, TextBody = text };

        public static EmailMessage Html(string to, string subject, string html, string? text = null) =>
            new() { To = [to], Subject = subject, HtmlBody = html, TextBody = text };
    }
}


