using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net.Mail;
using System.Net;

namespace BusinessLayer.Services
{
    // Structure of Email Template
    public record MailSender
    {
        public string Email { get; init; }
        public string Subject { get; init; }
        public string Body { get; init; }

        public MailSender(string email, string subject, string body)
        {
            Email = email;
            Subject = subject;
            Body = body;
        }
    }

    public static class EmailTemplateConfig
    {
        // Brand Information
        public static string CompanyName => "Lost & Found";
        public static string SupportEmail => "support@lostandfound.com";
        public static string SupportPhone => "+20 122 193 6850";
        public static string WebsiteUrl => "https://localhost:7214";
        public static string AppName => "Lost & Found";
        public static string SupportLocation => "Cairo, Egypt";

        // Social Media Links
        public static string FacebookUrl => "https://facebook.com/lostandfound";
        public static string TwitterUrl => "https://twitter.com/lostandfound";
        public static string InstagramUrl => "https://instagram.com/lostandfound";
        public static string LinkedInUrl => "https://linkedin.com/company/lostandfound";

        public static class Templates
        {
            public static string OTPExpiryMinutes = "10";
            public static string OTPMaxAttempts = "3";
            public static string OTPResetExpiry = "10";
            public static string ContactHours => "9 AM - 6 PM (GMT+2)";
        }
    }

    public interface IEmailTemplateService
    {
        // Email Sending
        Task SendEmailAsync(MailSender mail);

        // Email Templates
        MailSender CreateWelcomeEmail(string email, string? userName = null);
        MailSender CreateOtpVerificationEmail(string email, string otpCode, string? userName = null);
        MailSender CreateAccountVerifiedEmail(string email, string? userName = null);
        MailSender CreatePasswordResetOtpEmail(string email, string otpCode, string? userName = null);
        MailSender CreatePasswordChangedEmail(string email, string? userName = null);
        MailSender CreateNotificationEmail(string email, string subject, string message, string? userName = null);

        // Additional Email Types
        MailSender CreateOrderConfirmationEmail(string email, string orderId, string? userName = null);
        MailSender CreateNewsletterEmail(string email, string newsletterTitle, string content, string? userName = null);
        MailSender CreateSupportTicketEmail(string email, string ticketId, string? userName = null);
        MailSender CreateEventReminderEmail(string email, string eventName, DateTime eventDate, string? userName = null);
    }

    public class MailService : IEmailSender, IEmailTemplateService
    {
        private readonly IConfiguration _configuration;
        private readonly IOtpService _otpService;
        private readonly ILogger<MailService> _logger;

        public MailService(IConfiguration configuration, IOtpService otpService, ILogger<MailService> logger)
        {
            _configuration = configuration;
            _otpService = otpService;
            _logger = logger;

            EmailTemplateConfig.Templates.OTPExpiryMinutes = _otpService.GetOtpExpiryMinutes().ToString();
            EmailTemplateConfig.Templates.OTPMaxAttempts = _otpService.GetMaxOtpAttempts().ToString();
            EmailTemplateConfig.Templates.OTPResetExpiry = _otpService.GetResendCooldownMinutes().ToString();
        }

        // IEmailSender Implementation Interface
        public Task SendEmailAsync(string to, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("Recipient email cannot be null or empty", nameof(to));

            bool enableSsl = true;
            int port = 587;

            bool.TryParse(_configuration["EmailSettings:EnableSsl"], out enableSsl);
            int.TryParse(_configuration["EmailSettings:MailPort"], out port);

            var client = new SmtpClient(_configuration["EmailSettings:MailServer"], port)
            {
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    _configuration["EmailSettings:SenderEmail"],
                    _configuration["EmailSettings:Password"]
                )
            };

            var message = new MailMessage(
                from: _configuration["EmailSettings:SenderEmail"]!,
                to: to,
                subject: subject,
                body: body
            )
            {
                IsBodyHtml = true
            };

            return client.SendMailAsync(message);
        }

        // Send Email
        public async Task SendEmailAsync(MailSender mail)
        {
            if (mail == null)
                throw new ArgumentNullException(nameof(mail));

            await SendEmailAsync(mail.Email, mail.Subject, mail.Body);
        }

        // =========================================================
        // Email Templates
        // =========================================================

        public MailSender CreateWelcomeEmail(string email, string? userName = null)
        {
            var subject = $"🔎 Welcome to {EmailTemplateConfig.AppName}!";

            var body = GenerateEmailTemplate(
                title: "Welcome to Lost & Found",
                greeting: $"Hello {(string.IsNullOrWhiteSpace(userName) ? "there" : userName)} 👋",
                mainContent: $@"
                    <p>
                        Welcome to <strong>{EmailTemplateConfig.CompanyName}</strong> — a trusted platform that helps people
                        recover lost belongings by connecting the original owner with the person who found the item.
                    </p>

                    <div class='feature-box' style='background:#eef6ff; border-left:4px solid #1d4ed8;'>
                        <h3 style='margin-top:0; color:#0f172a;'>How the platform works</h3>
                        <ul style='padding-left:18px; margin-bottom:0;'>
                            <li>People can report found items with clear details and photos.</li>
                            <li>The original owner can search for the item and contact the finder through the item page.</li>
                            <li>Only the rightful owner should claim the lost item.</li>
                            <li>Every listing must be reviewed and approved by <strong>Admins</strong> before it appears on the website.</li>
                        </ul>
                    </div>

                    <div class='feature-box' style='background:#f8fafc; border-left:4px solid #0f766e;'>
                        <h3 style='margin-top:0; color:#0f172a;'>Before you start</h3>
                        <ol style='padding-left:18px; margin-bottom:0;'>
                            <li>Create your account and sign in.</li>
                            <li>Verify your email address to secure your account.</li>
                            <li>Complete your profile information.</li>
                            <li>After verification, you can submit a lost or found item request.</li>
                        </ol>
                    </div>

                    <div class='note-card'>
                        <strong>Important:</strong> To protect users and reduce false claims, account verification is required before using important platform features.
                    </div>

                    <p>
                        Our mission is simple: <strong>help every lost item find its way back to its real owner</strong>.
                    </p>",
                buttonText: "Sign In to Your Account",
                buttonLink: $"{EmailTemplateConfig.WebsiteUrl}/login",
                themeColor: "#1d4ed8"
            );

            return new MailSender(email, subject, body);
        }

        public MailSender CreateOtpVerificationEmail(string email, string otpCode, string? userName = null)
        {
            var subject = $"🔐 Verify Your {EmailTemplateConfig.AppName} Account";

            var body = GenerateEmailTemplate(
                title: "Verify Your Account",
                greeting: $"Hello {(string.IsNullOrWhiteSpace(userName) ? "there" : userName)}",
                mainContent: $@"
                    <p>
                        Thank you for registering with <strong>{EmailTemplateConfig.CompanyName}</strong>.
                        To activate your account and continue using the platform safely, please verify your email address.
                    </p>

                    <div class='otp-box'>
                        <div class='otp-label'>Your Verification Code</div>
                        <div class='otp-code'>{otpCode}</div>
                        <div class='otp-help'>Enter this 6-digit code on the verification page.</div>
                    </div>

                    <div class='warning-box'>
                        <p style='margin:0;'>
                            <strong>Security Notice:</strong><br />
                            • Code expires in <strong>{_otpService.GetOtpExpiryMinutes()} minutes</strong><br />
                            • Maximum attempts: <strong>{_otpService.GetMaxOtpAttempts()} attempts</strong><br />
                            • Never share this code with anyone
                        </p>
                    </div>

                    <p>
                        Verification is required so you can securely interact with items and contact other users through the system.
                    </p>",
                buttonText: "Open Verification Page",
                buttonLink: $"{EmailTemplateConfig.WebsiteUrl}/verify",
                themeColor: "#0f766e",
                showImportantNote: true,
                showSecurityNote: true
            );

            return new MailSender(email, subject, body);
        }

        public MailSender CreateAccountVerifiedEmail(string email, string? userName = null)
        {
            var subject = $"✅ Your {EmailTemplateConfig.AppName} Account Is Verified";

            var body = GenerateEmailTemplate(
                title: "Account Verified Successfully",
                greeting: $"Hello {(string.IsNullOrWhiteSpace(userName) ? "there" : userName)} 🎉",
                mainContent: $@"
                    <p>
                        Your account has been <strong>verified successfully</strong>.
                    </p>

                    <div class='feature-box' style='background:#ecfdf5; border-left:4px solid #16a34a;'>
                        <h3 style='margin-top:0; color:#14532d;'>You can now:</h3>
                        <ul style='padding-left:18px; margin-bottom:0;'>
                            <li>Submit lost or found item requests.</li>
                            <li>Communicate through item-related workflows.</li>
                            <li>Use your account with higher trust and security.</li>
                        </ul>
                    </div>

                    <div class='note-card'>
                        <strong>Please remember:</strong> item listings are not published immediately.
                        Every submission is reviewed by <strong>Admins</strong> before approval.
                    </div>

                    <p>
                        Thank you for helping build a safer and more reliable lost & found community.
                    </p>",
                buttonText: "Go to Dashboard",
                buttonLink: $"{EmailTemplateConfig.WebsiteUrl}/dashboard",
                themeColor: "#16a34a",
                showCelebration: true
            );

            return new MailSender(email, subject, body);
        }

        public MailSender CreatePasswordResetOtpEmail(string email, string otpCode, string? userName = null)
        {
            var subject = $"🔑 Password Reset Code - {EmailTemplateConfig.AppName}";

            var body = GenerateEmailTemplate(
                title: "Reset Your Password",
                greeting: $"Hello {(string.IsNullOrWhiteSpace(userName) ? "there" : userName)}",
                mainContent: $@"
                    <p>
                        We received a request to reset your password for your <strong>{EmailTemplateConfig.CompanyName}</strong> account.
                    </p>

                    <div class='otp-box'>
                        <div class='otp-label'>Your Password Reset Code</div>
                        <div class='otp-code'>{otpCode}</div>
                        <div class='otp-help'>Enter this code on the password reset page.</div>
                    </div>

                    <div class='warning-box'>
                        <p style='margin:0;'>
                            <strong>Security Information:</strong><br />
                            • Code expires in <strong>{_otpService.GetOtpExpiryMinutes()} minutes</strong><br />
                            • Maximum attempts: <strong>{_otpService.GetMaxOtpAttempts()} attempts</strong><br />
                            • Never share this code with anyone
                        </p>
                    </div>

                    <p>
                        If you did not request this reset, please ignore this message and consider updating your password if needed.
                    </p>",
                buttonText: "Reset Password",
                buttonLink: $"{EmailTemplateConfig.WebsiteUrl}/reset-password",
                themeColor: "#7c3aed",
                showSecurityNote: true
            );

            return new MailSender(email, subject, body);
        }

        public MailSender CreatePasswordChangedEmail(string email, string? userName = null)
        {
            var subject = $"✅ Password Changed Successfully - {EmailTemplateConfig.AppName}";

            var body = GenerateEmailTemplate(
                title: "Password Updated",
                greeting: $"Hello {(string.IsNullOrWhiteSpace(userName) ? "there" : userName)}",
                mainContent: @"
                    <p>Your password has been changed successfully.</p>

                    <div class='feature-box' style='background:#f8fafc; border-left:4px solid #334155;'>
                        <h3 style='margin-top:0; color:#0f172a;'>What this means</h3>
                        <ul style='padding-left:18px; margin-bottom:0;'>
                            <li>Your account is now protected with your new password.</li>
                            <li>If this action was not performed by you, contact support immediately.</li>
                            <li>For better security, use a strong and unique password.</li>
                        </ul>
                    </div>",
                buttonText: "Visit Website",
                buttonLink: $"{EmailTemplateConfig.WebsiteUrl}/login",
                themeColor: "#0f172a",
                showSecurityNote: true
            );

            return new MailSender(email, subject, body);
        }

        public MailSender CreateNotificationEmail(string email, string subject, string message, string? userName = null)
        {
            var emailSubject = $"📢 {subject} - {EmailTemplateConfig.AppName}";

            var body = GenerateEmailTemplate(
                title: subject,
                greeting: $"Hello {(string.IsNullOrWhiteSpace(userName) ? "there" : userName)}",
                mainContent: $@"
                    <div class='feature-box' style='background:#ffffff; border-left:4px solid #f59e0b;'>
                        {message}
                    </div>

                    <p>This is an automated notification from {EmailTemplateConfig.CompanyName}.</p>",
                buttonText: "Open Platform",
                buttonLink: $"{EmailTemplateConfig.WebsiteUrl}/home",
                themeColor: "#f59e0b",
                isNotification: true
            );

            return new MailSender(email, emailSubject, body);
        }

        // Additional / reusable templates
        public MailSender CreateOrderConfirmationEmail(string email, string orderId, string? userName = null)
        {
            var subject = $"✅ Request #{orderId} Confirmed - {EmailTemplateConfig.AppName}";

            var body = GenerateEmailTemplate(
                title: "Request Confirmation",
                greeting: $"Hello {(string.IsNullOrWhiteSpace(userName) ? "there" : userName)}",
                mainContent: $@"
                    <p>Your request <strong>#{orderId}</strong> has been received successfully.</p>

                    <div class='feature-box' style='background:#eff6ff; border-left:4px solid #2563eb;'>
                        <h3 style='margin-top:0; color:#1e3a8a;'>Request Details</h3>
                        <p><strong>Reference ID:</strong> {orderId}</p>
                        <p><strong>Status:</strong> Under Review</p>
                        <p><strong>Review Process:</strong> Admin approval is required before publication</p>
                    </div>

                    <p>We will notify you once the status changes.</p>",
                buttonText: "View Request",
                buttonLink: $"{EmailTemplateConfig.WebsiteUrl}/requests/{orderId}",
                themeColor: "#2563eb"
            );

            return new MailSender(email, subject, body);
        }

        public MailSender CreateNewsletterEmail(string email, string newsletterTitle, string content, string? userName = null)
        {
            var subject = $"📰 {newsletterTitle} - {EmailTemplateConfig.AppName}";

            var body = GenerateEmailTemplate(
                title: newsletterTitle,
                greeting: $"Hello {(string.IsNullOrWhiteSpace(userName) ? "there" : userName)}",
                mainContent: $@"
                    <p>Here are the latest updates from <strong>{EmailTemplateConfig.CompanyName}</strong>.</p>

                    <div class='feature-box' style='background:#f8fafc; border-left:4px solid #1d4ed8;'>
                        {content}
                    </div>",
                buttonText: "Read More",
                buttonLink: $"{EmailTemplateConfig.WebsiteUrl}/news",
                themeColor: "#1d4ed8",
                showSocialLinks: true
            );

            return new MailSender(email, subject, body);
        }

        public MailSender CreateSupportTicketEmail(string email, string ticketId, string? userName = null)
        {
            var subject = $"🛠️ Support Ticket #{ticketId} Created - {EmailTemplateConfig.AppName}";

            var body = GenerateEmailTemplate(
                title: "Support Request Created",
                greeting: $"Hello {(string.IsNullOrWhiteSpace(userName) ? "there" : userName)}",
                mainContent: $@"
                    <p>Your support ticket has been created successfully.</p>

                    <div class='feature-box' style='background:#fff7ed; border-left:4px solid #ea580c;'>
                        <h3 style='margin-top:0; color:#9a3412;'>Ticket Information</h3>
                        <p><strong>Ticket ID:</strong> {ticketId}</p>
                        <p><strong>Status:</strong> Open</p>
                        <p><strong>Expected reply:</strong> Within 24 business hours</p>
                    </div>",
                buttonText: "View Ticket",
                buttonLink: $"{EmailTemplateConfig.WebsiteUrl}/support/ticket/{ticketId}",
                themeColor: "#ea580c"
            );

            return new MailSender(email, subject, body);
        }

        public MailSender CreateEventReminderEmail(string email, string eventName, DateTime eventDate, string? userName = null)
        {
            var subject = $"📅 Reminder: {eventName} - {EmailTemplateConfig.AppName}";

            var body = GenerateEmailTemplate(
                title: "Event Reminder",
                greeting: $"Hello {(string.IsNullOrWhiteSpace(userName) ? "there" : userName)}",
                mainContent: $@"
                    <p>This is a reminder for your upcoming event.</p>

                    <div class='feature-box' style='background:#faf5ff; border-left:4px solid #9333ea;'>
                        <h3 style='margin-top:0; color:#581c87;'>Event Details</h3>
                        <p><strong>Event:</strong> {eventName}</p>
                        <p><strong>Date:</strong> {eventDate:dddd, MMMM dd, yyyy}</p>
                        <p><strong>Time:</strong> {eventDate:hh:mm tt}</p>
                    </div>",
                buttonText: "View Event",
                buttonLink: $"{EmailTemplateConfig.WebsiteUrl}/events",
                themeColor: "#9333ea",
                showCalendarIcon: true
            );

            return new MailSender(email, subject, body);
        }

        // =========================================================
        // Core Template Generator
        // =========================================================
        private string GenerateEmailTemplate(string title, string greeting, string mainContent, string buttonText = "", string buttonLink = "", string themeColor = "#1d4ed8", bool showImportantNote = false, bool showCelebration = false, bool showSecurityNote = false, bool isNotification = false, bool showSocialLinks = false, bool showCalendarIcon = false)
        {
            var icon = GetIconByType(title, isNotification, showCelebration, showCalendarIcon);

            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{title}</title>
    <style>
        {GetEmailStyles(themeColor)}
    </style>
</head>
<body>
    <div class='email-wrapper'>
        <div class='email-container'>

            <div class='header' style='background: linear-gradient(135deg, {themeColor}, {DarkenColor(themeColor)});'>
                <div class='brand-badge'>Lost &amp; Found</div>
                <h1>{icon} {title}</h1>
                <p>Helping lost items return to their rightful owners</p>
            </div>

            <div class='content'>
                <div class='greeting'>
                    <h2>{greeting}</h2>
                </div>

                <div class='main-copy'>
                    {mainContent}
                </div>

                {(string.IsNullOrWhiteSpace(buttonText) ? "" : $@"
                <div class='button-container'>
                    <a href='{buttonLink}' class='action-button'>{buttonText}</a>
                </div>")}

                {GetAdditionalNotes(showImportantNote, showSecurityNote)}

                {(showSocialLinks ? GenerateSocialLinks() : "")}
            </div>

            {GenerateEmailFooter()}

        </div>
    </div>
</body>
</html>";
        }

        private string GetEmailStyles(string themeColor)
        {
            return $@"
                body {{
                    margin: 0;
                    padding: 0;
                    background: #eef2f7;
                    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                    color: #1f2937;
                    line-height: 1.7;
                }}

                .email-wrapper {{
                    width: 100%;
                    background: #eef2f7;
                    padding: 24px 12px;
                }}

                .email-container {{
                    max-width: 680px;
                    margin: 0 auto;
                    background: #ffffff;
                    border-radius: 20px;
                    overflow: hidden;
                    box-shadow: 0 12px 40px rgba(15, 23, 42, 0.10);
                    border: 1px solid #e5e7eb;
                }}

                .header {{
                    padding: 40px 28px;
                    text-align: center;
                    color: #ffffff;
                }}

                .brand-badge {{
                    display: inline-block;
                    background: rgba(255,255,255,0.18);
                    color: #ffffff;
                    padding: 8px 14px;
                    border-radius: 999px;
                    font-size: 12px;
                    font-weight: 700;
                    letter-spacing: 0.8px;
                    text-transform: uppercase;
                    margin-bottom: 18px;
                }}

                .header h1 {{
                    margin: 0 0 10px 0;
                    font-size: 30px;
                    font-weight: 800;
                    line-height: 1.3;
                }}

                .header p {{
                    margin: 0;
                    font-size: 15px;
                    opacity: 0.96;
                }}

                .content {{
                    padding: 36px 32px 28px;
                }}

                .greeting h2 {{
                    margin: 0 0 18px 0;
                    font-size: 24px;
                    color: #0f172a;
                    font-weight: 700;
                }}

                .main-copy p {{
                    margin: 0 0 16px 0;
                    font-size: 15px;
                    color: #334155;
                }}

                .main-copy ul,
                .main-copy ol {{
                    color: #334155;
                    font-size: 15px;
                }}

                .feature-box {{
                    margin: 22px 0;
                    padding: 20px 18px;
                    border-radius: 14px;
                }}

                .note-card {{
                    margin: 22px 0;
                    padding: 16px 18px;
                    background: #fffbeb;
                    color: #854d0e;
                    border: 1px solid #fde68a;
                    border-radius: 14px;
                    font-size: 14px;
                }}

                .otp-box {{
                    margin: 24px 0;
                    padding: 26px 18px;
                    text-align: center;
                    background: #f8fafc;
                    border: 2px dashed {themeColor};
                    border-radius: 16px;
                }}

                .otp-label {{
                    font-size: 14px;
                    color: #64748b;
                    margin-bottom: 10px;
                    font-weight: 600;
                }}

                .otp-code {{
                    font-size: 38px;
                    line-height: 1.2;
                    font-weight: 800;
                    letter-spacing: 10px;
                    color: #0f172a;
                    font-family: 'Courier New', monospace;
                    margin-bottom: 10px;
                }}

                .otp-help {{
                    font-size: 13px;
                    color: #64748b;
                }}

                .warning-box {{
                    margin: 22px 0;
                    padding: 16px 18px;
                    background: #fff7ed;
                    border-left: 4px solid #f59e0b;
                    border-radius: 12px;
                    color: #9a3412;
                    font-size: 14px;
                }}

                .button-container {{
                    text-align: center;
                    margin: 28px 0 14px 0;
                }}

                .action-button {{
                    display: inline-block;
                    background: {themeColor};
                    color: #ffffff !important;
                    text-decoration: none;
                    padding: 15px 34px;
                    border-radius: 12px;
                    font-size: 15px;
                    font-weight: 700;
                    box-shadow: 0 8px 20px rgba(0,0,0,0.08);
                }}

                .important-note {{
                    background: #eff6ff;
                    border-left: 4px solid #2563eb;
                    padding: 16px;
                    margin: 20px 0;
                    border-radius: 12px;
                    color: #1e3a8a;
                    font-size: 14px;
                }}

                .security-note {{
                    background: #fef2f2;
                    border-left: 4px solid #dc2626;
                    padding: 16px;
                    margin: 20px 0;
                    border-radius: 12px;
                    color: #991b1b;
                    font-size: 14px;
                }}

                .footer {{
                    padding: 28px 24px 34px;
                    background: #f8fafc;
                    border-top: 1px solid #e5e7eb;
                    text-align: center;
                }}

                .footer-logo {{
                    font-size: 18px;
                    font-weight: 800;
                    color: #0f172a;
                    margin-bottom: 12px;
                }}

                .contact-info p {{
                    margin: 6px 0;
                    color: #64748b;
                    font-size: 14px;
                }}

                .social-links {{
                    margin-top: 18px;
                }}

                .social-links a {{
                    display: inline-block;
                    margin: 0 8px;
                    color: {themeColor};
                    text-decoration: none;
                    font-weight: 600;
                    font-size: 14px;
                }}

                .footer-bottom {{
                    margin-top: 18px;
                    font-size: 12px;
                    color: #94a3b8;
                    line-height: 1.8;
                }}

                .footer-bottom a {{
                    color: #64748b;
                    text-decoration: none;
                }}

                @media only screen and (max-width: 600px) {{
                    .header {{
                        padding: 30px 18px;
                    }}

                    .header h1 {{
                        font-size: 24px;
                    }}

                    .content {{
                        padding: 24px 20px 22px;
                    }}

                    .greeting h2 {{
                        font-size: 21px;
                    }}

                    .otp-code {{
                        font-size: 30px;
                        letter-spacing: 6px;
                    }}

                    .action-button {{
                        display: block;
                        width: 100%;
                        box-sizing: border-box;
                    }}
                }}";
        }

        private string GenerateEmailFooter()
        {
            return $@"
<div class='footer'>
    <div class='footer-logo'>Lost &amp; Found</div>

    <div class='contact-info'>
        <p><strong>{EmailTemplateConfig.CompanyName}</strong></p>
        <p>📍 {EmailTemplateConfig.SupportLocation}</p>
        <p>📞 {EmailTemplateConfig.SupportPhone}</p>
        <p>✉️ {EmailTemplateConfig.SupportEmail}</p>
        <p>🌐 {EmailTemplateConfig.WebsiteUrl}</p>
    </div>

    <div class='social-links'>
        <a href='{EmailTemplateConfig.FacebookUrl}'>Facebook</a>
        <a href='{EmailTemplateConfig.TwitterUrl}'>Twitter</a>
        <a href='{EmailTemplateConfig.InstagramUrl}'>Instagram</a>
        <a href='{EmailTemplateConfig.LinkedInUrl}'>LinkedIn</a>
    </div>

    <div class='footer-bottom'>
        <p>© {DateTime.Now.Year} {EmailTemplateConfig.CompanyName}. All rights reserved.</p>
        <p>This is an automated email. Please do not reply directly to this message.</p>
        <p>Support Hours: {EmailTemplateConfig.Templates.ContactHours}</p>
        <p>
            <a href='{EmailTemplateConfig.WebsiteUrl}/privacy'>Privacy Policy</a> |
            <a href='{EmailTemplateConfig.WebsiteUrl}/terms'>Terms of Service</a>
        </p>
    </div>
</div>";
        }

        private string GenerateSocialLinks()
        {
            return $@"
<div class='social-links' style='text-align:center; margin-top:28px;'>
    <a href='{EmailTemplateConfig.FacebookUrl}'>Facebook</a>
    <a href='{EmailTemplateConfig.TwitterUrl}'>Twitter</a>
    <a href='{EmailTemplateConfig.InstagramUrl}'>Instagram</a>
    <a href='{EmailTemplateConfig.LinkedInUrl}'>LinkedIn</a>
</div>";
        }

        private string GetAdditionalNotes(bool showImportantNote, bool showSecurityNote)
        {
            var notes = "";

            if (showImportantNote)
            {
                notes += @"
<div class='important-note'>
    <strong>Important:</strong> Your access to core platform actions may remain limited until your account is verified.
</div>";
            }

            if (showSecurityNote)
            {
                notes += $@"
<div class='security-note'>
    <strong>Security Notice:</strong> Never share your password, OTP, or personal information. 
    {EmailTemplateConfig.CompanyName} will never ask you to send sensitive information by email.
</div>";
            }

            return notes;
        }

        private string GetIconByType(string title, bool isNotification, bool showCelebration, bool showCalendarIcon)
        {
            if (showCelebration) return "🎉";
            if (showCalendarIcon) return "📅";
            if (isNotification) return "📢";

            if (title.Contains("Welcome", StringComparison.OrdinalIgnoreCase)) return "🔎";
            if (title.Contains("Verify", StringComparison.OrdinalIgnoreCase)) return "🔐";
            if (title.Contains("Password", StringComparison.OrdinalIgnoreCase)) return "🔑";
            if (title.Contains("Support", StringComparison.OrdinalIgnoreCase)) return "🛠️";
            if (title.Contains("Reminder", StringComparison.OrdinalIgnoreCase)) return "📌";
            if (title.Contains("Confirmed", StringComparison.OrdinalIgnoreCase)) return "✅";

            return "📍";
        }

        private string DarkenColor(string hexColor)
        {
            try
            {
                var color = System.Drawing.ColorTranslator.FromHtml(hexColor);

                return System.Drawing.ColorTranslator.ToHtml(
                    System.Drawing.Color.FromArgb(
                        Math.Max(color.R - 35, 0),
                        Math.Max(color.G - 35, 0),
                        Math.Max(color.B - 35, 0)
                    )
                );
            }
            catch
            {
                return hexColor;
            }
        }
    }
}