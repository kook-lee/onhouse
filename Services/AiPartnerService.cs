using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OnHouseLocal.Models;

namespace OnHouseLocal.Services
{
    public class AiPartnerListingItem
    {
        public string articleNumber { get; set; } = "";
        public string articleName { get; set; } = "";
        public string tradeType { get; set; } = "";
        public string rawPrice { get; set; } = "";
        public string priceDisplay { get; set; } = "";
        public string dong { get; set; } = "";
        public double exclusiveArea { get; set; }
        public double supplyArea { get; set; }
    }

    public class AiPartnerSyncResult
    {
        public bool success { get; set; }
        public string message { get; set; } = "";
        public List<string> articleNumbers { get; set; } = new();
        public List<AiPartnerListingItem> items { get; set; } = new();
        public List<string> logs { get; set; } = new();
    }
    public class AiPartnerService
    {
        public async Task<(bool success, string message, int extractedCount, int successCount, int skippedCount, int failedCount, List<string> errors, List<string> logs)> 
            LoginAndFetchListingsAsync(string memberId, string memberPw, string? agencyName, string? realtorInput, int userId, DatabaseService db, NaverLandService naverService)
        {
            var logs = new List<string>();
            void Log(string msg) => logs.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");

            Log($"이실장(aipartner.com) 연동 시도 시작 - 입력 아이디: {memberId}");

            // 1. 계정 정보 SQLite 로컬 암호화 보관
            await db.SaveRealtorSettingsAsync(new RealtorSettingsItem
            {
                UserId = userId,
                NaverId = memberId,
                NaverPassword = memberPw,
                AgencyName = agencyName ?? "",
                RealtorId = realtorInput ?? ""
            });
            Log("계정 정보 로컬 SQLite DB 암호화 저장 완료");

            var targetArticleNumbers = new HashSet<string>();

            // 2. 수동 입력된 링크/번호가 있으면 추출
            if (!string.IsNullOrWhiteSpace(realtorInput))
            {
                foreach (var artNo in naverService.ExtractMultipleArticleNumbers(realtorInput))
                {
                    targetArticleNumbers.Add(artNo);
                }
                if (targetArticleNumbers.Count > 0)
                {
                    Log($"수동 입력창에서 매물 번호 {targetArticleNumbers.Count}건 감지");
                }
            }

            // 3. 이실장 순수 C# 네이티브 보안 SSO 연동 및 전체 광고 매물 수집 (Node.js/외부 의존성 없음)
            Log("이실장 순수 C# 네이티브 보안 SSO 통신 시작...");
            try
            {
                using var handler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer(),
                    UseCookies = true,
                    AllowAutoRedirect = true
                };
                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");

                // 1) 게이트웨이 접속 및 CSRF 토큰 획득
                var loginPageRes = await client.GetAsync("https://www.aipartner.com/integrated/login?serviceCode=1000");
                string loginHtml = await loginPageRes.Content.ReadAsStringAsync();
                string csrfToken = Regex.Match(loginHtml, @"name=""csrf-token""\s+content=""([^""]+)""").Groups[1].Value;
                Log("이실장 게이트웨이 접속 성공");

                // 2) SSO 공개키 및 타임스탬프 획득
                using var pkReq = new HttpRequestMessage(HttpMethod.Get, "https://sso.aipartner.com/openapi/authentication/publickey/get");
                pkReq.Headers.Referrer = new Uri("https://www.aipartner.com/integrated/login?serviceCode=1000");
                var pkRes = await client.SendAsync(pkReq);
                string pkJson = await pkRes.Content.ReadAsStringAsync();
                using var pkDoc = JsonDocument.Parse(pkJson);
                string resultCodePk = pkDoc.RootElement.GetProperty("resultCode").GetString() ?? "";
                if (resultCodePk != "000000")
                {
                    string msg = pkDoc.RootElement.TryGetProperty("resultMessage", out var m) ? m.GetString() ?? "" : "공개키 획득 실패";
                    throw new Exception($"SSO 보안 공개키 수신 실패: {msg}");
                }
                string publicKey = pkDoc.RootElement.GetProperty("resultData").GetProperty("publicKey").GetString()!;
                string timeStamp = pkDoc.RootElement.GetProperty("resultData").GetProperty("timeStamp").GetString()!;
                Log("이실장 SSO 암호화 키 협상 완료");

                // 3) 순수 C# BouncyCastle IssacWeb 하이브리드 암호화 (SEED-CBC + RSA-OAEP)
                string authPayload = $"id={Uri.EscapeDataString(memberId)}&pw={Uri.EscapeDataString(memberPw)}&timeStamp={Uri.EscapeDataString(timeStamp)}";
                string issacwebData = IssacWebCrypto.HybridEncrypt(authPayload, publicKey);

                // 4) 로그인 정보 세션 임시 보관
                var saveForm = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["requestPage"] = "https://www.aipartner.com/home",
                    ["serviceCode"] = "1000",
                    ["formData[member-id]"] = memberId,
                    ["formData[member-pw]"] = memberPw,
                    ["formData[agentId]"] = "100",
                    ["formData[serviceCode]"] = "1000",
                    ["formData[loginCode]"] = "1",
                    ["formData[requestPage]"] = "https://www.aipartner.com/home",
                    ["_token"] = csrfToken
                });
                using var saveReq = new HttpRequestMessage(HttpMethod.Post, "https://www.aipartner.com/api/web/integrated/login-info-save") { Content = saveForm };
                saveReq.Headers.Add("Origin", "https://www.aipartner.com");
                saveReq.Headers.Add("X-CSRF-TOKEN", csrfToken);
                await client.SendAsync(saveReq);

                // 5) SSO 본인인증 실행
                var procForm = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["agentId"] = "100",
                    ["loginCode"] = "1",
                    ["issacwebData"] = issacwebData,
                    ["serviceCode"] = "1000"
                });
                using var procReq = new HttpRequestMessage(HttpMethod.Post, "https://sso.aipartner.com/authentication/issacweb/loginProcess") { Content = procForm };
                procReq.Headers.Add("Origin", "https://www.aipartner.com");
                var procRes = await client.SendAsync(procReq);
                string procText = await procRes.Content.ReadAsStringAsync();

                string resultCode = Regex.Match(procText, @"id=""resultCode""[^>]*value=""([^""]*)""").Groups[1].Value;
                string resultMsg = Regex.Match(procText, @"id=""resultMessage""[^>]*value=""([^""]*)""").Groups[1].Value;
                string secureToken = Regex.Match(procText, @"id=""secureToken""[^>]*value=""([^""]*)""").Groups[1].Value;
                string secureSessionId = Regex.Match(procText, @"id=""secureSessionId""[^>]*value=""([^""]*)""").Groups[1].Value;

                if (resultCode != "000000")
                {
                    Log($"❌ 이실장 로그인 실패: [{resultCode}] {resultMsg}");
                    return (false, string.IsNullOrEmpty(resultMsg) ? "이실장 로그인에 실패했습니다. 아이디 또는 비밀번호를 확인해주세요." : resultMsg, 0, 0, 0, 0, new List<string>(), logs);
                }

                Log("🎉 이실장 SSO 본인인증 성공! 보안 토큰 발급 완료");

                // 6) checkAuth
                var finishForm = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["resultCode"] = resultCode,
                    ["resultMessage"] = resultMsg,
                    ["secureToken"] = secureToken,
                    ["secureSessionId"] = secureSessionId,
                    ["userId"] = memberId,
                    ["agentId"] = "100"
                });
                using var finishReq = new HttpRequestMessage(HttpMethod.Post, "https://www.aipartner.com/api/web/sso/checkAuth") { Content = finishForm };
                finishReq.Headers.Add("Origin", "https://sso.aipartner.com");
                await client.SendAsync(finishReq);

                // 7) saveToken
                var tokenForm = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["agentId"] = "100",
                    ["resultCode"] = "000000",
                    ["secureSessionId"] = secureSessionId
                });
                using var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://sso.aipartner.com/token/saveToken.html") { Content = tokenForm };
                await client.SendAsync(tokenReq);

                // 8) agentProc (세션 최종 확정)
                using var agentReq = new HttpRequestMessage(HttpMethod.Post, "https://www.aipartner.com/api/web/sso/agentProc");
                await client.SendAsync(agentReq);

                Log("이실장 포털 정식 세션 쿠키 수령 완료");

                // 9) 다중 페이지 광고 매물 크롤링 및 파싱
                var seenNos = new HashSet<string>();
                int totalExtracted = 0;

                for (int page = 1; page <= 10; page++)
                {
                    string pageUrl = $"https://www.aipartner.com/offerings/ad_list?adName=ad&page={page}";
                    var pageRes = await client.GetAsync(pageUrl);
                    string html = await pageRes.Content.ReadAsStringAsync();

                    var rows = Regex.Matches(html, @"<tr[^>]*>[\s\S]*?</tr>");
                    int pageCount = 0;

                    foreach (Match rMatch in rows)
                    {
                        string r = rMatch.Value;
                        var mNo = Regex.Match(r, @"<div class=""numberN""[^>]*>[\s\S]*?([0-9]{9,11})[\s\S]*?</div>");
                        if (!mNo.Success) mNo = Regex.Match(r, @"data-seq=[""']([0-9]{9,11})[""']");
                        if (!mNo.Success) continue;

                        string artNo = mNo.Groups[1].Value;
                        if (seenNos.Contains(artNo)) continue;
                        seenNos.Add(artNo);

                        string name = Regex.Match(r, @"<span[^>]*class=[""']pre-wrap[""'][^>]*>([\s\S]*?)</span>").Groups[1].Value.Trim();
                        name = Regex.Replace(name, @"\s+", " ");

                        string dealType = Regex.Match(r, @"<span[^>]*class=[""']dealType[""'][^>]*>([\s\S]*?)</span>").Groups[1].Value.Trim();
                        string rawPrice = Regex.Match(r, @"<span[^>]*class=[""']price[""'][^>]*>([\s\S]*?)</span>").Groups[1].Value.Trim();
                        string dong = Regex.Match(r, @"<p[^>]*class=[""']dongInfo[""'][^>]*>([\s\S]*?)</p>").Groups[1].Value.Trim();

                        double exArea = 0;
                        var mEx = Regex.Match(r, @"data-gu=""\[전\]""[^>]*data-value=""([^""]+)""");
                        if (mEx.Success) double.TryParse(mEx.Groups[1].Value, out exArea);

                        double spArea = 0;
                        var mSp = Regex.Match(r, @"data-gu=""\[공\]""[^>]*data-value=""([^""]+)""");
                        if (mSp.Success) double.TryParse(mSp.Groups[1].Value, out spArea);

                        string displayPrice = FormatAdListPrice(dealType, rawPrice);

                        targetArticleNumbers.Add(artNo);
                        await db.UpsertNaverListingAsync(new NaverListingItem
                        {
                            UserId = userId,
                            ArticleNumber = artNo,
                            ArticleName = name,
                            TradeType = dealType,
                            PriceDisplay = displayPrice,
                            AreaM2 = exArea > 0 ? exArea : spArea,
                            Address = !string.IsNullOrEmpty(dong) ? $"서울 은평구 {dong}" : "",
                            LedgerStatus = "Pending",
                            LedgerMessage = "대장 검증 대기 중",
                            CreatedAt = DateTime.Now
                        });

                        pageCount++;
                        totalExtracted++;
                    }

                    if (pageCount > 0)
                    {
                        Log($"📄 광고 매물 {page}페이지 조회: {pageCount}건 확인 (누적 {totalExtracted}건)");
                    }

                    if (pageCount == 0 && page > 2) break;
                }

                Log($"✨ 이실장 전체 광고 매물 수집 완료: 총 {totalExtracted}건");
            }
            catch (Exception ex)
            {
                Log($"❌ 네이티브 SSO 연동 예외 발생: {ex.Message}");
            }

            // 4. 수집된 매물이 아직 없는 경우
            if (targetArticleNumbers.Count == 0)
            {
                Log("⚠️ 수집된 매물 번호가 0건입니다.");
                return (false, 
                    $"이실장에서 매물 목록을 가져오지 못했습니다.\n\n" +
                    "아이디(휴대폰 번호)와 비밀번호가 맞는지 확인해 주세요.\n" +
                    "또는 현재 크롬에서 보고 계신 이실장 화면의 주소(URL)를 입력창에 넣으시면 매물 전체를 즉시 긁어옵니다!",
                    0, 0, 0, 0, new List<string>(), logs);
            }

            // 5. 네이버 & 건축물대장 서비스에 매물 일괄 등록 (기검수 매물은 건너뛰기)
            Log($"총 {targetArticleNumbers.Count}건 매물 정보 네이버 & 건축물대장 수집/대조 시작...");
            var (successCount, failedCount, skippedCount, errors) = 
                await naverService.RegisterMultipleArticlesAsync(new List<string>(targetArticleNumbers), userId, db, skipAlreadyAudited: true);

            Log($"완료: 신규 수집 {successCount}건, 기존 검수 완료 건너뜀 {skippedCount}건, 실패 {failedCount}건");

            string message = skippedCount > 0
                ? $"이실장 연동 성공! 총 {targetArticleNumbers.Count}건 중 신규 매물 {successCount}건 수집 완료 (이미 대장 검수 완료된 {skippedCount}건은 기존 결과 안전 보존)"
                : $"이실장 연동 성공! 총 {targetArticleNumbers.Count}건의 매물 스펙을 수집하여 등록했습니다.";

            return (true, message, targetArticleNumbers.Count, successCount, skippedCount, failedCount, errors, logs);
        }

        private static string FormatAdListPrice(string dealType, string rawPrice)
        {
            if (string.IsNullOrWhiteSpace(rawPrice)) return "";
            rawPrice = rawPrice.Trim();
            if (dealType == "월세")
            {
                return $"월세 {Regex.Replace(rawPrice, @"\s*/\s*", "/")}";
            }
            string cleaned = Regex.Replace(rawPrice, @"[^0-9]", "");
            if (long.TryParse(cleaned, out long num))
            {
                if (num >= 10000)
                {
                    long uk = num / 10000;
                    long man = num % 10000;
                    return man > 0 ? $"{dealType} {uk}억 {man:N0}만" : $"{dealType} {uk}억";
                }
                return $"{dealType} {num:N0}만";
            }
            return $"{dealType} {rawPrice}".Trim();
        }
    }
}
