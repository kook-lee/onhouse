using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OnHouseLocal.Services
{
    public class PublicHousingPriceYearItem
    {
        public string Year { get; set; } = string.Empty;
        public long Price { get; set; } // 원 단위
        public double Area { get; set; } // 전용면적 (㎡)
        public string Dong { get; set; } = string.Empty;
        public string Ho { get; set; } = string.Empty;
        public string Floor { get; set; } = string.Empty;
        public string UpdatedDate { get; set; } = string.Empty;
    }

    public class PublicHousingPriceInfo
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Pnu { get; set; } = string.Empty;
        public string ComplexName { get; set; } = string.Empty;
        public string DongNm { get; set; } = string.Empty;
        public string HoNm { get; set; } = string.Empty;
        public double ExclusiveArea { get; set; }
        public string HousingType { get; set; } = string.Empty; // 아파트, 연립, 다세대
        
        public long PublicPrice { get; set; } // 최신 공시가격 (원 단위)
        public string BaseYear { get; set; } = string.Empty; // 기준연도 (예: 2024)
        
        /// <summary>
        /// HUG 안심전세 126% 보증보험 가입 한도: 공시가격 × 140% × 90% = 공시가격 × 1.26
        /// </summary>
        public long HugGuaranteeLimit => (long)(PublicPrice * 1.26);

        /// <summary>
        /// 해당 호수 정확 일치 여부 (false인 경우 해당 동 또는 단지 대표/유사면적 가격)
        /// </summary>
        public bool IsExactUnitMatch { get; set; }

        public List<PublicHousingPriceYearItem> History { get; set; } = new();
    }

    public class VWorldHousingPriceService
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(7) };
        private const string ServiceKey = "084E3A8F-CACB-426A-BC5A-24920CF543D6";
        private const string BaseUrl = "https://api.vworld.kr/ned/data/getApartHousingPriceAttr";

        private static VWorldHousingPriceService? _instance;
        public static VWorldHousingPriceService Instance => _instance ??= new VWorldHousingPriceService();

        /// <summary>
        /// 19자리 PNU 및 동/호/전용면적을 전달받아 최신 공동주택 공시가격 및 HUG 126% 한도를 산출
        /// </summary>
        public async Task<PublicHousingPriceInfo> QueryApartmentPriceAsync(
            string pnu, 
            string dongNm = "", 
            string hoNm = "", 
            double exclusiveArea = 0)
        {
            var result = new PublicHousingPriceInfo { Pnu = pnu };

            if (string.IsNullOrWhiteSpace(pnu) || pnu.Length < 10)
            {
                result.Success = false;
                result.Message = "올바른 19자리 PNU(필지고유번호)가 제공되지 않았습니다.";
                return result;
            }

            // 동, 호 정제 (예: "101동" -> "101", "201호" -> "201")
            string dongClean = CleanDong(dongNm);
            string hoClean = CleanHo(hoNm);

            try
            {
                List<PublicHousingPriceYearItem> records = new();

                // 1차 시도: 최신 연도(2026 -> 2025 -> 2024)부터 지정하여 최신 공시가격 우선 조회
                int curYear = DateTime.Now.Year;
                for (int y = curYear; y >= curYear - 2; y--)
                {
                    var yrRecords = await FetchPriceRecordsAsync(pnu, dongClean, hoClean, y.ToString());
                    if (yrRecords.Count == 0 && (!string.IsNullOrEmpty(dongClean) || !string.IsNullOrEmpty(hoClean)))
                    {
                        yrRecords = await FetchPriceRecordsAsync(pnu, "", "", y.ToString());
                    }

                    if (yrRecords.Count > 0)
                    {
                        records = yrRecords;
                        break;
                    }
                }

                // 2차 시도: 연도별 조회에서 나오지 않은 경우, 전체 연도 통합 조회 (최대 1000건 확보)
                if (records.Count == 0)
                {
                    records = await FetchPriceRecordsAsync(pnu, dongClean, hoClean, "");
                    if (records.Count == 0 && (!string.IsNullOrEmpty(dongClean) || !string.IsNullOrEmpty(hoClean)))
                    {
                        records = await FetchPriceRecordsAsync(pnu, "", "", "");
                    }
                }

                if (records.Count == 0)
                {
                    result.Success = false;
                    result.Message = "국토교통부(VWorld) 공동주택 공시가격 내역이 조회되지 않았습니다. (단독/다가구 또는 상가/오피스텔일 수 있습니다)";
                    return result;
                }

                // 2. 최적 레코드 선별
                // 우선순위 1: 동 + 호 정확 일치
                var matchRecords = records;
                bool exactMatch = false;

                if (!string.IsNullOrEmpty(dongClean) && !string.IsNullOrEmpty(hoClean))
                {
                    var exact = records.Where(r => 
                        (r.Dong.Contains(dongClean) || dongClean.Contains(r.Dong)) && 
                        (r.Ho == hoClean || r.Ho.Contains(hoClean))).ToList();

                    if (exact.Count > 0)
                    {
                        matchRecords = exact;
                        exactMatch = true;
                    }
                }

                // 우선순위 2: 호수 일치
                if (!exactMatch && !string.IsNullOrEmpty(hoClean))
                {
                    var hoMatches = records.Where(r => r.Ho == hoClean || r.Ho.Contains(hoClean)).ToList();
                    if (hoMatches.Count > 0)
                    {
                        matchRecords = hoMatches;
                        exactMatch = true;
                    }
                }

                // 우선순위 3: 전용면적(±2.0㎡ 이내) 일치
                if (!exactMatch && exclusiveArea > 0)
                {
                    var areaMatches = records.Where(r => Math.Abs(r.Area - exclusiveArea) <= 2.0).ToList();
                    if (areaMatches.Count > 0)
                    {
                        matchRecords = areaMatches;
                    }
                }

                // 최신 연도 기준 정렬 (숫자 크기 내림차순 정렬)
                var sorted = matchRecords
                    .OrderByDescending(r => int.TryParse(r.Year, out int y) ? y : 0)
                    .ThenByDescending(r => r.UpdatedDate)
                    .ToList();
                var latest = sorted.First();

                result.Success = true;
                result.PublicPrice = latest.Price;
                result.BaseYear = latest.Year;
                result.DongNm = latest.Dong;
                result.HoNm = latest.Ho;
                result.ExclusiveArea = latest.Area;
                result.IsExactUnitMatch = exactMatch;
                result.History = sorted;
                result.Message = exactMatch 
                    ? $"{latest.Year}년 기준 공동주택 공시가격 {latest.Price:N0}원 (해당 호수 일치)" 
                    : $"{latest.Year}년 기준 공동주택 공시가격 {latest.Price:N0}원 (단지/동 유사 기준가)";

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"VWorld 공시가격 조회 오류: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 주소를 기반으로 PNU를 생성하여 공시가격 조회
        /// </summary>
        public async Task<PublicHousingPriceInfo> QueryApartmentPriceByAddressAsync(
            string address, 
            string dongNm = "", 
            string hoNm = "", 
            double exclusiveArea = 0)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return new PublicHousingPriceInfo { Success = false, Message = "주소가 비어 있습니다." };
            }

            string sigunguCd = "11620";
            string bjdongCd = "10200";
            string bun = "0000";
            string ji = "0000";

            foreach (var pair in BuildingLedgerService.SigunguMap)
            {
                if (address.Contains(pair.Key))
                {
                    sigunguCd = pair.Value;
                    break;
                }
            }

            foreach (var pair in BuildingLedgerService.DongMap)
            {
                if (address.Contains(pair.Key))
                {
                    sigunguCd = pair.Value.sigunguCd;
                    bjdongCd = pair.Value.bjdongCd;
                    break;
                }
            }

            var lotMatch = Regex.Match(address, @"(\d{1,4})(?:-(\d{1,4}))?");
            if (lotMatch.Success)
            {
                bun = lotMatch.Groups[1].Value.PadLeft(4, '0');
                if (lotMatch.Groups[2].Success && !string.IsNullOrEmpty(lotMatch.Groups[2].Value))
                {
                    ji = lotMatch.Groups[2].Value.PadLeft(4, '0');
                }
            }

            string pnu = BuildingLedgerService.BuildPnu(sigunguCd, bjdongCd, bun, ji);
            return await QueryApartmentPriceAsync(pnu, dongNm, hoNm, exclusiveArea);
        }

        private async Task<List<PublicHousingPriceYearItem>> FetchPriceRecordsAsync(string pnu, string dong, string ho, string stdrYear = "")
        {
            var list = new List<PublicHousingPriceYearItem>();
            string url = $"{BaseUrl}?key={ServiceKey}&pnu={pnu}&format=json&numOfRows=1000&pageNo=1";

            if (!string.IsNullOrEmpty(stdrYear)) url += $"&stdrYear={stdrYear}";
            if (!string.IsNullOrEmpty(dong)) url += $"&dongNm={Uri.EscapeDataString(dong)}";
            if (!string.IsNullOrEmpty(ho)) url += $"&hoNm={Uri.EscapeDataString(ho)}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return list;

            string jsonStr = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            if (!root.TryGetProperty("apartHousingPrices", out var aphProp)) return list;
            if (!aphProp.TryGetProperty("field", out var fieldProp)) return list;

            if (fieldProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in fieldProp.EnumerateArray())
                {
                    var record = ParseRecord(item);
                    if (record != null) list.Add(record);
                }
            }
            else if (fieldProp.ValueKind == JsonValueKind.Object)
            {
                var record = ParseRecord(fieldProp);
                if (record != null) list.Add(record);
            }

            return list;
        }

        private static PublicHousingPriceYearItem? ParseRecord(JsonElement item)
        {
            try
            {
                string year = GetStr(item, "stdrYear");
                string priceStr = GetStr(item, "pblntfPc");
                if (!long.TryParse(priceStr, out long price) || price <= 0) return null;

                string areaStr = GetStr(item, "prvuseAr");
                double.TryParse(areaStr, out double area);

                return new PublicHousingPriceYearItem
                {
                    Year = year,
                    Price = price,
                    Area = area,
                    Dong = GetStr(item, "dongNm"),
                    Ho = GetStr(item, "hoNm"),
                    Floor = GetStr(item, "floorNm"),
                    UpdatedDate = GetStr(item, "lastUpdtDt")
                };
            }
            catch
            {
                return null;
            }
        }

        private static string CleanDong(string dong)
        {
            if (string.IsNullOrWhiteSpace(dong)) return "";
            dong = dong.Trim();
            var m = Regex.Match(dong, @"^(\d+)동?$");
            return m.Success ? m.Groups[1].Value : dong.Replace("동", "").Trim();
        }

        private static string CleanHo(string ho)
        {
            if (string.IsNullOrWhiteSpace(ho)) return "";
            ho = ho.Trim();
            var m = Regex.Match(ho, @"^([1-9]\d{1,3})호?$");
            return m.Success ? m.Groups[1].Value : ho.Replace("호", "").Trim();
        }

        private static string GetStr(JsonElement el, string name)
        {
            if (el.TryGetProperty(name, out var p))
            {
                return p.GetString()?.Trim() ?? "";
            }
            return "";
        }
    }
}
