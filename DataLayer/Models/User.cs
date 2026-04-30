using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Models
{
    public class User
    {
        [Key]
        public Guid UserId { get; set; } = Guid.NewGuid();

        // Personal Information
        [Required]
        public string UserProfileImgURL { get; set; } = string.Empty;

        [Required]
        public string UserName { get; set; } = string.Empty;
        
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string EmailChipher { get; set; } = string.Empty;
        [Required]
        public string EmailHash { get; set; } = string.Empty;

        [Required]
        public string PhoneNumberChipher { get; set; } = string.Empty;
        [Required]
        public string PhoneNumberHash { get; set; } = string.Empty;

        [Required]
        public string NationalIdChipher { get; set; } = string.Empty;

        [Required]
        public string NationalIdHash { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // Location Information
        [Required]
        [Column(TypeName = "decimal(9,6)")]
        public decimal Latitude { get; set; }

        [Required]
        [Column(TypeName = "decimal(9,6)")]
        public decimal Longitude { get; set; }

        [Required]
        [MaxLength(255)]
        public string Address { get; set; } = string.Empty;

        // Account Information
        public string AccountType { get; set; } = "User";
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }
        public bool Verified { get; set; } = false;
        public bool Login { get; set; } = false;
        public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();

        // Account Verification OTP
        [Required]
        public string OtpHash { get; set; } = ""; // Code OTP

        public DateTime? OtpExpiresAt { get; set; } // Expire Time
        public int OtpAttempts { get; set; } = 0; // Incorrect Number Input OTP
        public DateTime? OtpLastSentAt { get; set; } // Last Time Send OTP
        public int OtpRequestsCount { get; set; } = 0; // Number of times the account verification OTP

        // Reset Password OTP
        public string? PasswordResetOtpHash { get; set; } // Code OTP
        public DateTime? PasswordResetOtpExpiresAt { get; set; } // Expire Time
        public int PasswordResetOtpAttempts { get; set; } = 0; // Incorrect Number Input OTP
        public DateTime? LastMailSentResetPasswordAt { get; set; } // Last Time Send OTP
        public int PasswordOtpResetCount { get; set; } = 0; // Number of times the account verification OTP

        // Global Mail Limits
        public int MailActionsCount { get; set; } = 0; // Total OTP Mail Sending
        public DateTime MailActionsResetAt { get; set; } = DateTime.UtcNow; // First Time Send Mail
        public DateTime? MailBlockedUntil { get; set; } // Time Block Account

        // Per-flow window + verify blocking
        public DateTime ValidationOtpWindowResetAt { get; set; } = DateTime.UtcNow; // The countdown window for sending the OTP
        public DateTime ResetOtpWindowResetAt { get; set; } = DateTime.UtcNow; // The countdown window for sending OTP password reset

        // Block verification attempts
        public DateTime? OtpVerifyBlockedUntil { get; set; } // Time of expiry of the temporary ban on attempts to enter OTP
        public DateTime? ResetOtpVerifyBlockedUntil { get; set; } // Time of expiry of the temporary ban on attempts to enter OTP password reset

        public string? PasswordResetSessionTokenHash { get; set; }
        public DateTime? PasswordResetSessionTokenExpiresAt { get; set; }
        public DateTime? PasswordChangedAt { get; set; }

        // Account security
        public bool Blocked { get; set; } = false;
        public int FailedLoginAttempts { get; set; } = 0;

        // Accounting Paying & Wallet
        public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
        public Wallet? Wallet { get; set; }
    }
}
