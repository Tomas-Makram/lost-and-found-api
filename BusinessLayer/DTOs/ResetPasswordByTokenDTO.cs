using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class ResetPasswordByTokenDTO
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string ResetToken { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}