namespace OnHouseLocal.Models
{
    public class PropertyFilter
    {
        public string SearchText { get; set; } = string.Empty;
        public string PropertyType { get; set; } = "전체";
        public string TransactionType { get; set; } = "전체";
        public string Status { get; set; } = "전체";
        
        // 보증금 범위 (만원)
        public int MaxDeposit { get; set; } = 0; // 0이면 무제한
        
        // 월세 범위 (만원)
        public int MaxMonthlyRent { get; set; } = 0; // 0이면 무제한

        // 옵션 체크박스
        public bool OnlyWithParking { get; set; }
        public bool OnlyWithElevator { get; set; }
        public bool OnlyPetsAllowed { get; set; }
        public bool ExcludeViolatingBuilding { get; set; }
    }
}
