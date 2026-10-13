using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace EcewsPortal.Services;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "smtp.office365.com";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = "ETMSnotify@ecews.org";
    public string FromName { get; set; } = "ECEWS Portal";
}

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public bool IsConfigured
    {
        get
        {
            var o = options.Value;
            return o.Enabled
                   && !string.IsNullOrWhiteSpace(o.Host)
                   && !string.IsNullOrWhiteSpace(o.Username)
                   && !string.IsNullOrWhiteSpace(o.Password)
                   && !string.IsNullOrWhiteSpace(o.From);
        }
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var o = options.Value;
        if (!IsConfigured)
            throw new InvalidOperationException("SMTP is not configured. Set Smtp:Username and Smtp:Password in appsettings.");

        using var message = new MailMessage
        {
            From = new MailAddress(o.From, o.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        using var client = new SmtpClient(o.Host, o.Port)
        {
            EnableSsl = o.UseSsl,
            Credentials = new NetworkCredential(o.Username, o.Password)
        };

        // SmtpClient.SendMailAsync doesn't accept CancellationToken on older APIs.
        await client.SendMailAsync(message);
        logger.LogInformation("Sent email to {To} subject={Subject}", to, subject);
    }
}
