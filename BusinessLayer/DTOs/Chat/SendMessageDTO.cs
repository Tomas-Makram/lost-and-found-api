using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs
{
    public class SendMessageDTO
    {
        [Required]
        public Guid FoundItemId { get; set; }

        [Required]
        public Guid RecipientId { get; set; }

        [Required]
        [MinLength(1)]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
    }
}