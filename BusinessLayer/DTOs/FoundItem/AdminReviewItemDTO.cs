using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class AdminReviewItemDTO
    {
        [Required]
        public Guid FoundItemId { get; set; }

        // "Approve" | "Reject" | "Block"
        [Required]
        [RegularExpression("^(Approve|Reject|Block)$", ErrorMessage = "Action must be Approve, Reject, or Block.")]
        public string Action { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Note { get; set; }
    }
}