namespace BusinessLayer.DTOs
{
    public class AdminDashboardDTO
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int BlockedUsers { get; set; }
        public int TotalItems { get; set; }
        public int PendingReviewItems { get; set; }
        public int PublishedItems { get; set; }
        public int ClaimedItems { get; set; }
        public int RejectedItems { get; set; }
        public int BlockedItems { get; set; }
        public int TotalClaimAttempts { get; set; }
        public int PendingClaims { get; set; }
    }
}
