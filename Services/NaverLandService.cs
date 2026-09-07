using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OnHouseLocal.Models;

namespace OnHouseLocal.Services
{
    public class NaverArticleDetail
    {
        public string ArticleNumber { get; set; } = "";
        public string ArticleName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string TradeType { get; set; } = "";
        public string RealEstateType { get; set; } = "";
        public long Price { get; set; }
        public long PreviousDeposit { get; set; }
        public long PreviousMonthlyRent { get; set; }
        public double ExclusiveArea { get; set; }
        public double SupplyArea { get; set; }
        public string TargetFloor { get; set; } = "";
        public int TotalFloor { get; set; }
        public int RoomCount { get; set; }
        public int BathRoomCount { get; set; }
        public string Direction { get; set; } = "";
        public bool HasElevator { get; set; }
        public int TotalParking { get; set; }
        public int HouseholdCount { get; set; }
        public string ApprovalDate { get; set; } = "";
        public string BuildingUse { get; set; } = "";
        public string Pnu { get; set; } = "";
        public string SigunguCd { get; set; } = "";
        public string BjdongCd { get; set; } = "";
        public string Bun { get; set; } = "";
        public string Ji { get; set; } = "";
        public string FullJibunAddress { get; set; } = "";
    }

    public class DiscrepancyItem
    {
        public string ItemName { get; set; } = "";
        public string NaverValue { get; set; } = "";
        public string LedgerValue { get; set; } = "";
        public string Status { get; set; } = "Match"; // "Match", "Mismatch", "Warning"
        public string Note { get; set; } = "";
    }

    public class NaverInspectionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string ArticleNumber { get; set; } = "";
        public NaverArticleDetail? NaverItem { get; set; }
        public BuildingLedgerInfo? LedgerItem { get; set; }
        public List<DiscrepancyItem> Discrepancies { get; set; } = new();
        public string OverallStatus { get; set; } = "Safe"; // "Safe", "Warning", "Danger"
        public string Summary { get; set; } = "";
    }

    public class NaverLandService
    {
        private static readonly System.Net.CookieContainer _cookieContainer = new();
        private static readonly HttpClient _httpClient = new(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            CookieContainer = _cookieContainer
        })
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        private const string BaseUa = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36";
        private const string SaltKey = "a7f3c91e4b6d28a05fc7e13b9d4a6082";

        /// <summary>
        /// 네이버 부동산 URL 또는 매물번호를 입력받아 articleNumber를 추출
        /// </summary>
        public string ExtractArticleNumber(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            input = input.Trim();

            // 1. 순수 숫자 매물 번호인 경우 (예: 2647844806)
            if (Regex.IsMatch(input, @"^\d{8,12}$"))
            {
                return input;
            }

            // 2. layer= 파라미터가 포함된 URL인 경우 (LZ-String 압축 해제)
            var layerMatch = Regex.Match(input, @"[?&]layer=([^&#]+)");
            if (layerMatch.Success)
            {
                try
                {
                    string rawLayer = Uri.UnescapeDataString(layerMatch.Groups[1].Value);
                    string decompressed = DecompressFromEncodedURIComponent(rawLayer);
                    if (!string.IsNullOrEmpty(decompressed))
                    {
                        var artMatch = Regex.Match(decompressed, @"""articleId""\s*:\s*""?(\d+)""?");
                        if (artMatch.Success) return artMatch.Groups[1].Value;

                        var artNumMatch = Regex.Match(decompressed, @"""articleNumber""\s*:\s*""?(\d+)""?");
                        if (artNumMatch.Success) return artNumMatch.Groups[1].Value;
                    }
                }
                catch
                {
                    // 계속 다른 파싱 시도
                }
            }

            // 3. /articles/2647844806 또는 /article/info/2647844806
            var urlMatch = Regex.Match(input, @"(?:articles|info|articleNumber|articleId)[/=:]([0-9]{8,12})", RegexOptions.IgnoreCase);
            if (urlMatch.Success)
            {
                return urlMatch.Groups[1].Value;
            }

            // 4. URL 내부의 8~12자리 숫자
            var genericNumMatch = Regex.Match(input, @"\b([0-9]{9,11})\b");
            if (genericNumMatch.Success)
            {
                return genericNumMatch.Groups[1].Value;
            }

            return "";
        }

        /// <summary>
        /// 여러 줄의 URL 또는 쉼표/공백으로 구분된 매물 텍스트에서 매물번호 목록 추출
        /// </summary>
        public List<string> ExtractMultipleArticleNumbers(string text)
        {
            var result = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            var lines = text.Split(new[] { '\r', '\n', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                string artNo = ExtractArticleNumber(trimmed);
                if (!string.IsNullOrEmpty(artNo))
                {
                    result.Add(artNo);
                }
            }

            var genericMatches = Regex.Matches(text, @"(?:articleId|articleNumber|articles|info)?[:/=]?\s*([0-9]{9,11})");
            foreach (Match m in genericMatches)
            {
                if (m.Groups.Count > 1 && !string.IsNullOrEmpty(m.Groups[1].Value))
                {
                    result.Add(m.Groups[1].Value);
                }
            }

            return new List<string>(result);
        }

        /// <summary>
        /// 네이버 부동산 매물 상세 정보 크롤링
        /// </summary>
        public async Task<NaverArticleDetail?> FetchNaverArticleAsync(string articleNumber)
        {
            if (string.IsNullOrEmpty(articleNumber)) return null;

            try
            {
                // 1. 네이버 지도 웹페이지 요청하여 maskedAttestToken 추출
                string mapUrl = "https://fin.land.naver.com/map";
                using var reqPage = new HttpRequestMessage(HttpMethod.Get, mapUrl);
                reqPage.Headers.Add("User-Agent", BaseUa);
                reqPage.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                reqPage.Headers.Add("Accept-Language", "ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7");

                var pageRes = await _httpClient.SendAsync(reqPage);
                if (!pageRes.IsSuccessStatusCode) return null;

                string html = await pageRes.Content.ReadAsStringAsync();
                var tokenMatch = Regex.Match(html, @"maskedAttestToken[^a-zA-Z0-9_-]+([a-zA-Z0-9_-]{30,})");
                if (!tokenMatch.Success) return null;

                string maskedToken = tokenMatch.Groups[1].Value;
                string? unmasked = UnmaskAttestToken(maskedToken);
                if (string.IsNullOrEmpty(unmasked)) return null;

                byte[] nonce = new byte[8];
                RandomNumberGenerator.Fill(nonce);
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string xAttest = MaskAttestToken($"{nowMs}.{unmasked}", nonce);

                // 2. /article/key 호출하여 PNU 및 유형(realEstateType, tradeType) 조회
                string keyUrl = $"https://fin.land.naver.com/front-api/v1/article/key?articleNumber={articleNumber}";
                using var reqKey = new HttpRequestMessage(HttpMethod.Get, keyUrl);
                reqKey.Headers.Add("User-Agent", BaseUa);
                reqKey.Headers.Add("Referer", mapUrl);
                reqKey.Headers.Add("Accept", "application/json, text/plain, */*");
                reqKey.Headers.Add("Accept-Language", "ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7");
                reqKey.Headers.Add("x-page-url", mapUrl);
                reqKey.Headers.Add("x-attest", xAttest);

                var keyRes = await _httpClient.SendAsync(reqKey);
                if (!keyRes.IsSuccessStatusCode) return null;

                string keyJson = await keyRes.Content.ReadAsStringAsync();
                using var keyDoc = JsonDocument.Parse(keyJson);
                var keyRoot = keyDoc.RootElement;
                if (!keyRoot.TryGetProperty("result", out var keyResult)) return null;

                string pnu = "";
                if (keyResult.TryGetProperty("key", out var kObj) && kObj.TryGetProperty("pnu", out var pnuProp))
                {
                    pnu = pnuProp.GetString() ?? "";
                }

                string realEstateType = "C02";
                string tradeType = "A1";
                if (keyResult.TryGetProperty("type", out var typeObj))
                {
                    if (typeObj.TryGetProperty("realEstateType", out var retProp)) realEstateType = retProp.GetString() ?? "C02";
                    if (typeObj.TryGetProperty("tradeType", out var ttProp)) tradeType = ttProp.GetString() ?? "A1";
                }

                string legalDivisionNumber = "";
                string jibun = "";
                if (keyResult.TryGetProperty("address", out var addrObj))
                {
                    if (addrObj.TryGetProperty("legalDivisionNumber", out var ldnProp)) legalDivisionNumber = ldnProp.GetString() ?? "";
                    if (addrObj.TryGetProperty("jibun", out var jbProp)) jibun = jbProp.GetString() ?? "";
                }

                // 3. /article/basicInfo 호출하여 상세 스펙 조회
                string basicUrl = $"https://fin.land.naver.com/front-api/v1/article/basicInfo?articleNumber={articleNumber}&realEstateType={realEstateType}&tradeType={tradeType}";
                using var reqBasic = new HttpRequestMessage(HttpMethod.Get, basicUrl);
                reqBasic.Headers.Add("User-Agent", BaseUa);
                reqBasic.Headers.Add("Referer", mapUrl);
                reqBasic.Headers.Add("Accept", "application/json, text/plain, */*");
                reqBasic.Headers.Add("Accept-Language", "ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7");
                reqBasic.Headers.Add("x-page-url", mapUrl);
                reqBasic.Headers.Add("x-attest", xAttest);

                var basicRes = await _httpClient.SendAsync(reqBasic);
                if (!basicRes.IsSuccessStatusCode) return null;

                string basicJson = await basicRes.Content.ReadAsStringAsync();
                using var basicDoc = JsonDocument.Parse(basicJson);
                var bRoot = basicDoc.RootElement;
                if (!bRoot.TryGetProperty("result", out var bResult)) return null;

                var detail = new NaverArticleDetail
                {
                    ArticleNumber = articleNumber,
                    RealEstateType = realEstateType,
                    TradeType = tradeType == "A1" ? "매매" : (tradeType == "B1" ? "전세" : "월세"),
                    Pnu = pnu
                };

                // PNU 또는 legalDivisionNumber 분해 (sigunguCd 5자리, bjdongCd 5자리, bun 4자리, ji 4자리)
                if (legalDivisionNumber.Length >= 10)
                {
                    detail.SigunguCd = legalDivisionNumber.Substring(0, 5);
                    detail.BjdongCd = legalDivisionNumber.Substring(5, 5);
                }
                else if (pnu.Length >= 10)
                {
                    detail.SigunguCd = pnu.Substring(0, 5);
                    detail.BjdongCd = pnu.Substring(5, 5);
                }

                if (!string.IsNullOrEmpty(jibun))
                {
                    var jParts = jibun.Split('-');
                    if (jParts.Length > 0 && int.TryParse(jParts[0], out int bNum))
                        detail.Bun = bNum.ToString().PadLeft(4, '0');
                    if (jParts.Length > 1 && int.TryParse(jParts[1], out int jNum))
                        detail.Ji = jNum.ToString().PadLeft(4, '0');
                    else
                        detail.Ji = "0000";
                }
                else if (pnu.Length >= 19)
                {
                    detail.Bun = pnu.Substring(11, 4);
                    detail.Ji = pnu.Substring(15, 4);
                }

                // 가격 정보
                if (bResult.TryGetProperty("priceInfo", out var priceInfo))
                {
                    if (priceInfo.TryGetProperty("price", out var pProp) && pProp.ValueKind == JsonValueKind.Number)
                    {
                        detail.Price = pProp.GetInt64();
                    }
                    if (priceInfo.TryGetProperty("previousDeposit", out var pdProp) && pdProp.ValueKind == JsonValueKind.Number)
                    {
                        detail.PreviousDeposit = pdProp.GetInt64();
                    }
                    if (priceInfo.TryGetProperty("previousMonthlyRent", out var pmrProp) && pmrProp.ValueKind == JsonValueKind.Number)
                    {
                        detail.PreviousMonthlyRent = pmrProp.GetInt64();
                    }
                }

                // 상세 정보
                if (bResult.TryGetProperty("detailInfo", out var dInfo))
                {
                    // articleDetailInfo
                    if (dInfo.TryGetProperty("articleDetailInfo", out var adInfo))
                    {
                        detail.ArticleName = adInfo.TryGetProperty("articleName", out var anProp) ? anProp.GetString() ?? "" : "";
                        detail.Title = adInfo.TryGetProperty("articleFeatureDescription", out var afProp) ? afProp.GetString() ?? "" : "";
                        detail.Description = adInfo.TryGetProperty("articleDescription", out var descProp) ? descProp.GetString() ?? "" : "";
                        detail.BuildingUse = adInfo.TryGetProperty("buildingUse", out var buProp) ? buProp.GetString() ?? "" : "";
                    }

                    // sizeInfo
                    if (dInfo.TryGetProperty("sizeInfo", out var sInfo))
                    {
                        if (sInfo.TryGetProperty("exclusiveSpace", out var esProp) && esProp.ValueKind == JsonValueKind.Number)
                            detail.ExclusiveArea = esProp.GetDouble();
                        if (sInfo.TryGetProperty("supplySpace", out var ssProp) && ssProp.ValueKind == JsonValueKind.Number)
                            detail.SupplyArea = ssProp.GetDouble();
                    }

                    // spaceInfo
                    if (dInfo.TryGetProperty("spaceInfo", out var spInfo))
                    {
                        if (spInfo.TryGetProperty("roomCount", out var rcProp) && rcProp.ValueKind == JsonValueKind.Number)
                            detail.RoomCount = rcProp.GetInt32();
                        if (spInfo.TryGetProperty("bathRoomCount", out var bcProp) && bcProp.ValueKind == JsonValueKind.Number)
                            detail.BathRoomCount = bcProp.GetInt32();
                        if (spInfo.TryGetProperty("direction", out var dirProp))
                            detail.Direction = dirProp.GetString() ?? "";

                        if (spInfo.TryGetProperty("floorInfo", out var flInfo))
                        {
                            detail.TargetFloor = flInfo.TryGetProperty("targetFloor", out var tfProp) ? tfProp.GetString() ?? "" : "";
                            if (flInfo.TryGetProperty("totalFloor", out var totProp))
                            {
                                if (totProp.ValueKind == JsonValueKind.Number) detail.TotalFloor = totProp.GetInt32();
                                else if (totProp.ValueKind == JsonValueKind.String && int.TryParse(totProp.GetString(), out int tf)) detail.TotalFloor = tf;
                            }
                        }
                    }

                    // facilityInfo
                    if (dInfo.TryGetProperty("facilityInfo", out var facInfo))
                    {
                        if (facInfo.TryGetProperty("etc", out var etcArray) && etcArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in etcArray.EnumerateArray())
                            {
                                if (item.GetString() == "ELEVATOR")
                                {
                                    detail.HasElevator = true;
                                    break;
                                }
                            }
                        }

                        if (facInfo.TryGetProperty("totalParkingCount", out var tpProp) && tpProp.ValueKind == JsonValueKind.Number)
                            detail.TotalParking = tpProp.GetInt32();

                        if (facInfo.TryGetProperty("householdNumber", out var hnProp) && hnProp.ValueKind == JsonValueKind.Number)
                            detail.HouseholdCount = hnProp.GetInt32();

                        if (facInfo.TryGetProperty("buildingConjunctionDate", out var bcdProp))
                        {
                            string rawDate = bcdProp.GetString() ?? "";
                            if (rawDate.Length == 8)
                            {
                                detail.ApprovalDate = $"{rawDate.Substring(0, 4)}-{rawDate.Substring(4, 2)}-{rawDate.Substring(6, 2)}";
                            }
                            else
                            {
                                detail.ApprovalDate = rawDate;
                            }
                        }
                    }
                }

                return detail;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NaverLandService Error] {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 네이버 매물 정보와 국토교통부 건축물대장을 실시간 대조하여 허위매물/정보불일치를 검증
        /// </summary>
        public async Task<NaverInspectionResult> InspectAndCompareAsync(string urlOrArticleNo, BuildingLedgerService ledgerService)
        {
            var res = new NaverInspectionResult();
            string articleNo = ExtractArticleNumber(urlOrArticleNo);
            if (string.IsNullOrEmpty(articleNo))
            {
                res.Success = false;
                res.Message = "네이버 매물 URL 또는 매물번호를 인식할 수 없습니다.";
                return res;
            }

            res.ArticleNumber = articleNo;
            var naverItem = await FetchNaverArticleAsync(articleNo);
            if (naverItem == null)
            {
                res.Success = false;
                res.Message = $"네이버 매물(번호: {articleNo})의 상세 정보를 조회할 수 없습니다. (종료되었거나 존재하지 않는 매물일 수 있습니다)";
                return res;
            }

            res.NaverItem = naverItem;

            // 건축물대장 조회 (sigunguCd, bjdongCd, bun, ji)
            BuildingLedgerInfo? ledger = null;
            if (!string.IsNullOrEmpty(naverItem.SigunguCd) && !string.IsNullOrEmpty(naverItem.BjdongCd) && !string.IsNullOrEmpty(naverItem.Bun))
            {
                ledger = await ledgerService.QueryBuildingLedgerByCodesAsync(naverItem.SigunguCd, naverItem.BjdongCd, naverItem.Bun, naverItem.Ji);
            }

            res.LedgerItem = ledger;
            res.Success = true;

            int dangerCount = 0;
            int warningCount = 0;

            if (ledger == null || !ledger.Success)
            {
                res.OverallStatus = "Warning";
                res.Message = $"네이버 매물 정보는 확인되었으나, 공공 건축물대장 조회가 원활하지 않습니다. ({ledger?.Message ?? "주소지 대장 미확인"})";
                return res;
            }

            // 1. 위반건축물 검증
            if (ledger.IsViolatingBuilding)
            {
                dangerCount++;
                res.Discrepancies.Add(new DiscrepancyItem
                {
                    ItemName = "위반건축물 여부",
                    NaverValue = "표기 없음 (정상 매물로 등록)",
                    LedgerValue = "🚨 위반건축물 등재 건물",
                    Status = "Danger",
                    Note = "건축물대장에 위반건축물로 표기되어 있어 이행강제금 부과 또는 전세대출/보증보험 불가 위험이 있습니다."
                });
            }
            else
            {
                res.Discrepancies.Add(new DiscrepancyItem
                {
                    ItemName = "위반건축물 여부",
                    NaverValue = "정상",
                    LedgerValue = "정상 (위반 없음)",
                    Status = "Match",
                    Note = "대장 상 위반건축물 등재 내역이 없는 안전한 건축물입니다."
                });
            }

            // 2. 승강기 (엘리베이터) 일치 여부
            bool ledgerHasElvt = ledger.RideUseElvtCnt > 0 || ledger.EmgenUseElvtCnt > 0;
            if (naverItem.HasElevator != ledgerHasElvt)
            {
                if (naverItem.HasElevator && !ledgerHasElvt)
                {
                    dangerCount++;
                    res.Discrepancies.Add(new DiscrepancyItem
                    {
                        ItemName = "엘리베이터 (승강기)",
                        NaverValue = "있음 (옵션 표기)",
                        LedgerValue = "없음 (대장 상 승강기 0대)",
                        Status = "Danger",
                        Note = "네이버 광고에는 엘리베이터가 있다고 표기되어 있으나 대장에는 승강기가 없습니다. (허위매물 과태료 주의)"
                    });
                }
                else
                {
                    warningCount++;
                    res.Discrepancies.Add(new DiscrepancyItem
                    {
                        ItemName = "엘리베이터 (승강기)",
                        NaverValue = "없음",
                        LedgerValue = $"있음 ({ledger.RideUseElvtCnt}대)",
                        Status = "Warning",
                        Note = "실제 대장에는 승강기가 설치되어 있으나 매물 정보에는 미표기되었습니다."
                    });
                }
            }
            else
            {
                res.Discrepancies.Add(new DiscrepancyItem
                {
                    ItemName = "엘리베이터 (승강기)",
                    NaverValue = naverItem.HasElevator ? "있음" : "없음",
                    LedgerValue = ledgerHasElvt ? $"있음 ({ledger.RideUseElvtCnt}대)" : "없음",
                    Status = "Match",
                    Note = "승강기 유무가 대장과 정확히 일치합니다."
                });
            }

            // 3. 층수 (총 층수) 비교
            if (naverItem.TotalFloor > 0 && ledger.GrndFlrCnt > 0)
            {
                if (naverItem.TotalFloor != ledger.GrndFlrCnt)
                {
                    // 불일치
                    dangerCount++;
                    res.Discrepancies.Add(new DiscrepancyItem
                    {
                        ItemName = "총 층수 (건물 규모)",
                        NaverValue = $"{naverItem.TotalFloor}층",
                        LedgerValue = $"지상 {ledger.GrndFlrCnt}층 (지하 {ledger.UgrndFlrCnt}층)",
                        Status = "Danger",
                        Note = $"네이버 광고 총 층수({naverItem.TotalFloor}층)와 대장 지상 층수({ledger.GrndFlrCnt}층)가 불일치합니다."
                    });
                }
                else
                {
                    res.Discrepancies.Add(new DiscrepancyItem
                    {
                        ItemName = "총 층수 (건물 규모)",
                        NaverValue = $"총 {naverItem.TotalFloor}층 (해당층: {naverItem.TargetFloor})",
                        LedgerValue = $"지상 {ledger.GrndFlrCnt}층 / 지하 {ledger.UgrndFlrCnt}층",
                        Status = "Match",
                        Note = "건물 전체 층수가 대장과 완벽히 일치합니다."
                    });
                }
            }

            // 4. 주차 대수 비교
            res.Discrepancies.Add(new DiscrepancyItem
            {
                ItemName = "주차 대수",
                NaverValue = $"{naverItem.TotalParking}대",
                LedgerValue = $"{ledger.TotalParking}대",
                Status = Math.Abs(naverItem.TotalParking - ledger.TotalParking) <= 1 ? "Match" : "Warning",
                Note = naverItem.TotalParking == ledger.TotalParking ? "주차 대수 일치" : "주차 대수 약간 상이 (확인 권장)"
            });

            // 5. 사용승인일 (준공일) 비교
            string ledgerAprDate = ledger.UseApprovalDate;
            if (!string.IsNullOrEmpty(ledgerAprDate) && ledgerAprDate.Length == 8)
            {
                ledgerAprDate = $"{ledgerAprDate.Substring(0, 4)}-{ledgerAprDate.Substring(4, 2)}-{ledgerAprDate.Substring(6, 2)}";
            }

            bool dateMatch = string.IsNullOrEmpty(naverItem.ApprovalDate) || string.IsNullOrEmpty(ledgerAprDate) || naverItem.ApprovalDate == ledgerAprDate;
            res.Discrepancies.Add(new DiscrepancyItem
            {
                ItemName = "사용승인일 (준공일)",
                NaverValue = naverItem.ApprovalDate,
                LedgerValue = ledgerAprDate,
                Status = dateMatch ? "Match" : "Warning",
                Note = dateMatch ? "사용승인일 일치" : "사용승인일 차이 발생"
            });

            // 6. 종합 상태 산출
            if (dangerCount > 0)
            {
                res.OverallStatus = "Danger";
                res.Summary = $"🚨 허위매물 또는 과태료 위험 항목이 {dangerCount}건 발견되었습니다! (층수/엘리베이터/위반건축물 불일치)";
            }
            else if (warningCount > 0)
            {
                res.OverallStatus = "Warning";
                res.Summary = $"⚠️ 대장과 매물 간 세부 정보에 주의 항목({warningCount}건)이 있습니다.";
            }
            else
            {
                res.OverallStatus = "Safe";
                res.Summary = "✅ 네이버 광고 정보가 실제 건축물대장과 정확하게 일치하는 안심 매물입니다!";
            }

            return res;
        }

        /// <summary>
        /// 여러 매물 번호를 입력받아 네이버에서 스펙을 수집하고 DB에 신규/갱신 저장
        /// (이미 등록되어 대장 검수(Safe/Warning/Danger)가 완료된 매물은 불필요하게 네이버에서 재수집하지 않고 안전하게 건너뜁니다)
        /// </summary>
        public async Task<(int successCount, int failedCount, int skippedCount, List<string> errors)> RegisterMultipleArticlesAsync(
            List<string> articleNumbers, 
            int userId, 
            DatabaseService db,
            bool skipAlreadyAudited = true)
        {
            int success = 0;
            int failed = 0;
            int skipped = 0;
            var errors = new List<string>();

            foreach (var artNo in articleNumbers)
            {
                try
                {
                    // 1. 이미 DB에 등록되어 있고 대장 검수(Safe, Warning, Danger)가 완료된 매물은 중복 수집 방지
                    if (skipAlreadyAudited)
                    {
                        var existing = await db.GetNaverListingByArticleNumberAsync(artNo, userId);
                        if (existing != null && !string.IsNullOrEmpty(existing.LedgerStatus) && existing.LedgerStatus != "Pending")
                        {
                            skipped++;
                            continue;
                        }
                    }

                    var detail = await FetchNaverArticleAsync(artNo);
                    if (detail == null)
                    {
                        failed++;
                        errors.Add($"매물번호 {artNo}: 네이버에서 상세 정보를 가져오지 못했습니다.");
                        continue;
                    }

                    string priceStr = "";
                    if (detail.Price > 0)
                    {
                        if (detail.TradeType == "매매") priceStr = $"매매 {FormatMoney(detail.Price)}";
                        else if (detail.TradeType == "전세") priceStr = $"전세 {FormatMoney(detail.Price)}";
                        else priceStr = $"월세 {FormatMoney(detail.PreviousDeposit)}/{detail.Price}";
                    }

                    string floorInfo = "";
                    if (!string.IsNullOrEmpty(detail.TargetFloor)) floorInfo = $"{detail.TargetFloor}층";
                    if (detail.TotalFloor > 0) floorInfo = string.IsNullOrEmpty(floorInfo) ? $"총 {detail.TotalFloor}층" : $"{floorInfo}/{detail.TotalFloor}층";

                    var item = new NaverListingItem
                    {
                        UserId = userId,
                        ArticleNumber = detail.ArticleNumber,
                        ArticleName = string.IsNullOrEmpty(detail.ArticleName) ? detail.Title : detail.ArticleName,
                        TradeType = detail.TradeType,
                        RealEstateType = detail.RealEstateType,
                        PriceDisplay = priceStr,
                        FloorInfo = floorInfo,
                        AreaM2 = detail.ExclusiveArea > 0 ? detail.ExclusiveArea : detail.SupplyArea,
                        Address = string.IsNullOrEmpty(detail.FullJibunAddress) ? $"{detail.SigunguCd} {detail.BjdongCd} {detail.Bun}-{detail.Ji}".Trim() : detail.FullJibunAddress,
                        HasElevator = detail.HasElevator,
                        TotalParking = detail.TotalParking,
                        ApprovalDate = detail.ApprovalDate,
                        RawJson = JsonSerializer.Serialize(detail),
                        LedgerStatus = "Pending",
                        LedgerMessage = "대장 검증 대기 중",
                        CreatedAt = DateTime.Now
                    };

                    await db.UpsertNaverListingAsync(item);
                    success++;
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"매물번호 {artNo}: 오류 ({ex.Message})");
                }
            }

            return (success, failed, skipped, errors);
        }

        private static string FormatMoney(long amount)
        {
            if (amount >= 10000)
            {
                long uk = amount / 10000;
                long man = amount % 10000;
                return man > 0 ? $"{uk}억 {man:N0}만" : $"{uk}억";
            }
            return $"{amount:N0}만";
        }

        /// <summary>
        /// 단일 매물에 대해 건축물대장 정밀 대조 검증 수행 후 DB 업데이트
        /// </summary>
        public async Task<NaverInspectionResult> AuditListingAsync(int listingId, BuildingLedgerService ledgerService, DatabaseService db)
        {
            var item = await db.GetNaverListingByIdAsync(listingId);
            if (item == null)
            {
                return new NaverInspectionResult
                {
                    Success = false,
                    Message = "매물 정보를 찾을 수 없습니다."
                };
            }

            var res = await InspectAndCompareAsync(item.ArticleNumber, ledgerService);
            string status = res.OverallStatus; // "Safe", "Warning", "Danger"
            if (!res.Success) status = "Failed";

            string discrepanciesJson = JsonSerializer.Serialize(res.Discrepancies);
            await db.UpdateNaverListingLedgerResultAsync(listingId, status, res.Summary, discrepanciesJson);
            return res;
        }

        #region Token Encryption & Decryption Helpers

        private static byte[] H(byte[] t, byte[] e)
        {
            byte[] r = DeriveKey(e, t.Length);
            byte[] n = new byte[t.Length];
            for (int i = 0; i < t.Length; i++)
            {
                n[i] = (byte)(t[i] ^ r[i]);
            }
            return n;
        }

        private static byte[] DeriveKey(byte[] e, int length)
        {
            string hexR = Convert.ToHexString(e).ToLowerInvariant();
            byte[] n = new byte[length];
            using var sha = SHA256.Create();

            for (int t = 0, i = 0; t < length; t += 32, i += 1)
            {
                byte[] seedBytes = Encoding.UTF8.GetBytes($"{SaltKey}:{hexR}:{i}");
                byte[] s = sha.ComputeHash(seedBytes);
                for (int j = 0; j < 32 && t + j < length; j++)
                {
                    n[t + j] = s[j];
                }
            }
            return n;
        }

        public static string? UnmaskAttestToken(string t)
        {
            try
            {
                string b64 = t.Replace('-', '+').Replace('_', '/');
                b64 = b64.PadRight(4 * (int)Math.Ceiling(b64.Length / 4.0), '=');
                byte[] raw = Convert.FromBase64String(b64);
                if (raw.Length <= 8) return null;

                byte[] nonce = new byte[8];
                Array.Copy(raw, 0, nonce, 0, 8);

                byte[] payload = new byte[raw.Length - 8];
                Array.Copy(raw, 8, payload, 0, payload.Length);

                byte[] decrypted = H(payload, nonce);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return null;
            }
        }

        public static string MaskAttestToken(string t, byte[] nonce)
        {
            byte[] plain = Encoding.UTF8.GetBytes(t);
            byte[] enc = H(plain, nonce);

            byte[] combined = new byte[nonce.Length + enc.Length];
            Array.Copy(nonce, 0, combined, 0, nonce.Length);
            Array.Copy(enc, 0, combined, nonce.Length, enc.Length);

            string b64 = Convert.ToBase64String(combined);
            return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        #endregion

        #region LZ-String Decompression

        private static readonly string KeyStrUriSafe = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+-$";

        public static string DecompressFromEncodedURIComponent(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            input = input.Replace(' ', '+');
            return _decompress(input.Length, 32, index => KeyStrUriSafe.IndexOf(input[index]));
        }

        private static string _decompress(int length, int resetValue, Func<int, int> getNextValue)
        {
            var dictionary = new List<string>();
            int enlargeIn = 4;
            int dictSize = 4;
            int numBits = 3;
            string entry = "";
            var result = new StringBuilder();
            string w;
            int bits, resb, maxpower, power;
            string c;

            var data = new DataWrapper
            {
                Val = getNextValue(0),
                Position = resetValue,
                Index = 1
            };

            for (int i = 0; i < 3; i++)
            {
                dictionary.Add(((char)i).ToString());
            }

            bits = 0;
            maxpower = 4;
            power = 1;
            while (power != maxpower)
            {
                resb = data.Val & data.Position;
                data.Position >>= 1;
                if (data.Position == 0)
                {
                    data.Position = resetValue;
                    data.Val = getNextValue(data.Index++);
                }
                bits |= (resb > 0 ? 1 : 0) * power;
                power <<= 1;
            }

            switch (bits)
            {
                case 0:
                    bits = 0;
                    maxpower = 256;
                    power = 1;
                    while (power != maxpower)
                    {
                        resb = data.Val & data.Position;
                        data.Position >>= 1;
                        if (data.Position == 0)
                        {
                            data.Position = resetValue;
                            data.Val = getNextValue(data.Index++);
                        }
                        bits |= (resb > 0 ? 1 : 0) * power;
                        power <<= 1;
                    }
                    c = ((char)bits).ToString();
                    break;
                case 1:
                    bits = 0;
                    maxpower = 65536;
                    power = 1;
                    while (power != maxpower)
                    {
                        resb = data.Val & data.Position;
                        data.Position >>= 1;
                        if (data.Position == 0)
                        {
                            data.Position = resetValue;
                            data.Val = getNextValue(data.Index++);
                        }
                        bits |= (resb > 0 ? 1 : 0) * power;
                        power <<= 1;
                    }
                    c = ((char)bits).ToString();
                    break;
                case 2:
                    return "";
                default:
                    return "";
            }

            dictionary.Add(c);
            w = c;
            result.Append(c);

            while (true)
            {
                if (data.Index > length)
                {
                    return "";
                }

                bits = 0;
                maxpower = (int)Math.Pow(2, numBits);
                power = 1;
                while (power != maxpower)
                {
                    resb = data.Val & data.Position;
                    data.Position >>= 1;
                    if (data.Position == 0)
                    {
                        data.Position = resetValue;
                        data.Val = getNextValue(data.Index++);
                    }
                    bits |= (resb > 0 ? 1 : 0) * power;
                    power <<= 1;
                }

                int next = bits;
                switch (next)
                {
                    case 0:
                        bits = 0;
                        maxpower = 256;
                        power = 1;
                        while (power != maxpower)
                        {
                            resb = data.Val & data.Position;
                            data.Position >>= 1;
                            if (data.Position == 0)
                            {
                                data.Position = resetValue;
                                data.Val = getNextValue(data.Index++);
                            }
                            bits |= (resb > 0 ? 1 : 0) * power;
                            power <<= 1;
                        }

                        dictionary.Add(((char)bits).ToString());
                        next = dictSize++;
                        enlargeIn--;
                        break;
                    case 1:
                        bits = 0;
                        maxpower = 65536;
                        power = 1;
                        while (power != maxpower)
                        {
                            resb = data.Val & data.Position;
                            data.Position >>= 1;
                            if (data.Position == 0)
                            {
                                data.Position = resetValue;
                                data.Val = getNextValue(data.Index++);
                            }
                            bits |= (resb > 0 ? 1 : 0) * power;
                            power <<= 1;
                        }
                        dictionary.Add(((char)bits).ToString());
                        next = dictSize++;
                        enlargeIn--;
                        break;
                    case 2:
                        return result.ToString();
                }

                if (enlargeIn == 0)
                {
                    enlargeIn = (int)Math.Pow(2, numBits);
                    numBits++;
                }

                if (next < dictionary.Count && dictionary[next] != null)
                {
                    entry = dictionary[next];
                }
                else
                {
                    if (next == dictSize)
                    {
                        entry = w + w[0];
                    }
                    else
                    {
                        return "";
                    }
                }
                result.Append(entry);

                dictionary.Add(w + entry[0]);
                dictSize++;
                enlargeIn--;

                w = entry;

                if (enlargeIn == 0)
                {
                    enlargeIn = (int)Math.Pow(2, numBits);
                    numBits++;
                }
            }
        }

        private class DataWrapper
        {
            public int Val { get; set; }
            public int Position { get; set; }
            public int Index { get; set; }
        }

        #endregion
    }
}
