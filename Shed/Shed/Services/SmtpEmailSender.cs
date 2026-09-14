using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace Shed.Services
{
    /// <summary>
    /// Sends email via SMTP using the built-in <see cref="SmtpClient"/>.
    /// Configure via <see cref="SmtpOptions"/> (appsettings section "Smtp").
    /// </summary>
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (message.To is not { Length: > 0 })
                throw new ArgumentException("At least one recipient is required.", nameof(message));

            var from = message.From ?? _options.DefaultFrom;
            if (string.IsNullOrWhiteSpace(from))
                throw new InvalidOperationException(
                    "No From address. Set EmailMessage.From or SmtpOptions.DefaultFrom.");

            // â”€â”€ Dry-run: log instead of send â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (_options.DryRun)
            {
                _logger.LogInformation(
                    "[SmtpEmailSender DryRun] To={To} Subject={Subject}\n{Body}",
                    string.Join(", ", message.To),
                    message.Subject,
                    message.HtmlBody ?? message.TextBody ?? "(empty)");
                return;
            }

            // â”€â”€ Build MailMessage â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            using var mail = new MailMessage();
            mail.From = new MailAddress(from);
            mail.Subject = message.Subject;

            foreach (var to in message.To)
                mail.To.Add(to);

            foreach (var cc in message.Cc ?? [])
                mail.CC.Add(cc);

            foreach (var bcc in message.Bcc ?? [])
                mail.Bcc.Add(bcc);

            if (message.HtmlBody is { Length: > 0 })
            {
                mail.IsBodyHtml = true;
                mail.Body = message.HtmlBody;

                // Attach plain-text as alternate view for non-HTML clients
                if (message.TextBody is { Length: > 0 })
                {
                    var altView = AlternateView.CreateAlternateViewFromString(
                        message.TextBody, null, "text/plain");
                    mail.AlternateViews.Add(altView);
                }
            }
            else
            {
                mail.IsBodyHtml = false;
                mail.Body = message.TextBody ?? string.Empty;
            }

            // â”€â”€ Send â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            using var client = BuildClient();
            await client.SendMailAsync(mail, cancellationToken);

            _logger.LogInformation(
                "Email sent to {To} | Subject: {Subject}",
                string.Join(", ", message.To),
                message.Subject);
        }

        private SmtpClient BuildClient()
        {
            var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
            };

            if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
            }

            return client;
        }
    }
}


