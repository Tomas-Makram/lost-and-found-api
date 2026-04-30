namespace BusinessLayer.DTOs
{
    public class ChatMessageDTO
    {
        public Guid Id { get; set; }
        public Guid FoundItemId { get; set; }
        public string FoundItemTitle { get; set; } = string.Empty;
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderProfileImg { get; set; } = string.Empty;
        public Guid RecipientId { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsMine { get; set; }
    }
}