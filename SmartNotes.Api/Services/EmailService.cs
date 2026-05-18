using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace SmartNotes.Api.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            // Agafem les dades del teu appsettings.json
            var host = _config["SmtpSettings:Host"];
            var port = int.Parse(_config["SmtpSettings:Port"]!);
            var user = _config["SmtpSettings:Username"];
            var pass = _config["SmtpSettings:Password"];
            var fromName = _config["SmtpSettings:FromName"];
            var fromEmail = _config["SmtpSettings:FromEmail"];

            var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, pass),
                EnableSsl = true // Utilitzem connexió segura
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail!, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true // Permet enviar el correu amb disseny (negretes, enllaços, botons)
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
        }
    }
}