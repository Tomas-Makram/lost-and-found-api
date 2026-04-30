namespace BusinessLayer.DTOs
{
    public class UserListDTO
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public bool Verified { get; set; }
        public bool Blocked { get; set; }
        public bool Login { get; set; }
        public DateTime JoinDate { get; set; }
        public DateTime? LastLogin { get; set; }
        public int TotalItemsReported { get; set; }
        public int TotalClaimAttempts { get; set; }
    }
}