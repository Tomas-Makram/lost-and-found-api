namespace BusinessLayer.Models
{
    namespace BusinessLayer.Models
    {
        public class ResetPasswordSessionResult
        {
            public Guid UserId { get; set; }
            public string ResetToken { get; set; } = string.Empty;
            public DateTime ExpiresAtUtc { get; set; }
        }
    }
}
