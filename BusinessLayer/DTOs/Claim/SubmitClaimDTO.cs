using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class SubmitClaimDTO
    {
        [Required]
        public Guid FoundItemId { get; set; }

        [Required]
        [MinLength(1)]
        [MaxLength(500)]
        public string Answer { get; set; } = string.Empty;
    }
}