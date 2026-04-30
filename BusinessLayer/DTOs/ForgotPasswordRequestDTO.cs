using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class ForgotPasswordRequestDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}