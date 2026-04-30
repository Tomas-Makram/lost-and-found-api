using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.Services
{
    public sealed class OtpSettings
    {
        [Range(1, 120)]
        public int OtpExpiryMinutes { get; set; } = 10;

        [Range(1, 20)]
        public int MaxOtpAttempts { get; set; } = 3;

        [Range(0, 120)]
        public int ResendCooldownMinutes { get; set; } = 2;

        [Range(1, 365)]
        public int MailWindowDays { get; set; } = 30;

        [Range(1, 1000)]
        public int MaxMailsInWindow { get; set; } = 20;

        [Range(1, 1000)]
        public int MaxValidationSendsPerWindow { get; set; } = 10;

        [Range(1, 1000)]
        public int MaxResetSendsPerWindow { get; set; } = 10;

        [Range(1, 1440)]
        public int VerifyBlockMinutes { get; set; } = 15;

        [Range(1, 120)]
        public int ResetSessionTokenExpiryMinutes { get; set; } = 10;
    }
}