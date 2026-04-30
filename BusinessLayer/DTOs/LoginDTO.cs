using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class LoginDTO
    {
        [Required]
        public string EmailOrPhoneOrUsernameOrNationalId { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
