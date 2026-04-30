using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Models
{
    public class ClaimAttempt
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid FoundItemId { get; set; }

        [ForeignKey(nameof(FoundItemId))]
        public FoundItem FoundItem { get; set; } = null!;

        [Required]
        public Guid ClaimantUserId { get; set; }

        [ForeignKey(nameof(ClaimantUserId))]
        public User ClaimantUser { get; set; } = null!;

        [Required]
        [MaxLength(500)]
        public string ProvidedAnswer { get; set; } = string.Empty;

        public bool IsCorrect { get; set; } = false;

        public ClaimAttemptStatus Status { get; set; } = ClaimAttemptStatus.Pending;

        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

        public Guid? ReviewedByAdminId { get; set; }
        public DateTime? ReviewedAt { get; set; }

        [MaxLength(500)]
        public string? AdminNote { get; set; }
    }
}
