using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class ChangeFieldsDTO
    {
        [Required]
        public Guid UserId { get; set; } = Guid.NewGuid();

        [Url]
        public string UserProfileImgURL { get; set; } = string.Empty;

        [MinLength(3)]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [RegularExpression(@"^(01)[0-2,5]{1}[0-9]{8}$", ErrorMessage = "Phone number is not valid.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
        public decimal Latitude { get; set; } = 0;

        [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
        public decimal Longitude { get; set; } = 0;

        [MaxLength(255)]
        public string Address { get; set; } = string.Empty;

        [StringLength(14, MinimumLength = 14, ErrorMessage = "National ID must be exactly 14 digits.")]
        [RegularExpression(@"^\d{14}$", ErrorMessage = "National ID must contain exactly 14 digits.")]
        public string NationalId { get; set; } = string.Empty;
    }
}