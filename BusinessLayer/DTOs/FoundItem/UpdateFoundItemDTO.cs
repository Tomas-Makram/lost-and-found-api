using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class UpdateFoundItemDTO
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        [Range(-90, 90)]
        public decimal? FoundLatitude { get; set; }

        [Range(-180, 180)]
        public decimal? FoundLongitude { get; set; }

        [MaxLength(500)]
        public string? FoundAddress { get; set; }

        public List<string>? ImageUrls { get; set; }

        [Range(0, 100)]
        public decimal? RewardPercentage { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? EstimatedValueEGP { get; set; }

        [MaxLength(500)]
        public string? VerificationQuestion { get; set; }

        [MaxLength(500)]
        public string? VerificationAnswer { get; set; }
    }
}