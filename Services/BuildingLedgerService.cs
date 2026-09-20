using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OnHouseLocal.Models;

namespace OnHouseLocal.Services
{
    public class BuildingLedgerInfo
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public string PlatAddress { get; set; } = string.Empty;
        public string NewPlatAddress { get; set; } = string.Empty;
        public string DongName { get; set; } = string.Empty;
        public string RegstrGbCdNm { get; set; } = string.Empty; // 일반 / 집합
        public string MainPurps { get; set; } = string.Empty;
        public string EtcPurps { get; set; } = string.Empty;
        public string SigunguCd { get; set; } = string.Empty;
        public string BjdongCd { get; set; } = string.Empty;
        public string Bun { get; set; } = string.Empty;
        public string Ji { get; set; } = string.Empty;

        // 면적 및 규모 (대지면적, 연면적, 건축면적, 건폐율, 용적률)
        public double PlatArea { get; set; } // 대지면적 (㎡)
        public double ArchArea { get; set; } // 건축면적 (㎡)
        public double TotArea { get; set; }  // 연면적 (㎡)
        public double BcRat { get; set; }    // 건폐율 (%)
        public double VlRat { get; set; }    // 용적률 (%)
        public double PlatAreaPyung => Math.Round(PlatArea * 0.3025, 1);
        public double ArchAreaPyung => Math.Round(ArchArea * 0.3025, 1);
        public double TotAreaPyung => Math.Round(TotArea * 0.3025, 1);

        // 구조 및 높이
        public string Structure { get; set; } = string.Empty; // 주구조
        public string Roof { get; set; } = string.Empty;      // 지붕
        public double Height { get; set; }                    // 높이 (m)
        public int GrndFlrCnt { get; set; } // 지상 층수
        public int UgrndFlrCnt { get; set; } // 지하 층수
        public int HouseholdCount { get; set; } // 세대수
        public int FamilyCount { get; set; }    // 가구수
        public int HoCount { get; set; }        // 호수

        // 승강기 및 주차
        public int RideUseElvtCnt { get; set; } // 승용 승강기
        public int EmgenUseElvtCnt { get; set; } // 비상 승강기
        public int TotalElevatorCount => RideUseElvtCnt + EmgenUseElvtCnt;
        public bool HasElevator => TotalElevatorCount > 0;
        public int TotalParking { get; set; }
        public int IndrAutoUtcnt { get; set; }
        public int OudrAutoUtcnt { get; set; }
        public int IndrMechUtcnt { get; set; }
        public int OudrMechUtcnt { get; set; }

        // 사용승인일 및 위반건축물
        public string PmsDay { get; set; } = string.Empty;        // 허가일
        public string StcnsDay { get; set; } = string.Empty;      // 착공일
        public string UseApprovalDate { get; set; } = string.Empty; // 사용승인일
        public bool IsViolatingBuilding { get; set; } // 위반건축물 여부
        public bool IsSuspiciousViolation { get; set; } // 위반 의심 여부 (판넬/가설구조 증축 등)
        public string ViolationSuspicionReason { get; set; } = string.Empty; // 의심 사유
        public bool HasOfficialViolationData { get; set; } // API에 위반건축물 공식 필드가 제공되었는지 여부

        // 호별 전유/공용 및 대지권 상세 정보
        public string TargetHo { get; set; } = string.Empty;
        public string TargetDong { get; set; } = string.Empty;
        public double UnitExclusiveArea { get; set; } // 전용(전유)면적 (㎡)
        public double UnitCommonArea { get; set; }    // 공용면적 (㎡)
        public double UnitContractArea => Math.Round(UnitExclusiveArea + UnitCommonArea, 2); // 총 계약면적 (㎡)
        public double UnitLandShareArea { get; set; } // 해당 호수 대지권 지분 면적 (㎡)
        public string UnitLandShareRatio { get; set; } = string.Empty; // 대지권 비율
        public double UnitExclusiveAreaPyung => Math.Round(UnitExclusiveArea * 0.3025, 1);
        public double UnitCommonAreaPyung => Math.Round(UnitCommonArea * 0.3025, 1);
        public double UnitContractAreaPyung => Math.Round(UnitContractArea * 0.3025, 1);
        public double UnitLandShareAreaPyung => Math.Round(UnitLandShareArea * 0.3025, 1);
        public List<ExposPubuseAreaItem> ExposPubuseList { get; set; } = new();

        // 원본 전산 데이터
        public string RawJson { get; set; } = string.Empty;
    }

    public class ExposPubuseAreaItem
    {
        public string ExposPubuseGbCdNm { get; set; } = string.Empty; // 전유, 공용
        public string MainPurpsCdNm { get; set; } = string.Empty;     // 아파트, 제1종근린생활시설 등
        public string EtcPurps { get; set; } = string.Empty;          // 아파트(도시형생활주택), 계단실, 복도 등
        public double Area { get; set; }                              // 면적 (㎡)
        public double AreaPyung => Math.Round(Area * 0.3025, 1);
        public string DongNm { get; set; } = string.Empty;
        public string HoNm { get; set; } = string.Empty;
        public string FlrNoNm { get; set; } = string.Empty;
    }

    public class SafetyIssueItem
    {
        public string Field { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Danger, Warning, Info
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? FixAction { get; set; }
        public object? FixValue { get; set; }
    }

    public class SafetyAuditResult
    {
        public bool IsSafe => Issues.Count == 0;
        public string StatusLevel => Issues.Any(i => i.Type == "Danger") ? "Danger" : (Issues.Any(i => i.Type == "Warning") ? "Warning" : "Safe");
        public List<SafetyIssueItem> Issues { get; set; } = new();
        public BuildingLedgerInfo? Ledger { get; set; }
    }

    public class BuildingLedgerService
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(7) };
        private const string ServiceKey = "eQw9WYxqNsJrhAqVJR7FVYNBiE66u0qD6p6RS7Zk%2F3OJJ%2B6q44VHlTn2u2hpYl52nFQeGpiYPHNsDQc9t9P0OQ%3D%3D";

        // 서울 전 자치구 시군구코드 (5자리)
        public static readonly Dictionary<string, string> SigunguMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "강남구", "11680" }, { "강동구", "11740" }, { "강북구", "11305" }, { "강서구", "11500" },
            { "관악구", "11620" }, { "광진구", "11215" }, { "구로구", "11530" }, { "금천구", "11545" },
            { "노원구", "11350" }, { "도봉구", "11320" }, { "동대문구", "11230" }, { "동작구", "11590" },
            { "마포구", "11440" }, { "서대문구", "11410" }, { "서초구", "11650" }, { "성동구", "11200" },
            { "성북구", "11290" }, { "송파구", "11710" }, { "양천구", "11470" }, { "영등포구", "11560" },
            { "용산구", "11170" }, { "은평구", "11380" }, { "종로구", "11110" }, { "중구", "11140" }, { "중랑구", "11260" }
        };

        // 서울 전역 주요 법정동코드 매핑 (5자리)
        public static readonly Dictionary<string, (string sigunguCd, string bjdongCd)> DongMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // 중랑구 (11260)
            { "묵동", ("11260", "10300") }, { "면목동", ("11260", "10100") }, { "상봉동", ("11260", "10200") },
            { "망우동", ("11260", "10400") }, { "중화동", ("11260", "10500") }, { "신내동", ("11260", "10600") },

            // 양천구 (11470)
            { "목동", ("11470", "10200") }, { "신월동", ("11470", "10100") }, { "신정동", ("11470", "10300") },

            // 강서구 (11500)
            { "화곡동", ("11500", "10300") }, { "등촌동", ("11500", "10200") }, { "가양동", ("11500", "10400") },
            { "마곡동", ("11500", "10500") }, { "방화동", ("11500", "10700") }, { "염창동", ("11500", "10100") }, { "공항동", ("11500", "10800") },

            // 노원구 (11350)
            { "상계동", ("11350", "10500") }, { "중계동", ("11350", "10400") }, { "하계동", ("11350", "10300") }, { "공릉동", ("11350", "10200") }, { "월계동", ("11350", "10100") },

            // 관악구 (11620)
            { "신림동", ("11620", "10200") }, { "봉천동", ("11620", "10100") }, { "남현동", ("11620", "10300") },

            // 강남구 (11680)
            { "역삼동", ("11680", "10100") }, { "개포동", ("11680", "10300") }, { "청담동", ("11680", "10400") },
            { "삼성동", ("11680", "10500") }, { "대치동", ("11680", "10600") }, { "신사동", ("11680", "10700") },
            { "논현동", ("11680", "10800") }, { "압구정동", ("11680", "10900") }, { "도곡동", ("11680", "11800") },
            { "세곡동", ("11680", "11100") }, { "자곡동", ("11680", "11200") }, { "수서동", ("11680", "11500") }, { "일원동", ("11680", "11400") },

            // 서초구 (11650)
            { "서초동", ("11650", "10800") }, { "반포동", ("11650", "10700") }, { "방배동", ("11650", "10100") }, { "양재동", ("11650", "10200") }, { "잠원동", ("11650", "10600") },

            // 송파구 (11710)
            { "잠실동", ("11710", "10100") }, { "방이동", ("11710", "11100") }, { "송파동", ("11710", "10400") }, { "문정동", ("11710", "10800") },
            { "석촌동", ("11710", "10500") }, { "삼전동", ("11710", "10600") }, { "가락동", ("11710", "10700") }, { "풍납동", ("11710", "10300") },

            // 마포구 (11440)
            { "서교동", ("11440", "12000") }, { "연남동", ("11440", "12400") }, { "합정동", ("11440", "12200") },
            { "망원동", ("11440", "12300") }, { "동교동", ("11440", "12100") }, { "상수동", ("11440", "11500") },
            { "공덕동", ("11440", "10200") }, { "아현동", ("11440", "10100") }, { "도화동", ("11440", "10400") }, { "성산동", ("11440", "12500") }, { "상암동", ("11440", "12700") },

            // 동작구 (11590)
            { "상도동", ("11590", "10200") }, { "사당동", ("11590", "10700") }, { "흑석동", ("11590", "10500") }, { "대방동", ("11590", "10800") }, { "노량진동", ("11590", "10100") }, { "신대방동", ("11590", "10900") },

            // 영등포구 (11560)
            { "영등포동", ("11560", "10100") }, { "당산동", ("11560", "11600") }, { "문래동", ("11560", "12100") }, { "여의도동", ("11560", "11000") }, { "대림동", ("11560", "13200") }, { "신길동", ("11560", "13100") }, { "양평동", ("11560", "12500") },

            // 광진구 (11215)
            { "화양동", ("11215", "10700") }, { "자양동", ("11215", "10500") }, { "군자동", ("11215", "10900") }, { "중곡동", ("11215", "10100") }, { "구의동", ("11215", "10300") }, { "광장동", ("11215", "10400") },

            // 성동구 (11200)
            { "성수동", ("11200", "11400") }, { "행당동", ("11200", "10700") }, { "왕십리동", ("11200", "10100") }, { "금호동", ("11200", "10900") }, { "옥수동", ("11200", "11300") }, { "마장동", ("11200", "10500") },

            // 용산구 (11170)
            { "이태원동", ("11170", "13000") }, { "한남동", ("11170", "13100") }, { "후암동", ("11170", "10100") }, { "청파동", ("11170", "10700") }, { "이촌동", ("11170", "12800") }, { "한강로", ("11170", "12400") },

            // 서대문구 (11410)
            { "신촌동", ("11410", "11400") }, { "연희동", ("11410", "11900") }, { "창천동", ("11410", "11300") }, { "남가좌동", ("11410", "12000") }, { "북가좌동", ("11410", "12100") }, { "홍제동", ("11410", "11500") }, { "홍은동", ("11410", "11800") },

            // 은평구 (11380)
            { "갈현동", ("11380", "10400") }, { "불광동", ("11380", "10300") }, { "응암동", ("11380", "10700") }, { "대조동", ("11380", "10600") }, { "역촌동", ("11380", "10800") }, { "녹번동", ("11380", "10200") },

            // 강동구 (11740)
            { "천호동", ("11740", "10900") }, { "길동", ("11740", "10500") }, { "암사동", ("11740", "10700") }, { "성내동", ("11740", "10800") }, { "명일동", ("11740", "10400") }, { "고덕동", ("11740", "10200") },

            // 구로구 (11530)
            { "구로동", ("11530", "10200") }, { "신도림동", ("11530", "10100") }, { "개봉동", ("11530", "10700") }, { "고척동", ("11530", "10600") }, { "오류동", ("11530", "10800") },

            // 금천구 (11545)
            { "가산동", ("11545", "10100") }, { "독산동", ("11545", "10200") }, { "시흥동", ("11545", "10300") },

            // 동대문구 (11230)
            { "장안동", ("11230", "10600") }, { "전농동", ("11230", "10200") }, { "답십리동", ("11230", "10300") }, { "이문동", ("11230", "10800") }, { "휘경동", ("11230", "10700") }, { "제기동", ("11230", "10400") },

            // 성북구 (11290)
            { "길음동", ("11290", "13400") }, { "돈암동", ("11290", "10300") }, { "정릉동", ("11290", "13300") }, { "장위동", ("11290", "13600") }, { "석관동", ("11290", "13800") },

            // 도봉구 (11320)
            { "쌍문동", ("11320", "10100") }, { "방학동", ("11320", "10200") }, { "창동", ("11320", "10300") }, { "도봉동", ("11320", "10400") },

            // 강북구 (11305)
            { "수유동", ("11305", "10100") }, { "미아동", ("11305", "10300") }, { "번동", ("11305", "10200") }
        };

        public record ResolvedAddressCodes(
            string SigunguCd, 
            string BjdongCd, 
            string Bun, 
            string Ji, 
            string PlatGbCd, 
            string PlatAddress, 
            string RoadAddress, 
            string BuildingName);

        /// <summary>
        /// 국토교통부 VWorld Geocoder/주소검색 API를 활용하여 전국(서울/경기/인천/지방 전역) 지번/도로명 주소를 19자리 표준 PNU 및 법정동코드로 정밀 변환
        /// </summary>
        public static async Task<ResolvedAddressCodes?> ResolveAddressWithVWorldAsync(string rawAddress)
        {
            if (string.IsNullOrWhiteSpace(rawAddress)) return null;

            // 동/호수 등 군더더기 제거하여 기본 주소 정제 (예: "경기 성남시 수정구 창곡동 504 3210동 604호" -> "경기 성남시 수정구 창곡동 504")
            string cleanAddr = Regex.Replace(rawAddress, @"\s*\d{1,4}\s*동\s*", " ").Trim();
            cleanAddr = Regex.Replace(cleanAddr, @"\s*\d{1,4}\s*호\s*", " ").Trim();
            cleanAddr = Regex.Replace(cleanAddr, @"\s{2,}", " ").Trim();

            const string vworldKey = "084E3A8F-CACB-426A-BC5A-24920CF543D6";
            string[] categories = new[] { "parcel", "road" };

            foreach (var cat in categories)
            {
                try
                {
                    string url = $"https://api.vworld.kr/req/search?service=search&request=search&version=2.0&crs=EPSG:4326&size=5&page=1&query={Uri.EscapeDataString(cleanAddr)}&type=address&category={cat}&format=json&key={vworldKey}";
                    var resp = await _httpClient.GetAsync(url);
                    if (!resp.IsSuccessStatusCode) continue;

                    string json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("response", out var rProp)) continue;
                    string status = rProp.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "" : "";
                    if (status != "OK") continue;

                    if (rProp.TryGetProperty("result", out var resProp) &&
                        resProp.TryGetProperty("items", out var items) &&
                        items.ValueKind == JsonValueKind.Array &&
                        items.GetArrayLength() > 0)
                    {
                        var first = items[0];
                        string pnu = first.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                        if (pnu.Length >= 19)
                        {
                            string sigunguCd = pnu.Substring(0, 5);
                            string bjdongCd = pnu.Substring(5, 5);
                            string platGb = pnu.Substring(10, 1) == "2" ? "1" : "0";
                            string bun = pnu.Substring(11, 4);
                            string ji = pnu.Substring(15, 4);

                            string parcel = "";
                            string road = "";
                            string bldNm = "";
                            if (first.TryGetProperty("address", out var aProp))
                            {
                                parcel = aProp.TryGetProperty("parcel", out var pProp) ? pProp.GetString() ?? "" : "";
                                road = aProp.TryGetProperty("road", out var rdProp) ? rdProp.GetString() ?? "" : "";
                                bldNm = aProp.TryGetProperty("bldnm", out var bnProp) ? bnProp.GetString() ?? "" : "";
                            }

                            return new ResolvedAddressCodes(sigunguCd, bjdongCd, bun, ji, platGb, parcel, road, bldNm);
                        }
                    }
                }
                catch
                {
                    // ignore and fallback
                }
            }

            return null;
        }

        /// <summary>
        /// 시군구코드(5), 법정동코드(5), 번지로부터 19자리 표준 PNU(필지고유번호)를 조합
        /// </summary>
        public static string BuildPnu(string sigunguCd, string bjdongCd, string bun, string ji, string platGb = "0")
        {
            string platDigit = (platGb == "1" || platGb == "2") ? "2" : "1";
            string b = (string.IsNullOrEmpty(bun) ? "0" : bun).PadLeft(4, '0');
            string j = (string.IsNullOrEmpty(ji) ? "0" : ji).PadLeft(4, '0');
            return $"{sigunguCd}{bjdongCd}{platDigit}{b}{j}";
        }

        /// <summary>
        /// 주소를 분석하여 국토교통부 건축HUB 표제부 API 호출 (전국 주소 실시간 지원 및 단지 내 동 매칭)
        /// </summary>
        public async Task<BuildingLedgerInfo> QueryBuildingLedgerAsync(string address, string targetDong = "")
        {
            var info = new BuildingLedgerInfo();
            if (string.IsNullOrWhiteSpace(address))
            {
                info.Message = "주소가 비어 있습니다.";
                return info;
            }

            try
            {
                // 1. 전국 지번/도로명 주소를 VWorld 실시간 API로 정밀 법정동/번지 식별
                var resolved = await ResolveAddressWithVWorldAsync(address);
                if (resolved != null)
                {
                    var resLedger = await QueryBuildingLedgerByCodesAsync(
                        resolved.SigunguCd, 
                        resolved.BjdongCd, 
                        resolved.Bun, 
                        resolved.Ji, 
                        targetDong, 
                        resolved.PlatGbCd);

                    if (resLedger.Success)
                    {
                        if (string.IsNullOrWhiteSpace(resLedger.BuildingName) && !string.IsNullOrWhiteSpace(resolved.BuildingName))
                            resLedger.BuildingName = resolved.BuildingName;
                        if (string.IsNullOrWhiteSpace(resLedger.PlatAddress) && !string.IsNullOrWhiteSpace(resolved.PlatAddress))
                            resLedger.PlatAddress = resolved.PlatAddress;
                        if (string.IsNullOrWhiteSpace(resLedger.NewPlatAddress) && !string.IsNullOrWhiteSpace(resolved.RoadAddress))
                            resLedger.NewPlatAddress = resolved.RoadAddress;

                        return resLedger;
                    }
                }

                // 2. 오프라인 사전 매핑 (서울시 자치구 및 주요 동) 폴백
                string sigunguCd = "11620"; // 기본 관악구
                string bjdongCd = "10200";  // 기본 신림동
                string bun = "0000";
                string ji = "0000";

                // 구 매칭
                foreach (var pair in SigunguMap)
                {
                    if (address.Contains(pair.Key))
                    {
                        sigunguCd = pair.Value;
                        break;
                    }
                }

                // 동 매칭
                foreach (var pair in DongMap)
                {
                    if (address.Contains(pair.Key))
                    {
                        sigunguCd = pair.Value.sigunguCd;
                        bjdongCd = pair.Value.bjdongCd;
                        break;
                    }
                }

                // 번지 추출 (예: 1432-15 또는 737)
                bool hasLot = false;
                var lotMatch = Regex.Match(address, @"(\d{1,4})(?:-(\d{1,4}))?");
                if (lotMatch.Success)
                {
                    bun = lotMatch.Groups[1].Value.PadLeft(4, '0');
                    if (lotMatch.Groups[2].Success && !string.IsNullOrEmpty(lotMatch.Groups[2].Value))
                    {
                        ji = lotMatch.Groups[2].Value.PadLeft(4, '0');
                    }
                    hasLot = true;
                }

                if (!hasLot || bun == "0000")
                {
                    info.Success = false;
                    info.Message = $"'{address}'에는 건물 번지수(예: 묵동 164-32)가 없습니다. 주소 옆 [🔍 주소 검색] 버튼으로 번지를 선택해주세요.";
                    return info;
                }

                return await QueryBuildingLedgerByCodesAsync(sigunguCd, bjdongCd, bun, ji, targetDong, "0");
            }
            catch (Exception ex)
            {
                info.Success = false;
                info.Message = $"건축물대장 조회 중 일시적 오류: {ex.Message}";
                return info;
            }
        }

        /// <summary>
        /// 시군구코드, 법정동코드, 번, 지를 직접 전달받아 건축물대장을 조회 (단지 내 동 지정 및 총괄표제부 결합 지원)
        /// </summary>
        public async Task<BuildingLedgerInfo> QueryBuildingLedgerByCodesAsync(
            string sigunguCd, 
            string bjdongCd, 
            string bun, 
            string ji, 
            string targetDong = "", 
            string platGbCd = "0")
        {
            var info = new BuildingLedgerInfo();
            try
            {
                if (string.IsNullOrEmpty(bun)) bun = "0000";
                if (string.IsNullOrEmpty(ji)) ji = "0000";
                bun = bun.PadLeft(4, '0');
                ji = ji.PadLeft(4, '0');
                string pGb = string.IsNullOrEmpty(platGbCd) ? "0" : platGbCd;

                string url = $"https://apis.data.go.kr/1613000/BldRgstHubService/getBrTitleInfo?serviceKey={ServiceKey}&sigunguCd={sigunguCd}&bjdongCd={bjdongCd}&platGbCd={pGb}&bun={bun}&ji={ji}&numOfRows=100&pageNo=1&_type=json";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    info.Message = $"건축HUB 응답 오류 (HTTP {response.StatusCode})";
                    return info;
                }

                string jsonStr = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);

                var root = doc.RootElement;
                if (!root.TryGetProperty("response", out var respObj))
                {
                    info.Message = "건축HUB 데이터 구조 오류";
                    return info;
                }

                var header = respObj.GetProperty("header");
                string resultCode = header.GetProperty("resultCode").GetString() ?? "";
                if (resultCode != "00")
                {
                    info.Message = header.GetProperty("resultMsg").GetString() ?? "조회 실패";
                    return info;
                }

                var body = respObj.GetProperty("body");
                int totalCount = 0;
                if (body.TryGetProperty("totalCount", out var tcProp))
                {
                    if (tcProp.ValueKind == JsonValueKind.Number) totalCount = tcProp.GetInt32();
                    else if (tcProp.ValueKind == JsonValueKind.String && int.TryParse(tcProp.GetString(), out int tc)) totalCount = tc;
                }

                if (totalCount == 0)
                {
                    info.Message = "건축물대장 상 건물이 조회되지 않았습니다. (지번 또는 도로명 확인 필요)";
                    return info;
                }

                var items = body.GetProperty("items").GetProperty("item");
                JsonElement bld;
                if (items.ValueKind == JsonValueKind.Array)
                {
                    bld = items[0];
                    // 여러 동이 있는 대단지인 경우, 지정된 동(targetDong)과 일치하는 동 표제부 우선 선택
                    if (!string.IsNullOrWhiteSpace(targetDong))
                    {
                        string cleanDong = targetDong.Trim();
                        string dongDigits = Regex.Replace(cleanDong, @"[^\d]", "");
                        bool matched = false;

                        foreach (var it in items.EnumerateArray())
                        {
                            string dNm = GetJsonString(it, "dongNm");
                            if (dNm.Equals(cleanDong, StringComparison.OrdinalIgnoreCase) ||
                                dNm.Equals(cleanDong + "동", StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(dongDigits) && (dNm.Contains(dongDigits) || dNm == dongDigits + "동")))
                            {
                                bld = it;
                                matched = true;
                                break;
                            }
                        }

                        // 완벽 일치가 없어도 주거용 주건축물을 우선 배정
                        if (!matched)
                        {
                            foreach (var it in items.EnumerateArray())
                            {
                                string purp = GetJsonString(it, "mainPurpsCdNm");
                                string mainAtch = GetJsonString(it, "mainAtchGbCd");
                                if (mainAtch == "0" && (purp.Contains("공동주택") || purp.Contains("아파트") || purp.Contains("다세대") || purp.Contains("근린생활")))
                                {
                                    bld = it;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    bld = items;
                }

                PopulateBuildingLedgerInfo(info, bld);

                // 동별 표제부에 대지면적/건폐율/용적률이 0인 대단지 아파트인 경우, 총괄표제부 정보로 지표 보완
                if (info.PlatArea <= 0 || info.BcRat <= 0)
                {
                    await SupplementWithRecapTitleInfoAsync(info, sigunguCd, bjdongCd, bun, ji, pGb);
                }
            }
            catch (Exception ex)
            {
                info.Success = false;
                info.Message = $"건축물대장 조회 중 오류: {ex.Message}";
            }

            return info;
        }

        /// <summary>
        /// 대단지 아파트 등 동별 표제부의 대지면적이 0인 경우, 국토교통부 총괄표제부를 조회하여 단지 전체 대지면적/건폐율/용적률/주차대수를 보완
        /// </summary>
        private static async Task SupplementWithRecapTitleInfoAsync(
            BuildingLedgerInfo info, 
            string sigunguCd, 
            string bjdongCd, 
            string bun, 
            string ji, 
            string platGbCd = "0")
        {
            try
            {
                string url = $"https://apis.data.go.kr/1613000/BldRgstHubService/getBrRecapTitleInfo?serviceKey={ServiceKey}&sigunguCd={sigunguCd}&bjdongCd={bjdongCd}&platGbCd={platGbCd}&bun={bun}&ji={ji}&_type=json";
                var resp = await _httpClient.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return;

                string json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("response", out var rProp)) return;
                if (!rProp.TryGetProperty("body", out var body)) return;
                if (!body.TryGetProperty("items", out var itemsObj)) return;
                if (!itemsObj.TryGetProperty("item", out var items)) return;

                JsonElement recap = items.ValueKind == JsonValueKind.Array ? items[0] : items;
                double rPlatArea = GetJsonDouble(recap, "platArea");
                double rBcRat = GetJsonDouble(recap, "bcRat");
                double rVlRat = GetJsonDouble(recap, "vlRat");
                int rParking = GetJsonInt(recap, "totPkngCnt");
                int rHhld = GetJsonInt(recap, "hhldCnt");

                if (info.PlatArea <= 0 && rPlatArea > 0) info.PlatArea = rPlatArea;
                if (info.BcRat <= 0 && rBcRat > 0) info.BcRat = rBcRat;
                if (info.VlRat <= 0 && rVlRat > 0) info.VlRat = rVlRat;
                if (info.TotalParking <= 0 && rParking > 0) info.TotalParking = rParking;
                if (info.HouseholdCount <= 0 && rHhld > 0) info.HouseholdCount = rHhld;
            }
            catch
            {
                // 보완 실패 시 기본값 유지
            }
        }

        /// <summary>
        /// 매물 입력 정보의 허위광고/과태료 위험을 종합 진단
        /// </summary>
        public SafetyAuditResult AuditPropertySafety(PropertyItem prop, BuildingLedgerInfo? ledger = null)
        {
            var result = new SafetyAuditResult { Ledger = ledger };
            string fullText = $"{prop.Title} {prop.DetailAddress} {prop.SecretMemo}".ToLower();

            // 1. 호수 기반 층수 오기 검증 (예: 302호인데 2층 입력)
            var hoMatch = Regex.Match(prop.DetailAddress, @"([1-9]\d{2,3})\s*호?");
            if (hoMatch.Success && int.TryParse(hoMatch.Groups[1].Value, out int hoNum))
            {
                int expectedFloor = hoNum / 100;
                if (expectedFloor > 0 && expectedFloor <= 50)
                {
                    if (prop.Floor != expectedFloor)
                    {
                        result.Issues.Add(new SafetyIssueItem
                        {
                            Field = "floor",
                            Type = "Danger",
                            Title = "🚨 층수 불일치 (허위매물 과태료 주의)",
                            Message = $"상세주소는 '{hoNum}호'인데 해당 층은 '{prop.Floor}층'으로 입력되었습니다. (예상 층수: {expectedFloor}층)",
                            FixAction = "set_floor",
                            FixValue = expectedFloor
                        });
                    }
                }
            }

            // 반지하/지하 호수 검증
            if ((prop.DetailAddress.Contains("b0") || prop.DetailAddress.Contains("지하") || prop.DetailAddress.Contains("반지하")) && prop.Floor > 0)
            {
                result.Issues.Add(new SafetyIssueItem
                {
                    Field = "floor",
                    Type = "Danger",
                    Title = "🚨 반지하/지하 층수 오기",
                    Message = $"호실명에 '지하/반지하'가 포함되어 있으나 해당 층이 지상({prop.Floor}층)으로 설정되어 있습니다.",
                    FixAction = "set_floor",
                    FixValue = -1
                });
            }

            // 2. 층수 논리 모순 검증 (해당 층 > 전체 층)
            if (prop.Floor > prop.TotalFloor && prop.Floor > 0)
            {
                result.Issues.Add(new SafetyIssueItem
                {
                    Field = "floor",
                    Type = "Danger",
                    Title = "❌ 층수 논리 모순 차단",
                    Message = $"해당 층({prop.Floor}층)이 전체 층({prop.TotalFloor}층)보다 높습니다. 필수 수정해야 합니다.",
                    FixAction = "set_total_floor",
                    FixValue = prop.Floor
                });
            }

            // 3. 본문 텍스트 ↔ 옵션 체크박스 불일치 검증 (현직 중개사 과태료 1위 원인)
            // (1) 엘리베이터
            bool mentionsElevator = fullText.Contains("엘베") || fullText.Contains("엘리베이터") || fullText.Contains("승강기") || fullText.Contains("ev");
            bool mentionsNoElevator = fullText.Contains("엘베 없음") || fullText.Contains("엘베없음") || fullText.Contains("엘베x") || fullText.Contains("계단이용");

            if (mentionsElevator && !mentionsNoElevator && !prop.HasElevator)
            {
                result.Issues.Add(new SafetyIssueItem
                {
                    Field = "hasElevator",
                    Type = "Warning",
                    Title = "⚠️ 엘리베이터 누락 주의",
                    Message = "매물명이나 메모에 '엘리베이터/엘베'가 언급되었으나 옵션에 '엘베 없음'으로 체크되어 있습니다.",
                    FixAction = "set_elevator",
                    FixValue = true
                });
            }
            else if (mentionsNoElevator && prop.HasElevator)
            {
                result.Issues.Add(new SafetyIssueItem
                {
                    Field = "hasElevator",
                    Type = "Danger",
                    Title = "🚨 엘리베이터 오기 (허위광고)",
                    Message = "본문에는 '엘베 없음/계단'이라 적혀 있으나 옵션에는 '엘리베이터 있음'으로 체크되어 있습니다.",
                    FixAction = "set_elevator",
                    FixValue = false
                });
            }

            // (2) 주차
            bool mentionsNoParking = fullText.Contains("주차 불가") || fullText.Contains("주차불가") || fullText.Contains("주차x");
            if (mentionsNoParking && prop.HasParking)
            {
                result.Issues.Add(new SafetyIssueItem
                {
                    Field = "hasParking",
                    Type = "Warning",
                    Title = "⚠️ 주차 정보 불일치",
                    Message = "본문에 '주차 불가'로 적혀 있으나 옵션에 '주차 가능'으로 체크되어 있습니다.",
                    FixAction = "set_parking",
                    FixValue = false
                });
            }

            // 4. 건축물대장 실시간 데이터와 교차 검증 (조회된 대장이 있을 경우)
            if (ledger != null && ledger.Success)
            {
                // (1) 대장 승강기 대수 대조
                if (ledger.HasElevator && !prop.HasElevator)
                {
                    result.Issues.Add(new SafetyIssueItem
                    {
                        Field = "hasElevator",
                        Type = "Warning",
                        Title = "🏛️ 건축물대장 불일치 (승강기)",
                        Message = $"건축물대장상 승강기({ledger.TotalElevatorCount}대)가 완비된 건물이나 '엘리베이터 없음'으로 체크되었습니다.",
                        FixAction = "set_elevator",
                        FixValue = true
                    });
                }
                else if (!ledger.HasElevator && prop.HasElevator)
                {
                    result.Issues.Add(new SafetyIssueItem
                    {
                        Field = "hasElevator",
                        Type = "Danger",
                        Title = "🚨 건축물대장 위반 (허위 승강기)",
                        Message = "건축물대장상 승강기가 없는 건물인데 '엘리베이터 있음'으로 체크되었습니다. 허위광고로 과태료 대상입니다.",
                        FixAction = "set_elevator",
                        FixValue = false
                    });
                }

                // (2) 대장 지상 층수 대조
                if (ledger.GrndFlrCnt > 0 && prop.TotalFloor != ledger.GrndFlrCnt)
                {
                    result.Issues.Add(new SafetyIssueItem
                    {
                        Field = "totalFloor",
                        Type = "Warning",
                        Title = "🏛️ 대장 총 층수 불일치",
                        Message = $"건축물대장상 지상 총 층수는 {ledger.GrndFlrCnt}층입니다. (현재 입력값: {prop.TotalFloor}층)",
                        FixAction = "set_total_floor",
                        FixValue = ledger.GrndFlrCnt
                    });
                }

                // (3) 대장 위반건축물 여부 대조
                if (ledger.IsViolatingBuilding && !prop.IsViolatingBuilding)
                {
                    result.Issues.Add(new SafetyIssueItem
                    {
                        Field = "isViolatingBuilding",
                        Type = "Danger",
                        Title = "🚨 위반건축물 미고지 (치명적 과태료)",
                        Message = "건축물대장상 '위반건축물'로 등재되어 있으나 체크되지 않았습니다! 미고지 시 행정처분 및 과태료 대상입니다.",
                        FixAction = "set_violating",
                        FixValue = true
                    });
                }
            }

            return result;
        }

        private static string GetJsonString(JsonElement element, string propName)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                return prop.GetString()?.Trim() ?? "";
            }
            return "";
        }

        private static int GetJsonInt(JsonElement element, string propName)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number) return prop.GetInt32();
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out int v)) return v;
            }
            return 0;
        }

        private static double GetJsonDouble(JsonElement element, string propName)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number) return prop.GetDouble();
                if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out double v)) return v;
            }
            return 0;
        }

        private static long GetJsonLong(JsonElement element, string propName)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number) return prop.GetInt64();
                if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out long v)) return v;
            }
            return 0;
        }

        private static void PopulateBuildingLedgerInfo(BuildingLedgerInfo info, JsonElement bld)
        {
            info.Success = true;
            info.BuildingName = GetJsonString(bld, "bldNm");
            info.PlatAddress = GetJsonString(bld, "platPlc");
            info.NewPlatAddress = GetJsonString(bld, "newPlatPlc");
            info.DongName = GetJsonString(bld, "dongNm");
            info.RegstrGbCdNm = GetJsonString(bld, "regstrGbCdNm");
            info.MainPurps = GetJsonString(bld, "mainPurpsCdNm");
            info.EtcPurps = GetJsonString(bld, "etcPurps");
            info.SigunguCd = GetJsonString(bld, "sigunguCd");
            info.BjdongCd = GetJsonString(bld, "bjdongCd");
            info.Bun = GetJsonString(bld, "bun");
            info.Ji = GetJsonString(bld, "ji");

            info.PlatArea = GetJsonDouble(bld, "platArea");
            info.ArchArea = GetJsonDouble(bld, "archArea");
            info.TotArea = GetJsonDouble(bld, "totArea");
            info.BcRat = GetJsonDouble(bld, "bcRat");
            info.VlRat = GetJsonDouble(bld, "vlRat");

            string strct = GetJsonString(bld, "strctCdNm");
            if (string.IsNullOrEmpty(strct)) strct = GetJsonString(bld, "etcStrct");
            info.Structure = strct;

            string roof = GetJsonString(bld, "roofCdNm");
            if (string.IsNullOrEmpty(roof)) roof = GetJsonString(bld, "etcRoof");
            info.Roof = roof;

            info.Height = GetJsonDouble(bld, "heit");
            info.GrndFlrCnt = GetJsonInt(bld, "grndFlrCnt");
            info.UgrndFlrCnt = GetJsonInt(bld, "ugrndFlrCnt");
            info.HouseholdCount = GetJsonInt(bld, "hhldCnt");
            info.FamilyCount = GetJsonInt(bld, "fmlyCnt");
            info.HoCount = GetJsonInt(bld, "hoCnt");

            info.RideUseElvtCnt = GetJsonInt(bld, "rideUseElvtCnt");
            info.EmgenUseElvtCnt = GetJsonInt(bld, "emgenUseElvtCnt");

            info.IndrAutoUtcnt = GetJsonInt(bld, "indrAutoUtcnt");
            info.OudrAutoUtcnt = GetJsonInt(bld, "oudrAutoUtcnt");
            info.IndrMechUtcnt = GetJsonInt(bld, "indrMechUtcnt");
            info.OudrMechUtcnt = GetJsonInt(bld, "oudrMechUtcnt");
            info.TotalParking = info.IndrAutoUtcnt + info.OudrAutoUtcnt + info.IndrMechUtcnt + info.OudrMechUtcnt;

            string itatBld = GetJsonString(bld, "itgrtItatBldYn");
            if (string.IsNullOrEmpty(itatBld)) itatBld = GetJsonString(bld, "itatBldYn");
            info.HasOfficialViolationData = !string.IsNullOrEmpty(itatBld);
            info.IsViolatingBuilding = itatBld == "1" || itatBld.Equals("Y", StringComparison.OrdinalIgnoreCase);

            // 공공 오픈API는 정책상 itgrtItatBldYn 필드가 생략되는 경우가 많으므로
            // 대장 내 구조, 비고, 용도 등 텍스트에서 불법/위반/판넬/가설 키워드를 감지하여 중개사고 예방
            string checkStr = $"{info.Structure} {info.EtcPurps} {GetJsonString(bld, "etcStrct")} {GetJsonString(bld, "bldNm")}";
            if (checkStr.Contains("위반") || checkStr.Contains("불법") || checkStr.Contains("시정명령"))
            {
                info.IsViolatingBuilding = true;
                info.IsSuspiciousViolation = true;
                info.ViolationSuspicionReason = "대장 전산에 위반/불법/시정명령 표기 감지";
            }
            else if (checkStr.Contains("조립식판넬") || checkStr.Contains("판넬조") || checkStr.Contains("컨테이너") || checkStr.Contains("가설"))
            {
                info.IsSuspiciousViolation = true;
                info.ViolationSuspicionReason = "대장 구조에 '조립식판넬/가설구조' 표기 (상층부/옥탑 무단증축 의심)";
            }

            info.PmsDay = GetJsonString(bld, "pmsDay");
            info.StcnsDay = GetJsonString(bld, "stcnsDay");
            info.UseApprovalDate = GetJsonString(bld, "useAprDay");

            try
            {
                info.RawJson = bld.GetRawText();
            }
            catch
            {
                info.RawJson = "{}";
            }
            info.Message = "건축물대장 표제부 조회가 완료되었습니다.";
        }

        /// <summary>
        /// 국토교통부 건축HUB 전유공용면적 API 호출하여 호별 전유부/공용부 상세 내역을 수집 (단지 내 동/호수 타겟 조회 지원)
        /// </summary>
        public async Task<List<ExposPubuseAreaItem>> QueryExposPubuseAreaAsync(
            string sigunguCd, 
            string bjdongCd, 
            string bun, 
            string ji, 
            string targetDong = "", 
            string targetHo = "", 
            string platGbCd = "0")
        {
            var list = new List<ExposPubuseAreaItem>();
            try
            {
                if (string.IsNullOrEmpty(bun)) bun = "0000";
                if (string.IsNullOrEmpty(ji)) ji = "0000";
                bun = bun.PadLeft(4, '0');
                ji = ji.PadLeft(4, '0');
                string pGb = string.IsNullOrEmpty(platGbCd) ? "0" : platGbCd;

                string hoDigits = Regex.Replace(targetHo ?? "", @"[^\d]", "");

                // 1단계: targetHo 및 targetDong 후보군으로 국토부 API 직접 정밀 타겟 쿼리 (대단지 수만건 부하 방지 및 정밀 추출)
                if (!string.IsNullOrEmpty(hoDigits))
                {
                    var dongCandidates = new List<string>();
                    if (!string.IsNullOrWhiteSpace(targetDong))
                    {
                        string dTrim = targetDong.Trim();
                        dongCandidates.Add(dTrim.EndsWith("동") ? dTrim : dTrim + "동");
                        dongCandidates.Add(dTrim.Replace("동", "").Trim());
                    }
                    dongCandidates.Add(""); // 동 파라미터 없이 호수 단독 시도

                    foreach (var dongCand in dongCandidates)
                    {
                        string dQuery = !string.IsNullOrEmpty(dongCand) ? $"&dongNm={Uri.EscapeDataString(dongCand)}" : "";
                        string hQuery = $"&hoNm={Uri.EscapeDataString(hoDigits)}";
                        string targetedUrl = $"https://apis.data.go.kr/1613000/BldRgstHubService/getBrExposPubuseAreaInfo?serviceKey={ServiceKey}&sigunguCd={sigunguCd}&bjdongCd={bjdongCd}&platGbCd={pGb}&bun={bun}&ji={ji}{dQuery}{hQuery}&numOfRows=100&pageNo=1&_type=json";

                        var candItems = await FetchExposPubuseFromUrlAsync(targetedUrl);
                        if (candItems.Count > 0)
                        {
                            return candItems;
                        }
                    }
                }

                // 2단계: 호수 직접 쿼리로 안 나오는 경우 최대 1000건 조회하여 로컬 필터링 폴백
                string fallbackUrl = $"https://apis.data.go.kr/1613000/BldRgstHubService/getBrExposPubuseAreaInfo?serviceKey={ServiceKey}&sigunguCd={sigunguCd}&bjdongCd={bjdongCd}&platGbCd={pGb}&bun={bun}&ji={ji}&numOfRows=1000&pageNo=1&_type=json";
                return await FetchExposPubuseFromUrlAsync(fallbackUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExposPubuseArea Warning] {ex.Message}");
            }
            return list;
        }

        private static async Task<List<ExposPubuseAreaItem>> FetchExposPubuseFromUrlAsync(string url)
        {
            var list = new List<ExposPubuseAreaItem>();
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return list;

                string jsonStr = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;
                if (!root.TryGetProperty("response", out var respObj)) return list;
                if (!respObj.TryGetProperty("body", out var body)) return list;
                if (!body.TryGetProperty("items", out var itemsObj)) return list;
                if (!itemsObj.TryGetProperty("item", out var items)) return list;

                if (items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var it in items.EnumerateArray())
                    {
                        var parsed = ParseExposPubuseItem(it);
                        if (parsed != null) list.Add(parsed);
                    }
                }
                else if (items.ValueKind == JsonValueKind.Object)
                {
                    var parsed = ParseExposPubuseItem(items);
                    if (parsed != null) list.Add(parsed);
                }
            }
            catch
            {
                // ignore
            }
            return list;
        }

        private static ExposPubuseAreaItem? ParseExposPubuseItem(JsonElement el)
        {
            try
            {
                string hoNm = GetJsonString(el, "hoNm");
                string dongNm = GetJsonString(el, "dongNm");
                string exposGb = GetJsonString(el, "exposPubuseGbCdNm");
                string mainPurps = GetJsonString(el, "mainPurpsCdNm");
                string etcPurps = GetJsonString(el, "etcPurps");
                double area = GetJsonDouble(el, "area");
                string flrNoNm = GetJsonString(el, "flrNoNm");

                return new ExposPubuseAreaItem
                {
                    HoNm = hoNm,
                    DongNm = dongNm,
                    ExposPubuseGbCdNm = exposGb,
                    MainPurpsCdNm = mainPurps,
                    EtcPurps = etcPurps,
                    Area = area,
                    FlrNoNm = flrNoNm
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 건축물대장 표제부에 호별 전유공용면적 및 법정 대지권(대지지분)을 보완 결합
        /// </summary>
        public async Task EnrichWithExposPubuseAreaAsync(
            BuildingLedgerInfo ledger, 
            string sigunguCd, 
            string bjdongCd, 
            string bun, 
            string ji, 
            string targetHo, 
            string targetDong,
            string platGbCd = "0")
        {
            if (ledger == null || string.IsNullOrWhiteSpace(targetHo)) return;

            ledger.TargetHo = targetHo;
            ledger.TargetDong = targetDong;

            var allItems = await QueryExposPubuseAreaAsync(sigunguCd, bjdongCd, bun, ji, targetDong, targetHo, platGbCd);
            if (allItems.Count == 0) return;

            // 호수 숫자만 정제 (예: "802호" -> "802")
            string hoDigits = Regex.Replace(targetHo, @"[^\d]", "");
            if (string.IsNullOrEmpty(hoDigits)) hoDigits = targetHo;

            var matchedItems = allItems.Where(x => 
                x.HoNm == hoDigits || 
                x.HoNm.TrimStart('0') == hoDigits.TrimStart('0') ||
                x.HoNm.Contains(hoDigits)).ToList();

            // 동 지정이 있는 경우 동 필터링
            if (!string.IsNullOrWhiteSpace(targetDong))
            {
                string cleanDong = targetDong.Trim();
                string dongDigits = Regex.Replace(cleanDong, @"[^\d]", "");
                var dongFiltered = matchedItems.Where(x => 
                    x.DongNm.Equals(cleanDong, StringComparison.OrdinalIgnoreCase) ||
                    x.DongNm.Equals(cleanDong + "동", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(dongDigits) && (x.DongNm.Contains(dongDigits) || x.DongNm == dongDigits + "동"))).ToList();
                if (dongFiltered.Count > 0)
                {
                    matchedItems = dongFiltered;
                }
            }

            if (matchedItems.Count > 0)
            {
                ledger.ExposPubuseList = matchedItems;
                ledger.UnitExclusiveArea = Math.Round(matchedItems.Where(x => x.ExposPubuseGbCdNm == "전유").Sum(x => x.Area), 2);
                ledger.UnitCommonArea = Math.Round(matchedItems.Where(x => x.ExposPubuseGbCdNm == "공용").Sum(x => x.Area), 2);

                // 법정 대지권(대지지분) 산출 (전체 대지면적 × (호별 전유면적 / 단지 전체 전유면적 합계))
                if (ledger.PlatArea > 0 && ledger.UnitExclusiveArea > 0)
                {
                    double totalExposArea = allItems.Where(x => x.ExposPubuseGbCdNm == "전유").Sum(x => x.Area);
                    if (totalExposArea <= ledger.UnitExclusiveArea)
                    {
                        int unitCount = ledger.HouseholdCount > 0 ? ledger.HouseholdCount : (ledger.HoCount > 0 ? ledger.HoCount : 50);
                        totalExposArea = ledger.UnitExclusiveArea * unitCount;
                    }

                    double landShare = Math.Round(ledger.PlatArea * (ledger.UnitExclusiveArea / totalExposArea), 2);
                    ledger.UnitLandShareArea = landShare;
                    ledger.UnitLandShareRatio = $"{ledger.PlatArea:N2}분의 {landShare:N2}";
                }

                // RawJson 딕셔너리에 호별 상세 정보 융합 저장
                try
                {
                    var dict = new Dictionary<string, object>();
                    if (!string.IsNullOrWhiteSpace(ledger.RawJson) && ledger.RawJson.Trim().StartsWith("{"))
                    {
                        using var doc = JsonDocument.Parse(ledger.RawJson);
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            dict[prop.Name] = prop.Value.Clone();
                        }
                    }
                    dict["targetHo"] = targetHo;
                    dict["targetDong"] = targetDong;
                    dict["unitExclusiveArea"] = ledger.UnitExclusiveArea;
                    dict["unitCommonArea"] = ledger.UnitCommonArea;
                    dict["unitContractArea"] = ledger.UnitContractArea;
                    dict["unitLandShareArea"] = ledger.UnitLandShareArea;
                    dict["unitLandShareRatio"] = ledger.UnitLandShareRatio;
                    dict["exposPubuseList"] = ledger.ExposPubuseList;
                    ledger.RawJson = JsonSerializer.Serialize(dict);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
