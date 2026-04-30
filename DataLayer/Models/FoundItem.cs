using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Models
{
    public class FoundItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ReportedByUserId { get; set; }

        [ForeignKey(nameof(ReportedByUserId))]
        public User ReportedByUser { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(9,6)")]
        public decimal FoundLatitude { get; set; }

        [Required]
        [Column(TypeName = "decimal(9,6)")]
        public decimal FoundLongitude { get; set; }

        [Required]
        [MaxLength(500)]
        public string FoundAddress { get; set; } = string.Empty;

        // JSON array of image URLs  e.g. ["/uploads/found-items/abc.jpg"]
        public string ImagesJson { get; set; } = "[]";

        // e.g. 10 means the finder wants 10% of the item's estimated value
        [Column(TypeName = "decimal(5,2)")]
        public decimal RewardPercentage { get; set; } = 10m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedValueEGP { get; set; }

        // The question only the real owner can answer
        [Required]
        [MaxLength(500)]
        public string VerificationQuestion { get; set; } = string.Empty;

        // bcrypt hash of the correct answer (stored lower-cased, trimmed)
        [Required]
        public string VerificationAnswerHash { get; set; } = string.Empty;

        public FoundItemStatus Status { get; set; } = FoundItemStatus.PendingReview;

        [MaxLength(1000)]
        public string? AdminNote { get; set; }

        public Guid? ReviewedByAdminId { get; set; }

        [ForeignKey(nameof(ReviewedByAdminId))]
        public User? ReviewedByAdmin { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public DateTime FoundAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsClaimed { get; set; } = false;
        public DateTime? ClaimedAt { get; set; }

        public Guid? ClaimedByUserId { get; set; }

        [ForeignKey(nameof(ClaimedByUserId))]
        public User? ClaimedByUser { get; set; }

        public ICollection<ClaimAttempt> ClaimAttempts { get; set; } = new List<ClaimAttempt>();
        public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    }
}
