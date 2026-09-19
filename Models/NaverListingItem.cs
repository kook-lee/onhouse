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

        // [신규] 건축물대장 정밀 스펙 & VWorld 공동주택 공시가격 & HUG 안심전세 126%
        public double PlatArea { get; set; } // 대지면적 (㎡)
        public double ArchArea { get; set; } // 건축면적 (㎡)
        public double TotArea { get; set; }  // 연면적 (㎡)
        public double BcRat { get; set; }    // 건폐율 (%)
        public double VlRat { get; set; }    // 용적률 (%)
        public double PlatAreaPyung => Math.Round(PlatArea * 0.3025, 1);
        public double TotAreaPyung => Math.Round(TotArea * 0.3025, 1);
        public string BuildingStructure { get; set; } = ""; // 주구조
        public long PublicPrice { get; set; } // 공시가격 (원)
        public string PublicPriceYear { get; set; } = ""; // 공시연도 (예: 2024)
        public long HugGuaranteeLimit { get; set; } // HUG 안심전세 126% 보증보험 한도 (원)
        public string LedgerRawJson { get; set; } = ""; // 건축물대장 표제부 원본 JSON
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
