namespace BusinessLayer.DTOs
{
    public class ConversationDTO
    {
        public Guid FoundItemId { get; set; }
        public string FoundItemTitle { get; set; } = string.Empty;
        public Guid OtherUserId { get; set; }
        public string OtherUserName { get; set; } = string.Empty;
        public string OtherUserProfileImg { get; set; } = string.Empty;
        public ChatMessageDTO? LastMessage { get; set; }
        public int UnreadCount { get; set; }
    }
}