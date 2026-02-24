using System.Threading.Tasks;

namespace Core.Services.Email
{
    public interface IEmailService
    {
        Task<EmailResult> SendPasswordResetEmailAsync(string email, string userName, string resetLink);
        Task<EmailResult> SendEmailAsync(string toEmail, string subject, string htmlContent);
    }

    public class EmailResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }

        public static EmailResult SuccessResult(string message = null)
            => new EmailResult { Success = true, Message = message };

        public static EmailResult FailureResult(string error)
            => new EmailResult { Success = false, Error = error };
    }
}