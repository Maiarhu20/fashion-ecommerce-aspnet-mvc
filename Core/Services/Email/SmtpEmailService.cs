using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Services.Email
{
    public class SmtpEmailService : IEmailService
    {
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IOptions<SmtpSettings> smtpSettings, ILogger<SmtpEmailService> logger)
        {
            _smtpSettings = smtpSettings.Value;
            _logger = logger;
        }

        public async Task<EmailResult> SendPasswordResetEmailAsync(string email, string userName, string resetLink)
        {
            try
            {
                var subject = "Password Reset Request - Admin Panel";
                var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; }}
        .content {{ background: #f9f9f9; padding: 20px; }}
        .button {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block; }}
        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Password Reset</h1>
        </div>
        <div class='content'>
            <p>Hello {userName},</p>
            <p>You requested to reset your password. Click the button below to set a new password:</p>
            <p style='text-align: center;'>
                <a href='{resetLink}' class='button'>Reset Password</a>
            </p>
            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p><code>{resetLink}</code></p>
            <p>This link will expire in 1 hour for security reasons.</p>
            <p>If you didn't request this reset, please ignore this email.</p>
        </div>
        <div class='footer'>
            <p>&copy; {DateTime.Now.Year} Your Company Name. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

                return await SendEmailAsync(email, subject, htmlContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
                return EmailResult.FailureResult($"Failed to send email: {ex.Message}");
            }
        }

        public async Task<EmailResult> SendEmailAsync(string toEmail, string subject, string htmlContent)
        {
            try
            {
                using var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
                {
                    EnableSsl = _smtpSettings.EnableSsl,
                    Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password),
                    Timeout = 10000 // 10 seconds
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_smtpSettings.FromEmail, _smtpSettings.FromName),
                    Subject = subject,
                    Body = htmlContent,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);

                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
                return EmailResult.SuccessResult("Email sent successfully");
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, "SMTP error sending email to {Email}. Status: {StatusCode}", toEmail, smtpEx.StatusCode);
                return EmailResult.FailureResult($"SMTP error: {smtpEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending email to {Email}", toEmail);
                return EmailResult.FailureResult($"Failed to send email: {ex.Message}");
            }
        }
    }

    public class SmtpSettings
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public string Username { get; set; }
        public string Password { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
        public bool EnableSsl { get; set; } = true;
    }
}