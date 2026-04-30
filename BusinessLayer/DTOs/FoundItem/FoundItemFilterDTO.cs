namespace BusinessLayer.DTOs
{
    public class FoundItemFilterDTO
    {
        public string? Category { get; set; }
        public string? Keyword { get; set; }
        public decimal? NearLatitude { get; set; }
        public decimal? NearLongitude { get; set; }
        public double? RadiusKm { get; set; }
        public string? Status { get; set; }   // Admin filter
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
