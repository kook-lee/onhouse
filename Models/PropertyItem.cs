using System;

namespace OnHouseLocal.Models
{
    public class PropertyItem
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1; // 소유 사용자 ID
        
        // 기본 정보
        public string Title { get; set; } = string.Empty;
        public string PropertyType { get; set; } = "원룸"; // 원룸, 투룸/쓰리룸, 오피스텔, 아파트, 상가/사무실
        public string TransactionType { get; set; } = "월세"; // 월세, 전세, 매매
        
        // 가격 조건 (단위: 만원)
        public int Deposit { get; set; } // 보증금 (전세가)
        public int MonthlyRent { get; set; } // 월세
        public int MaintenanceFee { get; set; } // 관리비
        
        // 위치 및 건물 정보
        public string Address { get; set; } = string.Empty;
        public string DetailAddress { get; set; } = string.Empty;
        public int Floor { get; set; } = 1; // 해당 층
        public int TotalFloor { get; set; } = 5; // 전체 층
        public double AreaM2 { get; set; } // 전용면적 (m2)
        public double AreaPyeong => Math.Round(AreaM2 * 0.3025, 1); // 평수 환산
        
        // 옵션 및 특징
        public bool HasElevator { get; set; }
        public bool HasParking { get; set; }
        public bool AllowsPets { get; set; }
        public bool IsLoanAvailable { get; set; } = true; // 전세대출/보증보험 가능 여부
        public bool IsViolatingBuilding { get; set; } = false; // 위반건축물 여부
        
        // 상태 (신규, 확인중, 생존확인, 이미나감, 거래진행중, 공실, 계약완료)
        public string Status { get; set; } = "신규";
        public DateTime? LastCheckedAt { get; set; }
        public string CheckedBy { get; set; } = string.Empty;
        public string SuspiciousSignal { get; set; } = string.Empty; // 의심 신호 (예: ⚠️ 시세 대비 35% 저렴)
        public string MatchedCustomerInfo { get; set; } = string.Empty; // 매칭된 손님 정보 (예: 🙋 김철수 손님 일치)

        public string SourceChannel { get; set; } = "직접등록"; // 직접등록, 🏠 피터팬, 🥕 당근
        public string ImageUrl { get; set; } = string.Empty; // 사진 미리보기 링크
        
        // 비공개 임대인 정보 (중개사 전용)
        public string OwnerName { get; set; } = string.Empty; // 임대인 성명 / 작성자
        public string OwnerPhone { get; set; } = string.Empty; // 임대인 연락처
        public string SecretMemo { get; set; } = string.Empty; // 호실 비번, 임대인 성향 등
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // 가격 표시 포맷 헬퍼
        public string PriceDisplay
        {
            get
            {
                if (TransactionType == "전세")
                    return $"전세 {Deposit:#,##0}만";
                if (TransactionType == "매매")
                    return $"매매 {Deposit:#,##0}만";

                return MonthlyRent > 0 
                    ? $"{Deposit:#,##0} / {MonthlyRent:#,##0}만" 
                    : $"보증금 {Deposit:#,##0}만";
            }
        }

        // 층수 표시 헬퍼
        public string FloorDisplay => $"{Floor}층 / {TotalFloor}층";

        // 요약 특징 뱃지 리스트
        public string SummaryBadges
        {
            get
            {
                var badges = new System.Collections.Generic.List<string>();
                if (HasElevator) badges.Add("엘베");
                if (HasParking) badges.Add("주차");
                if (AllowsPets) badges.Add("반려동물");
                if (IsLoanAvailable) badges.Add("대출가능");
                if (IsViolatingBuilding) badges.Add("⚠️위반건축물");
                return string.Join(" · ", badges);
            }
        }
    }
}
