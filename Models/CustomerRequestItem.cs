using System;

namespace OnHouseLocal.Models
{
    public class CustomerRequestItem
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string PropertyType { get; set; } = "원룸"; // 전체, 원룸, 투룸, 오피스텔, 빌라, 상가
        public string TransactionType { get; set; } = "월세"; // 전체, 월세, 전세, 매매
        public int MinDeposit { get; set; } = 0;
        public int MaxDeposit { get; set; } = 10000;
        public int MaxMonthlyRent { get; set; } = 100;
        public string TargetRegion { get; set; } = "관악구"; // "관악구,동작구"
        public string Memo { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
