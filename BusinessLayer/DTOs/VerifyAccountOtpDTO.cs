using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class VerifyAccountOtpDTO
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits.")]
        public string OtpCode { get; set; } = string.Empty;
    }
}