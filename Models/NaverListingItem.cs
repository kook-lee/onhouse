using System;

namespace OnHouseLocal.Models
{
    public class NaverListingItem
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1;
        public string ArticleNumber { get; set; } = "";
        public string ArticleName { get; set; } = "";
        public string TradeType { get; set; } = ""; // 매매, 전세, 월세
        public string RealEstateType { get; set; } = "";
        public string PriceDisplay { get; set; } = "";
        public string FloorInfo { get; set; } = ""; // 3/4층
        public double AreaM2 { get; set; }
        public string Address { get; set; } = "";
        public bool HasElevator { get; set; }
        public int TotalParking { get; set; }
        public string ApprovalDate { get; set; } = "";
        public string RawJson { get; set; } = "";
        public string LedgerStatus { get; set; } = "Pending"; // "Pending", "Safe", "Warning", "Danger", "Failed"
        public string LedgerMessage { get; set; } = "";
        public string LedgerDiscrepanciesJson { get; set; } = "";
        public DateTime? InspectedAt { get; set; }
        public bool IsImported { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class RealtorSettingsItem
    {
        public int UserId { get; set; } = 1;
        public string RealtorName { get; set; } = "";
        public string AgencyName { get; set; } = "";
        public string RealtorId { get; set; } = "";
        public string NaverId { get; set; } = "";
        public string NaverPassword { get; set; } = "";
        public bool AutoSync { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
