using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class ManageUserDTO
    {
        [Required]
        public Guid UserId { get; set; }

        // "Block" | "Unblock"
        [Required]
        [RegularExpression("^(Block|Unblock)$", ErrorMessage = "Action must be Block or Unblock.")]
        public string Action { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Reason { get; set; }
    }
}