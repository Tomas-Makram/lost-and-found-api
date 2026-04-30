using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class AdminReviewClaimDTO
    {
        [Required]
        public Guid ClaimAttemptId { get; set; }

        // "Approve" | "Reject"
        [Required]
        [RegularExpression("^(Approve|Reject)$", ErrorMessage = "Action must be Approve or Reject.")]
        public string Action { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Note { get; set; }
    }
}