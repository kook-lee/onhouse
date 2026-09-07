using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using OnHouseLocal.Models;

namespace OnHouseLocal.Services
{
    public class CrawlerService
    {
        private readonly DatabaseService _db;

        // 피터팬 카페 (10322296) 핵심 카테고리/지역별 메뉴 목록
        private static readonly (int MenuId, string Category, string Region)[] PeterpanMenuList = new[]
        {
            // 원룸
            (3, "원룸", "관악구/동작구"),
            (2, "원룸", "강남구/서초구"),
            (49, "원룸", "영등포/구로/금천"),
            (5, "원룸", "마포구/용산구"),
            (48, "원룸", "강동구/송파구"),
            (50, "원룸", "강서구/양천구"),
            (4, "원룸", "서대문/은평구"),
            (51, "원룸", "광진구/중랑구"),
            (6, "원룸", "중구/종로/성북"),
            (69, "원룸", "동대문/성동구"),
            (7, "원룸", "강북/노원/도봉"),

            // 오피스텔
            (289, "오피스텔", "서울 월세"),
            (2605, "오피스텔", "서울 전세"),

            // 투룸/쓰리룸
            (72, "투룸", "동작/관악/영등포"),
            (71, "투룸", "강남/서초/송파"),
            (74, "투룸", "마포/은평/서대문"),
            (73, "투룸", "금천/구로/강서"),
            (76, "투룸", "성북/성동/광진/용산"),

            // 빌라/연립
            (2436, "빌라", "서울 월세"),
            (2440, "빌라", "서울 전세"),

            // 서울 전세
            (101, "전세", "동작/관악/영등포"),
            (100, "전세", "강남/서초/송파"),
            (103, "전세", "마포/은평/서대문"),

            // 상가 / 사무실
            (829, "상가", "동작/관악/영등포"),
            (828, "상가", "강남/서초/송파"),
            (841, "사무실", "동작/관악/영등포")
        };

        // 당근부동산 서울 전역 35개 핵심 직거래 지역 풀
        private static readonly string[] DaangnRegionList = new[]
        {
            // 관악 / 동작 / 금천 / 구로
            "서울특별시/관악구/신림동",
            "서울특별시/관악구/봉천동",
            "서울특별시/관악구/낙성대",
            "서울특별시/동작구/사당동",
            "서울특별시/동작구/상도동",
            "서울특별시/동작구/흑석동",
            "서울특별시/금천구/가산동",
            "서울특별시/금천구/독산동",
            "서울특별시/구로구/구로동",
            "서울특별시/구로구/신도림",

            // 강남 / 서초 / 송파 / 강동
            "서울특별시/강남구/역삼동",
            "서울특별시/강남구/논현동",
            "서울특별시/강남구/대치동",
            "서울특별시/강남구/삼성동",
            "서울특별시/서초구/서초동",
            "서울특별시/서초구/방배동",
            "서울특별시/서초구/양재동",
            "서울특별시/송파구/잠실동",
            "서울특별시/송파구/가락동",
            "서울특별시/송파구/문정동",
            "서울특별시/강동구/천호동",

            // 마포 / 용산 / 서대문 / 은평
            "서울특별시/마포구/서교동",
            "서울특별시/마포구/연남동",
            "서울특별시/마포구/망원동",
            "서울특별시/마포구/공덕동",
            "서울특별시/용산구/이태원동",
            "서울특별시/용산구/한남동",
            "서울특별시/서대문구/신촌동",
            "서울특별시/은평구/불광동",

            // 영등포 / 강서 / 양천
            "서울특별시/영등포구/당산동",
            "서울특별시/영등포구/영등포동",
            "서울특별시/영등포구/여의도동",
            "서울특별시/강서구/화곡동",
            "서울특별시/양천구/목동",

            // 성동 / 광진 / 동대문 / 성북 / 노원
            "서울특별시/성동구/성수동",
            "서울특별시/광진구/화양동",
            "서울특별시/광진구/자양동",
            "서울특별시/동대문구/회기동",
            "서울특별시/성북구/안암동",
            "서울특별시/노원구/공릉동"
        };

        public bool IsCrawling { get; private set; } = false;
        public string CurrentProgress { get; private set; } = "대기 중";
        public List<string> ProgressLogs { get; } = new List<string>();
        private readonly object _logLock = new object();

        public void AddLog(string msg)
        {
            lock (_logLock)
            {
                ProgressLogs.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
                if (ProgressLogs.Count > 300) ProgressLogs.RemoveAt(0);
            }
            CurrentProgress = msg;
            Console.WriteLine(msg);
        }

        public void StartCrawling()
        {
            IsCrawling = true;
            lock (_logLock)
            {
                ProgressLogs.Clear();
            }
            AddLog("실시간 직거래 매물 수집 작업이 시작되었습니다.");
        }

        public void StopCrawling(string finalMsg)
        {
            IsCrawling = false;
            AddLog(finalMsg);
        }

        public CrawlerService(DatabaseService db)
        {
            _db = db;
        }

        /// <summary>
        /// 피터팬 + 당근마켓 직거래 매물 정밀 수집 (daysLimit: 최근 N일 이내 매물만 수집, 기본 7일)
        /// </summary>
        public async Task<int> CrawlDirectDealsAsync(string defaultRegion = "서울", int daysLimit = 7)
        {
            StartCrawling();
            int insertedCount = 0;
            try
            {
                if (daysLimit <= 0 || daysLimit > 7) daysLimit = 7;
                long cutoffTimestamp = DateTimeOffset.UtcNow.AddDays(-daysLimit).ToUnixTimeMilliseconds();

                using var client = CreateClient();

                // ========== 1. 피터팬 카페 전용 API 직접 수집 (26개 카테고리) ==========
                AddLog($"[피터팬 수집 시작] 최근 {daysLimit}일 이내 매물 정밀 수집 중...");

                foreach (var (menuId, cat, reg) in PeterpanMenuList)
                {
                    try
                    {
                        await Task.Delay(80 + new Random().Next(40));
                        var deals = await FetchPeterpanMenuArticlesAsync(client, menuId, cat, reg, cutoffTimestamp);

                        AddLog($"[피터팬 수집: \"{cat} ({reg})\"] {deals.Count}건 파싱 완료");

                        foreach (var deal in deals)
                        {
                            if (await _db.InsertCrawledDealAsync(deal))
                                insertedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[피터팬 오류: Menu {menuId}] {ex.Message}");
                    }
                }

                // ========== 2. 당근부동산 (realty.daangn.com) 서울 35개 지역 실매물 수집 ==========
                AddLog($"[당근부동산 수집 시작] 서울 35개 지역 순수 직거래 수집 중 (최근 {daysLimit}일)...");

                foreach (var regPath in DaangnRegionList)
                {
                    try
                    {
                        await Task.Delay(80 + new Random().Next(40));
                        var deals = await FetchRealtyDaangnDealsAsync(client, regPath, cutoffTimestamp);

                        AddLog($"[당근 수집: \"{regPath.Split('/').Last()}\"] {deals.Count}건 파싱 완료");

                        foreach (var deal in deals)
                        {
                            if (await _db.InsertCrawledDealAsync(deal))
                                insertedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[당근 오류: {regPath}] {ex.Message}");
                    }
                }

                StopCrawling($"[수집 완료] 총 {insertedCount}건의 새로운 직거래 매물이 등록되었습니다!");
            }
            catch (Exception ex)
            {
                StopCrawling($"[수집 중단] 예외 발생: {ex.Message}");
                throw;
            }
            return insertedCount;
        }

        private HttpClient CreateClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = true,
                AllowAutoRedirect = true
            };

            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept-Language", "ko-KR,ko;q=0.9,en-US;q=0.8");
            client.Timeout = TimeSpan.FromSeconds(15);
            return client;
        }

        /// <summary>
        /// 피터팬 카페(10322296) 신버전 API 직접 호출 및 날짜 필터링
        /// </summary>
        private async Task<List<DanggeunDealItem>> FetchPeterpanMenuArticlesAsync(HttpClient client, int menuId, string category, string regionHint, long cutoffTimestamp)
        {
            var list = new List<DanggeunDealItem>();
            string apiUrl = $"https://apis.naver.com/cafe-web/cafe-boardlist-api/v1/cafes/10322296/menus/{menuId}/articles?page=1&perPage=25";

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                req.Headers.Add("Referer", $"https://cafe.naver.com/f-e/cafes/10322296/menus/{menuId}");
                req.Headers.Add("Origin", "https://cafe.naver.com");
                req.Headers.Add("Accept", "application/json, text/plain, */*");

                var res = await client.SendAsync(req);
                if (!res.IsSuccessStatusCode) return list;

                var jsonStr = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);

                if (!doc.RootElement.TryGetProperty("result", out var resultEl)) return list;
                if (!resultEl.TryGetProperty("articleList", out var articleListEl)) return list;

                foreach (var art in articleListEl.EnumerateArray())
                {
                    if (!art.TryGetProperty("item", out var item)) continue;

                    long writeTimestamp = item.TryGetProperty("writeDateTimestamp", out var wt) ? wt.GetInt64() : 0;
                    if (writeTimestamp > 0 && writeTimestamp < cutoffTimestamp)
                    {
                        continue; // 설정한 N일 이전 글은 스킵
                    }

                    int articleIdNum = item.TryGetProperty("articleId", out var ai) ? ai.GetInt32() : 0;
                    if (articleIdNum <= 0) continue;

                    string subject = item.TryGetProperty("subject", out var sb) ? sb.GetString() ?? "" : "";
                    string summary = item.TryGetProperty("summary", out var sm) ? sm.GetString() ?? "" : "";
                    string representImage = item.TryGetProperty("representImage", out var ri) ? ri.GetString() ?? "" : "";
                    string writerNickname = item.TryGetProperty("writerNickname", out var wn) ? wn.GetString() ?? "집주인/세입자" : "집주인/세입자";

                    string cleanTitle = CleanText(subject);
                    if (cleanTitle.Length < 3 || cleanTitle.Contains("구합니다") || cleanTitle.Contains("구해요")) continue;

                    var (deposit, rent, isJeonse, priceDisplay) = ParsePrice(cleanTitle, summary);

                    string fullText = $"{cleanTitle} {summary}";
                    string region = ExtractRegion(fullText, regionHint);

                    var imgList = new List<string>();
                    if (!string.IsNullOrWhiteSpace(representImage))
                        imgList.Add(representImage);

                    string articleUrl = $"https://cafe.naver.com/kig/{articleIdNum}";

                    string cleanSummary = CleanText(summary);
                    string body = !string.IsNullOrWhiteSpace(cleanSummary) ? cleanSummary : cleanTitle;

                    string desc = $"📝 [집주인/임차인 직거래 원문]\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                                  $"{body}\n\n" +
                                  $"📍 위치: 서울 {region} 인근\n" +
                                  $"💰 금액: {priceDisplay}\n" +
                                  $"📸 사진: {(imgList.Count > 0 ? "첨부 사진 있음" : "원문 사진 확인")}\n" +
                                  $"💡 피터팬 카페 원문 링크에서 상세 사진 및 연락처를 확인할 수 있습니다.";

                    DateTime detectTime = writeTimestamp > 0
                        ? DateTimeOffset.FromUnixTimeMilliseconds(writeTimestamp).LocalDateTime
                        : DateTime.Now;

                    list.Add(new DanggeunDealItem
                    {
                        ArticleId = $"kig_{articleIdNum}",
                        Title = cleanTitle,
                        Region = region,
                        PriceDisplay = priceDisplay,
                        Deposit = deposit,
                        MonthlyRent = rent,
                        Description = desc,
                        AuthorName = string.IsNullOrWhiteSpace(writerNickname) ? "집주인/임차인(직거래)" : writerNickname,
                        AuthorType = "🏠 피터팬 직거래",
                        ArticleUrl = articleUrl,
                        ImageUrl = string.Join(",", imgList),
                        DetectedAt = detectTime
                    });
                }
            }
            catch (Exception ex)
            {
                AddLog($"[피터팬 API 파싱 오류: Menu {menuId}] {ex.Message}");
            }

            return list;
        }

        /// <summary>
        /// 당근부동산(realty.daangn.com) 동별 매물 목록 및 상세(사진+본문) 파싱
        /// </summary>
        private async Task<List<DanggeunDealItem>> FetchRealtyDaangnDealsAsync(HttpClient client, string regionPath, long cutoffTimestamp)
        {
            var list = new List<DanggeunDealItem>();
            string mapUrl = $"https://realty.daangn.com/map/{regionPath}";

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, mapUrl);
                req.Headers.Add("Referer", "https://realty.daangn.com");

                var res = await client.SendAsync(req);
                if (!res.IsSuccessStatusCode) return list;

                var html = await res.Content.ReadAsStringAsync();
                var matches = Regex.Matches(html, @"href=""/articles/(?<id>\d+)""[^>]*>(?<title>[^<]+)</a>");
                string regionName = regionPath.Split('/').Last();

                int count = 0;
                foreach (Match m in matches)
                {
                    if (count >= 15) break; // 지역당 최대 15건까지 넉넉하게 수집!
                    count++;

                    string id = m.Groups["id"].Value;
                    string rawTitle = m.Groups["title"].Value.Trim();
                    string cleanTitle = CleanText(rawTitle);

                    if (cleanTitle.Length < 3) continue;

                    string articleUrl = $"https://realty.daangn.com/articles/{id}";
                    var (deposit, rent, isJeonse, parsedPrice) = ParsePrice(cleanTitle);

                    var imgList = new List<string>();
                    string articleDesc = "";
                    DateTime detectTime = DateTime.Now;

                    bool isBroker = false;
                    try
                    {
                        await Task.Delay(40);
                        var detailReq = new HttpRequestMessage(HttpMethod.Get, articleUrl);
                        detailReq.Headers.Add("Referer", "https://realty.daangn.com");
                        var detailRes = await client.SendAsync(detailReq);

                        if (detailRes.IsSuccessStatusCode)
                        {
                            var detailHtml = await detailRes.Content.ReadAsStringAsync();

                            // [날짜 필터링] publishedAt 추출 및 N일 이내 검증
                            var pubMatch = Regex.Match(detailHtml, @"""publishedAt""\s*:\s*""(?<date>[^""]+)""");
                            if (pubMatch.Success && DateTimeOffset.TryParse(pubMatch.Groups["date"].Value, out var pubDate))
                            {
                                if (pubDate.ToUnixTimeMilliseconds() < cutoffTimestamp)
                                {
                                    continue; // 설정한 N일 이전 매물은 스킵!
                                }
                                detectTime = pubDate.LocalDateTime;
                            }

                            var imgMatches = Regex.Matches(detailHtml, @"https://img\.kr\.gcp-karroter\.net/realty/realty/articles/[a-zA-Z0-9_\-\.]+");
                            foreach (Match im in imgMatches)
                            {
                                if (!imgList.Contains(im.Value)) imgList.Add(im.Value);
                            }

                            var descMatch = Regex.Match(detailHtml, @"property=""og:description""\s*content=""(?<desc>[^""]+)""");
                            if (descMatch.Success) articleDesc = CleanText(descMatch.Groups["desc"].Value);

                            // [핵심] 실제 매물 본문(desc/title) 내 중개업소 광고만 정확히 차단 (웹사이트 푸터 공통 링크 오판 방지)
                            string contentToCheck = $"{cleanTitle} {articleDesc}";
                            if (contentToCheck.Contains("공인중개사사무소") || contentToCheck.Contains("중개법인") ||
                                contentToCheck.Contains("소속공인중개사") || contentToCheck.Contains("중개보조원") ||
                                contentToCheck.Contains("대표공인중개사") || contentToCheck.Contains("등록번호"))
                            {
                                isBroker = true;
                            }
                        }
                    }
                    catch { }

                    if (isBroker) continue; // 본문에 중개사 정보가 있는 광고글만 건너뜁니다!

                    string body = !string.IsNullOrWhiteSpace(articleDesc) ? articleDesc : cleanTitle;

                    string desc = $"📝 [당근부동산 직거래 매물]\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                                  $"{body}\n\n" +
                                  $"📍 위치: 서울 {regionName} 인근\n" +
                                  $"💰 금액: {parsedPrice}\n" +
                                  $"📸 사진: {imgList.Count}장\n" +
                                  $"🥕 출처: 🥕 당근 직거래\n\n" +
                                  $"💡 당근 앱 또는 웹에서 이웃과 직접 채팅/상담으로 거래할 수 있습니다.";

                    list.Add(new DanggeunDealItem
                    {
                        ArticleId = $"realty_{id}",
                        Title = cleanTitle,
                        Region = regionName,
                        PriceDisplay = parsedPrice,
                        Deposit = deposit,
                        MonthlyRent = rent,
                        Description = desc,
                        AuthorName = "당근 이웃/집주인",
                        AuthorType = "🥕 당근 직거래",
                        ArticleUrl = articleUrl,
                        ImageUrl = string.Join(",", imgList),
                        DetectedAt = detectTime
                    });
                }
            }
            catch (Exception ex)
            {
                AddLog($"[당근부동산 오류: {regionPath}] {ex.Message}");
            }

            return list;
        }

        private string CleanText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var clean = Regex.Replace(raw, @"<[^>]+>", "");
            clean = Regex.Replace(clean, @"<mark>|</mark>", "");
            clean = clean.Replace("\\n", "\n").Replace("\\\"", "\"")
                         .Replace("\\u0026", "&").Replace("\\u002F", "/")
                         .Replace("\\u003C", "<").Replace("\\u003E", ">");
            clean = HttpUtility.HtmlDecode(clean);
            clean = Regex.Replace(clean, @"더 깊이 있는 검색을 위해 AI가.*", "", RegexOptions.Singleline);
            clean = Regex.Replace(clean, @"AI의 특성상 다소 부정확.*", "", RegexOptions.Singleline);
            return Regex.Replace(clean, @"[ ]+", " ").Trim();
        }

        private string ExtractRegion(string text, string hint = "")
        {
            var regions = new[] {
                "관악구","신림","봉천","서울대입구","낙성대","사당","동작구","강남","역삼","논현","서초","송파","잠실",
                "마포","홍대","신촌","영등포","당산","문래","구로","노원","공릉","성북","안암","광진","건대","화양",
                "자양","왕십리","성동","강서","화곡","양천","목동","동대문","회기","중랑","은평","용산","금천","가산",
                "종로","혜화","대학로","이태원","합정","망원","연남","상수","성수","뚝섬","천호","강동","미아","수유",
                "쌍문","도봉","상계","중계","중앙대","흑석","상도","방배","대림","신길"
            };
            foreach (var r in regions)
            {
                if (text.Contains(r)) return r;
            }
            if (!string.IsNullOrWhiteSpace(hint)) return hint.Split('/')[0];
            return "서울/수도권";
        }

        /// <summary>
        /// 정밀 금액 파서 (피터팬, 당근 등 모든 형태 지원)
        /// </summary>
        public static (int Deposit, int Rent, bool IsJeonse, string PriceDisplay) ParsePrice(string title, string content = "")
        {
            string full = $"{title} {content}";
            string c = full.Replace(",", "");

            // 1. 월세 X억 Y / Z or 월세 X,000 / Y or 월세 X / Y
            var mRent = Regex.Match(c, @"월세\s*(?<dep>\d+억(?:\s*\d+)?|\d+)\s*[/]\s*(?<rent>\d+)");
            if (mRent.Success)
            {
                string depStr = mRent.Groups["dep"].Value.Trim();
                int rent = int.Parse(mRent.Groups["rent"].Value);
                int dep = ParseMoneyValue(depStr);
                return (dep, rent, false, $"{depStr} / {rent}만");
            }

            // 2. 보증금 X / 월세 Y
            var depForm = Regex.Match(c, @"(?:월세\s*보증금|보증금|보)\s*[:：]?\s*(?<dep>\d+억(?:\s*\d+)?|\d+)(?:만|만원)?");
            var rentForm = Regex.Match(c, @"(?:월세\s*가격|월세|월)\s*[:：]?\s*(?<rent>\d+)(?:만|만원)?");
            if (depForm.Success && rentForm.Success)
            {
                string depStr = depForm.Groups["dep"].Value.Trim();
                int dep = ParseMoneyValue(depStr);
                int rent = int.Parse(rentForm.Groups["rent"].Value);
                return (dep, rent, false, $"{depStr} / {rent}만");
            }

            // 3. 전세 X억 Y or 전세 X만
            var mJeonse = Regex.Match(c, @"전세\s*[:：]?\s*(?<dep>\d+억(?:\s*\d+)?|\d+)(?:만|만원)?");
            if (mJeonse.Success)
            {
                string depStr = mJeonse.Groups["dep"].Value.Trim();
                int dep = ParseMoneyValue(depStr);
                return (dep, 0, true, $"전세 {depStr}만");
            }

            // 4. 슬래시 패턴: 1000/135 or 1.34억/4 or 300/37
            var slash = Regex.Match(c, @"(?<dep>\d+(?:\.\d+)?억|\d{2,5})\s*[-/]\s*(?<rent>\d{1,4})");
            if (slash.Success)
            {
                string depStr = slash.Groups["dep"].Value.Trim();
                int dep = ParseMoneyValue(depStr);
                int rent = int.Parse(slash.Groups["rent"].Value);
                return (dep, rent, false, $"{depStr} / {rent}만");
            }

            // 5. 매매
            var mSale = Regex.Match(c, @"매매\s*[:：]?\s*(?<price>\d+억(?:\s*\d+)?|\d+)(?:만|만원)?");
            if (mSale.Success)
            {
                string priceStr = mSale.Groups["price"].Value.Trim();
                int price = ParseMoneyValue(priceStr);
                return (price, 0, false, $"매매 {priceStr}만");
            }

            // 6. 단일 금액 (3000만 등)
            var single = Regex.Match(c, @"(\d+)(?:만|만원)");
            if (single.Success)
            {
                int v = int.Parse(single.Groups[1].Value);
                return v >= 3000 ? (v, 0, true, $"전세 {v}만") : (v, 45, false, $"{v} / 45만");
            }

            return (500, 45, false, "500 / 45만");
        }

        private static int ParseMoneyValue(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return 0;
            str = str.Replace(" ", "");

            if (str.Contains("억"))
            {
                var parts = str.Split('억');
                if (double.TryParse(parts[0], out double eok))
                {
                    int total = (int)(eok * 10000);
                    if (parts.Length > 1 && int.TryParse(parts[1], out int rem))
                        total += rem;
                    return total;
                }
            }

            if (int.TryParse(str, out int val))
                return val;

            return 0;
        }
    }
}
