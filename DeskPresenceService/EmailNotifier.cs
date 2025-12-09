using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace DeskPresenceService;

public class EmailNotifier
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailNotifier> _logger;

    public EmailNotifier(IConfiguration config, ILogger<EmailNotifier> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task TrySendReportAsync(string[] attachmentPaths)
    {
        if (!_config.GetValue("Email:Enabled", true))
        {
            _logger.LogInformation("Email sending disabled; skipping.");
            return;
        }

        try
        {
            string? smtpServer = _config["Email:SmtpServer"];
            int port = _config.GetValue("Email:Port", 587);
            string? username = _config["Email:Username"];
            string? password = _config["Email:Password"];
            string? toAddress = _config["Email:To"];

            if (string.IsNullOrWhiteSpace(smtpServer) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(toAddress))
            {
                _logger.LogWarning("Email configuration incomplete; cannot send report.");
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Desk Presence Tracker", username));
            message.To.Add(new MailboxAddress("", toAddress));
            message.Subject = "EOFY Work-From-Home Report";

            var builder = new BodyBuilder
            {
                TextBody = "Attached are your WFH EOFY reports (TXT and CSV)."
            };

            foreach (var path in attachmentPaths)
            {
                if (File.Exists(path))
                    builder.Attachments.Add(path);
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("EOFY report email sent to {To}.", toAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send EOFY report email.");
        }
    }
}
