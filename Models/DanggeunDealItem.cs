using System;

namespace OnHouseLocal.Models
{
    public class DanggeunDealItem
    {
        public int Id { get; set; }
        public string ArticleId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string PriceDisplay { get; set; } = string.Empty;
        public int Deposit { get; set; }
        public int MonthlyRent { get; set; }
        public string Description { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string AuthorType { get; set; } = "당근 직거래";
        public string ArticleUrl { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty; // 콤마(,) 구분으로 다중 사진 지원
        public DateTime DetectedAt { get; set; } = DateTime.Now;
        public bool IsImported { get; set; } = false;
        public string Status { get; set; } = "신규"; // 신규, 확인중, 생존확인, 이미나감
        public string SuspiciousSignal { get; set; } = string.Empty; // ⚠️ 시세 대비 40% 저렴 등
        public string MatchedCustomerInfo { get; set; } = string.Empty; // 🙋 [손님명] 매칭
    }
}
