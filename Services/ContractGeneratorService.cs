using System.Text;
using OnHouseLocal.Models;

namespace OnHouseLocal.Services
{
    public class ContractGeneratorService
    {
        /// <summary>
        /// 손님 전송용 깔끔한 카카오톡/문자 브리핑 카드 문구 생성
        /// </summary>
        public string GenerateBriefingText(PropertyItem item)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🏠 [실매물 추천 브리핑]");
            sb.AppendLine($"▶ {item.Title}");
            sb.AppendLine($"━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"• 금액: {item.PriceDisplay} (관리비 {item.MaintenanceFee}만)");
            sb.AppendLine($"• 구조: {item.PropertyType} / {item.FloorDisplay}");
            sb.AppendLine($"• 면적: 약 {item.AreaPyeong}평 ({item.AreaM2}㎡)");
            sb.AppendLine($"• 위치: {item.Address} {(string.IsNullOrWhiteSpace(item.DetailAddress) ? "" : $"({item.DetailAddress})")}");
            
            var options = new System.Collections.Generic.List<string>();
            if (item.HasElevator) options.Add("엘리베이터");
            if (item.HasParking) options.Add("주차가능");
            if (item.AllowsPets) options.Add("반려동물협의");
            if (item.IsLoanAvailable) options.Add("대출가능");
            if (item.SourceChannel != "직접등록") options.Add($"출처: {item.SourceChannel}");

            if (options.Count > 0)
            {
                sb.AppendLine($"• 특징: {string.Join(", ", options)}");
            }
            
            sb.AppendLine($"• 상태: {item.Status} (즉시 입주 협의 가능)");
            sb.AppendLine($"━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"📞 문의: 편하게 연락주시면 안내 도와드리겠습니다.");
            return sb.ToString();
        }

        /// <summary>
        /// 부동산 계약서 맞춤 안전 특약 자동 생성
        /// </summary>
        public string GenerateSpecialTerms(PropertyItem item)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[계약서 권장 안전 특약]");
            sb.AppendLine("1. 현 시설물 상태에서의 임대차 계약이며, 잔금일까지 현 상태를 유지하기로 한다.");
            sb.AppendLine("2. 임대인은 임차인의 잔금일 익일까지 등기부상 근저당 및 제한물권을 추가로 설정하지 아니한다. (위반 시 계약 무효 및 계약금 반환)");

            if (item.TransactionType == "전세" || item.IsLoanAvailable)
            {
                sb.AppendLine("3. 본 계약은 임차인의 전세자금대출(또는 보증보험) 실행을 전제로 하며, 임대인이나 주택 하자로 대출 불가 시 계약금을 전액 반환하기로 한다.");
            }

            if (item.AllowsPets)
            {
                sb.AppendLine("4. 반려동물(소형견/묘) 사육을 승낙하며, 퇴거 시 반려동물로 인한 시설물 훼손 및 벽지 오염 시 원상복구(도배비용 배상)하기로 한다.");
            }

            if (item.IsViolatingBuilding)
            {
                sb.AppendLine("5. [주의] 본 건물은 건축물대장상 위반건축물로 등재된 부분이 있음을 사전 고지하고 계약을 체결한다.");
            }

            sb.AppendLine("6. 공과금 및 관리비는 입주일(잔금일) 기준으로 일할 정산하여 인수·인계한다.");
            return sb.ToString();
        }
    }
}
