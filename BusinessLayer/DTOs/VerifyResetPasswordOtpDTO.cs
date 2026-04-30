using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class VerifyResetPasswordOtpDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits.")]
        public string OtpCode { get; set; } = string.Empty;
    }
}