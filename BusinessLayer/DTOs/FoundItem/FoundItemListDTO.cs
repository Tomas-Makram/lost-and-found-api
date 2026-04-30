namespace BusinessLayer.DTOs
{
    public class FoundItemListDTO
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal FoundLatitude { get; set; }
        public decimal FoundLongitude { get; set; }
        public string FoundAddress { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new();
        public decimal RewardPercentage { get; set; }
        public decimal? EstimatedValueEGP { get; set; }
        public decimal? RewardAmountEGP { get; set; }
        public string VerificationQuestion { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ReportedByUserName { get; set; } = string.Empty;
        public Guid ReportedByUserId { get; set; }
        public DateTime FoundAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsClaimed { get; set; }
        public bool HasAttempted { get; set; }   // current user already tried
    }
}
