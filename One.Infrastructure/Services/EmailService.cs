using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using One.Infrastructure.Interfaces;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace One.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var smtpHost = _config["Email:SmtpHost"];
            var smtpPort = _config.GetValue<int>("Email:SmtpPort", 587);
            var smtpUser = _config["Email:SmtpUser"];
            var smtpPass = _config["Email:SmtpPass"];
            var fromEmail = _config["Email:FromEmail"] ?? "noreply@oneerp.com";
            var fromName = _config["Email:FromName"] ?? "OneERP";

            // Never log the recipient or body because the message may contain an OTP.
            if (string.IsNullOrEmpty(smtpHost))
            {
                throw new InvalidOperationException("SMTP is not configured.");
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(to);

            await client.SendMailAsync(mailMessage);
        }
    }
}
