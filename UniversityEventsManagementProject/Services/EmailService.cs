using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace UniversityEventsManagement.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage);
        Task SendEventRegistrationEmailAsync(string email, string userName, string eventTitle);
        Task SendCertificateEmailAsync(string email, string userName, string eventTitle, string certificateNumber);
        Task Send2FAOTPAsync(string email, string userName, string otpCode);
        Task SendEmailVerificationCodeAsync(string email, string userName, string verificationCode);
        Task SendPasswordResetCodeAsync(string email, string userName, string resetCode);
        Task SendPasswordChangeVerificationCodeAsync(string email, string userName, string verificationCode);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EmailService(
            IConfiguration configuration, 
            ILogger<EmailService> logger, 
            IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetBaseUrl()
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request == null)
                return "https://localhost:7248";

            return $"{request.Scheme}://{request.Host}";
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("Attempted to send email with empty address");
                return;
            }

            try
            {
                var smtpSettings = _configuration.GetSection("EmailSettings");
                var smtpServer = smtpSettings["SmtpServer"];
                var smtpPort = int.Parse(smtpSettings["SmtpPort"] ?? "587");
                var senderEmail = smtpSettings["SenderEmail"];
                var senderPassword = smtpSettings["SenderPassword"];
                if (string.IsNullOrWhiteSpace(senderEmail))
                {
                    _logger.LogWarning("Sender email is not configured");
                    return;
                }

                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(senderEmail, senderPassword);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(senderEmail, "University Events Management"),
                        Subject = subject,
                        Body = htmlMessage,
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(email);

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation($"Email sent successfully to {email}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending email to {email}: {ex.Message}");
            }
        }

        public async Task SendEventRegistrationEmailAsync(string email, string userName, string eventTitle)
        {
            var baseUrl = GetBaseUrl();
            var subject = "Event Registration Confirmation";
            var htmlMessage = $@"
                <div style='font-family: Arial, sans-serif; direction: ltr; text-align: left; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9;'>
                    <div style='background: linear-gradient(135deg, #5E8C3E 0%, #66B2FF 100%); color: white; padding: 20px; border-radius: 10px 10px 0 0; text-align: center;'>
                        <h2 style='margin: 0;'>Event Registration Confirmed</h2>
                    </div>
                    <div style='background-color: white; padding: 30px; border-radius: 0 0 10px 10px;'>
                        <h3 style='color: #333; margin-top: 0;'>Hello {userName},</h3>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Your registration for the event <strong>{eventTitle}</strong> has been confirmed successfully.
                        </p>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Thank you for participating in our events!
                        </p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{baseUrl}/Home/Dashboard' style='background-color: #5E8C3E; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Go to Dashboard
                            </a>
                        </div>
                    </div>
                    <div style='text-align: center; margin-top: 20px; color: #999; font-size: 12px;'>
                        <p>University Events Management</p>
                        <p>© {DateTime.Now.Year} All Rights Reserved</p>
                    </div>
                </div>
            ";

            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task SendCertificateEmailAsync(string email, string userName, string eventTitle, string certificateNumber)
        {
            var baseUrl = GetBaseUrl();
            var subject = "Event Participation Certificate";
            var htmlMessage = $@"
                <div style='font-family: Arial, sans-serif; direction: ltr; text-align: left; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9;'>
                    <div style='background: linear-gradient(135deg, #5E8C3E 0%, #66B2FF 100%); color: white; padding: 20px; border-radius: 10px 10px 0 0; text-align: center;'>
                        <h2 style='margin: 0;'>Certificate Issued</h2>
                    </div>
                    <div style='background-color: white; padding: 30px; border-radius: 0 0 10px 10px;'>
                        <h3 style='color: #333; margin-top: 0;'>Hello {userName},</h3>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            A certificate has been issued for your participation in the event: <strong>{eventTitle}</strong>
                        </p>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            <strong>Certificate Number:</strong> {certificateNumber}
                        </p>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            You can download your certificate from your dashboard.
                        </p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{baseUrl}/Certificates/MyCertificates' style='background-color: #5E8C3E; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                View Certificates
                            </a>
                        </div>
                    </div>
                    <div style='text-align: center; margin-top: 20px; color: #999; font-size: 12px;'>
                        <p>University Events Management</p>
                        <p>© {DateTime.Now.Year} All Rights Reserved</p>
                    </div>
                </div>
            ";

            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task Send2FAOTPAsync(string email, string userName, string otpCode)
        {
            var subject = "Two-Factor Authentication Verification Code";
            var htmlMessage = $@"
                <div style='font-family: Arial, sans-serif; direction: ltr; text-align: left; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9;'>
                    <div style='background: linear-gradient(135deg, #66B2FF 0%, #4A90E2 100%); color: white; padding: 20px; border-radius: 10px 10px 0 0; text-align: center;'>
                        <h2 style='margin: 0;'>Two-Factor Authentication Code</h2>
                    </div>
                    <div style='background-color: white; padding: 30px; border-radius: 0 0 10px 10px;'>
                        <h3 style='color: #333; margin-top: 0;'>Hello {userName},</h3>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            You have requested to log in to your account in the University Events Management System.
                            Please use the following code to verify your identity:
                        </p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <div style='display: inline-block; background-color: #f0f0f0; padding: 20px 40px; border-radius: 8px; border: 2px dashed #66B2FF;'>
                                <span style='font-size: 32px; font-weight: bold; color: #66B2FF; letter-spacing: 8px; font-family: monospace;'>{otpCode}</span>
                            </div>
                        </div>
                        <p style='color: #999; font-size: 14px; line-height: 1.6;'>
                            <strong>Note:</strong> This code is valid for 10 minutes only. Do not share this code with anyone.
                        </p>
                        <p style='color: #999; font-size: 14px; line-height: 1.6;'>
                            If you did not request to log in, please ignore this email or change your password.
                        </p>
                    </div>
                    <div style='text-align: center; margin-top: 20px; color: #999; font-size: 12px;'>
                        <p>University Events Management</p>
                        <p>© {DateTime.Now.Year} All Rights Reserved</p>
                    </div>
                </div>
            ";

            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task SendEmailVerificationCodeAsync(string email, string userName, string verificationCode)
        {
            var subject = "Email Verification Code";
            var htmlMessage = $@"
                <div style='font-family: Arial, sans-serif; direction: ltr; text-align: left; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9;'>
                    <div style='background: linear-gradient(135deg, #5E8C3E 0%, #66B2FF 100%); color: white; padding: 20px; border-radius: 10px 10px 0 0; text-align: center;'>
                        <h2 style='margin: 0;'>Welcome to University Events Management</h2>
                    </div>
                    <div style='background-color: white; padding: 30px; border-radius: 0 0 10px 10px;'>
                        <h3 style='color: #333; margin-top: 0;'>Hello {userName},</h3>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Thank you for registering with the University Events Management System.
                            Please use the following code to verify your email address:
                        </p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <div style='display: inline-block; background-color: #f0f0f0; padding: 20px 40px; border-radius: 8px; border: 2px dashed #66B2FF;'>
                                <span style='font-size: 32px; font-weight: bold; color: #66B2FF; letter-spacing: 8px; font-family: monospace;'>{verificationCode}</span>
                            </div>
                        </div>
                        <p style='color: #999; font-size: 14px; line-height: 1.6;'>
                            <strong>Note:</strong> This code is valid for 10 minutes only. After verifying your email, you will be able to log in to your account.
                        </p>
                        <p style='color: #999; font-size: 14px; line-height: 1.6;'>
                            If you did not request to create an account, please ignore this email.
                        </p>
                    </div>
                    <div style='text-align: center; margin-top: 20px; color: #999; font-size: 12px;'>
                        <p>University Events Management</p>
                        <p>© {DateTime.Now.Year} All Rights Reserved</p>
                    </div>
                </div>
            ";

            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task SendPasswordResetCodeAsync(string email, string userName, string resetCode)
        {
            var subject = "Password Reset Verification Code";
            var htmlMessage = $@"
                <div style='font-family: Arial, sans-serif; direction: ltr; text-align: left; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9;'>
                    <div style='background: linear-gradient(135deg, #C0392B 0%, #E74C3C 100%); color: white; padding: 20px; border-radius: 10px 10px 0 0; text-align: center;'>
                        <h2 style='margin: 0;'>Password Reset Request</h2>
                    </div>
                    <div style='background-color: white; padding: 30px; border-radius: 0 0 10px 10px;'>
                        <h3 style='color: #333; margin-top: 0;'>Hello {userName},</h3>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            You have requested to reset your password for your account in the University Events Management System.
                            Please use the following code to verify your identity and reset your password:
                        </p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <div style='display: inline-block; background-color: #f0f0f0; padding: 20px 40px; border-radius: 8px; border: 2px dashed #C0392B;'>
                                <span style='font-size: 32px; font-weight: bold; color: #C0392B; letter-spacing: 8px; font-family: monospace;'>{resetCode}</span>
                            </div>
                        </div>
                        <p style='color: #999; font-size: 14px; line-height: 1.6;'>
                            <strong>Note:</strong> This code is valid for 10 minutes only. Do not share this code with anyone.
                        </p>
                        <p style='color: #999; font-size: 14px; line-height: 1.6;'>
                            If you did not request a password reset, please ignore this email or contact support if you have concerns about your account security.
                        </p>
                    </div>
                    <div style='text-align: center; margin-top: 20px; color: #999; font-size: 12px;'>
                        <p>University Events Management</p>
                        <p>© {DateTime.Now.Year} All Rights Reserved</p>
                    </div>
                </div>
            ";

            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task SendPasswordChangeVerificationCodeAsync(string email, string userName, string verificationCode)
        {
            var subject = "Password Change Verification Code";
            var htmlMessage = $@"
                <div style='font-family: Arial, sans-serif; direction: ltr; text-align: left; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9;'>
                    <div style='background: linear-gradient(135deg, #66B2FF 0%, #4A90E2 100%); color: white; padding: 20px; border-radius: 10px 10px 0 0; text-align: center;'>
                        <h2 style='margin: 0;'>Password Change Verification</h2>
                    </div>
                    <div style='background-color: white; padding: 30px; border-radius: 0 0 10px 10px;'>
                        <h3 style='color: #333; margin-top: 0;'>Hello {userName},</h3>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            You have requested to change your password for your account in the University Events Management System.
                            Please use the following code to verify your identity and complete the password change:
                        </p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <div style='display: inline-block; background-color: #f0f0f0; padding: 20px 40px; border-radius: 8px; border: 2px dashed #66B2FF;'>
                                <span style='font-size: 32px; font-weight: bold; color: #66B2FF; letter-spacing: 8px; font-family: monospace;'>{verificationCode}</span>
                            </div>
                        </div>
                        <p style='color: #999; font-size: 14px; line-height: 1.6;'>
                            <strong>Note:</strong> This code is valid for 10 minutes only. Do not share this code with anyone.
                        </p>
                        <p style='color: #999; font-size: 14px; line-height: 1.6;'>
                            If you did not request to change your password, please ignore this email or contact support if you have concerns about your account security.
                        </p>
                    </div>
                    <div style='text-align: center; margin-top: 20px; color: #999; font-size: 12px;'>
                        <p>University Events Management</p>
                        <p>© {DateTime.Now.Year} All Rights Reserved</p>
                    </div>
                </div>
            ";

            await SendEmailAsync(email, subject, htmlMessage);
        }
    }
}
