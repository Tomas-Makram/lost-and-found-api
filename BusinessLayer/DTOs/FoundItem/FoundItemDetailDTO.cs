namespace BusinessLayer.DTOs
{
    public class FoundItemDetailDTO : FoundItemListDTO
    {
        public string? AdminNote { get; set; }
        public string? ReviewedByAdminName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public string? ClaimedByUserName { get; set; }
    }
}