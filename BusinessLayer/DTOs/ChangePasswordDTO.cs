using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class ChangePasswordDTO
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string OldPassword { get; set; } = string.Empty;

        [Required]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        public string ConfirmPassword { get; set; }= string.Empty;
    }
}
