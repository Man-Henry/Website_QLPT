using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Website_QLPT.Services
{
    public class SmtpEmailSender : IEmailSenderService, IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            return SendEmailCoreAsync(email, subject, htmlMessage, strict: true);
        }

        async Task IEmailSender.SendEmailAsync(string email, string subject, string htmlMessage)
        {
            await SendEmailCoreAsync(email, subject, htmlMessage, strict: false);
        }

        private async Task SendEmailCoreAsync(string email, string subject, string htmlMessage, bool strict)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var host = smtpSettings["Host"];
            var userName = smtpSettings["UserName"];
            var password = smtpSettings["Password"];
            var fromEmail = smtpSettings["FromEmail"];
            var fromName = smtpSettings["FromName"] ?? "Website_QLPT";

            if (string.IsNullOrWhiteSpace(host)
                || string.IsNullOrWhiteSpace(userName)
                || string.IsNullOrWhiteSpace(password)
                || string.IsNullOrWhiteSpace(fromEmail))
            {
                const string message = "SMTP settings are not configured. Email sending is skipped.";
                _logger.LogWarning(message);

                if (strict)
                {
                    throw new InvalidOperationException(message);
                }

                return;
            }

            try
            {
                var port = int.Parse(smtpSettings["Port"] ?? "587");

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(userName, password),
                    EnableSsl = true
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail!, fromName),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };
                
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email successfully sent to {Email} with Subject: {Subject}", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", email);

                if (strict)
                {
                    throw;
                }
            }
        }
    }
}
