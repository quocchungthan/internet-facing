namespace Shed.Services
{
    /// <summary>SMTP connection settings bound from <c>appsettings.json</c> section <c>Smtp</c>.</summary>
    public sealed class SmtpOptions
    {
        public const string Section = "Smtp";

        /// <summary>SMTP host, e.g. "smtp.gmail.com"</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>SMTP port. Common values: 25 (plain), 465 (SSL), 587 (STARTTLS).</summary>
        public int Port { get; set; } = 587;

        /// <summary>
        /// <c>true</c>  â†’ wrap connection in SSL from the start (port 465).
        /// <c>false</c> â†’ use STARTTLS if the server advertises it (port 587).
        /// </summary>
        public bool UseSsl { get; set; } = false;

        /// <summary>Login username.</summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>Login password. Store in User Secrets / environment variable in production.</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Default "From" address used when <see cref="EmailMessage.From"/> is not set.
        /// E.g. "no-reply@example.com" or "App Name &lt;no-reply@example.com&gt;"
        /// </summary>
        public string DefaultFrom { get; set; } = string.Empty;

        /// <summary>
        /// When <c>true</c>, emails are written to the application log instead of actually sent.
        /// Useful for development environments.
        /// </summary>
        public bool DryRun { get; set; } = false;
    }
}


