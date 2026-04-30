using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class CreateFoundItemDTO
    {
        [Required]
        [MinLength(5)]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MinLength(20)]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [Range(-90, 90)]
        public decimal FoundLatitude { get; set; }

        [Required]
        [Range(-180, 180)]
        public decimal FoundLongitude { get; set; }

        [Required]
        [MaxLength(500)]
        public string FoundAddress { get; set; } = string.Empty;

        // Array of image URLs (uploaded separately)
        public List<string> ImageUrls { get; set; } = new();

        [Range(0, 100)]
        public decimal RewardPercentage { get; set; } = 10m;

        [Range(0, double.MaxValue)]
        public decimal? EstimatedValueEGP { get; set; }

        [Required]
        [MinLength(10)]
        [MaxLength(500)]
        public string VerificationQuestion { get; set; } = string.Empty;

        [Required]
        [MinLength(2)]
        [MaxLength(500)]
        public string VerificationAnswer { get; set; } = string.Empty;

        public DateTime FoundAt { get; set; } = DateTime.UtcNow;
    }
}