namespace BusinessLayer.DTOs
{
    public class ClaimAttemptDTO
    {
        public Guid Id { get; set; }
        public Guid FoundItemId { get; set; }
        public string FoundItemTitle { get; set; } = string.Empty;
        public Guid ClaimantUserId { get; set; }
        public string ClaimantUserName { get; set; } = string.Empty;
        public string ProvidedAnswer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime AttemptedAt { get; set; }
        public string? AdminNote { get; set; }
    }
}
