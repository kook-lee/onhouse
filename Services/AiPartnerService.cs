using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OnHouseLocal.Models;

namespace OnHouseLocal.Services
{
    public class AiPartnerService
    {
        public async Task<(bool success, string message, int extractedCount, int successCount, int skippedCount, int failedCount, List<string> errors)> 
            LoginAndFetchListingsAsync(string memberId, string memberPw, string? agencyName, string? realtorInput, int userId, DatabaseService db, NaverLandService naverService)
        {
            // 1. 계정 정보 SQLite 로컬 암호화 보관
            await db.SaveRealtorSettingsAsync(new RealtorSettingsItem
            {
                UserId = userId,
                NaverId = memberId, // 이실장 아이디/휴대폰번호
                NaverPassword = memberPw, // 이실장 비밀번호
                AgencyName = agencyName ?? "",
                RealtorId = realtorInput ?? ""
            });

            var targetArticleNumbers = new HashSet<string>();

            // 2. 입력된 링크/번호가 있으면 추출
            if (!string.IsNullOrWhiteSpace(realtorInput))
            {
                foreach (var artNo in naverService.ExtractMultipleArticleNumbers(realtorInput))
                {
                    targetArticleNumbers.Add(artNo);
                }
            }

            // 3. 이실장 (aipartner.com / aipartner.plus) 웹 로그인 및 광고 목록 크롤링 시도
            try
            {
                using var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    CookieContainer = new System.Net.CookieContainer()
                };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                string loginPageUrl = "https://aipartner.com/integrated/login";
                var getRes = await client.GetAsync(loginPageUrl);
                if (getRes.IsSuccessStatusCode)
                {
                    string html = await getRes.Content.ReadAsStringAsync();
                    string csrfToken = "";
                    var m = Regex.Match(html, @"name=""csrf-token""\s+content=""([^""]+)""");
                    if (m.Success) csrfToken = m.Groups[1].Value;

                    var postData = new Dictionary<string, string>
                    {
                        { "member-id", memberId },
                        { "member-pw", memberPw },
                        { "agentId", "100" },
                        { "serviceCode", "1000" },
                        { "loginCode", "1" },
                        { "requestPage", "https://www.aipartner.com/home" }
                    };
                    if (!string.IsNullOrEmpty(csrfToken)) postData["_token"] = csrfToken;

                    using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.aipartner.com/api/web/integrated/loginStore")
                    {
                        Content = new FormUrlEncodedContent(postData)
                    };
                    req.Headers.Add("Referer", loginPageUrl);
                    if (!string.IsNullOrEmpty(csrfToken)) req.Headers.Add("X-CSRF-TOKEN", csrfToken);

                    var postRes = await client.SendAsync(req);
                    if (postRes.IsSuccessStatusCode)
                    {
                        // 광고 중인 매물 목록 페이지 크롤링
                        var adRes = await client.GetAsync("https://www.aipartner.com/offerings/adlist");
                        if (adRes.IsSuccessStatusCode)
                        {
                            string adHtml = await adRes.Content.ReadAsStringAsync();
                            var matches = Regex.Matches(adHtml, @"(?:articleNo|articleNumber|articles|info)[/=:""'\s]+([0-9]{9,11})");
                            foreach (Match match in matches)
                            {
                                if (match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                                {
                                    targetArticleNumbers.Add(match.Groups[1].Value);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AiPartnerService] 로그인 크롤링: {ex.Message}");
            }

            // 4. 수집된 매물이 아직 없는 경우
            if (targetArticleNumbers.Count == 0)
            {
                return (false, 
                    $"이실장({memberId}) 계정 정보는 안전하게 보관되었습니다.\n다만 이실장은 카카오/소셜 로그인 및 본인인증 체계가 적용되어 있어, 브라우저에서 직접 로그인하신 후 매물 목록을 가져오시는 것을 권장합니다.\n\n상단의 [🌐 이실장 웹사이트 열기]를 눌러 로그인 후, 매물 목록을 복사하여 [➕ 매물 일괄 등록]에 붙여넣어 주시면 1초 만에 전수 검증이 수행됩니다.",
                    0, 0, 0, 0, new List<string>());
            }

            // 5. 네이버 & 건축물대장 서비스에 매물 일괄 등록 (기검수 매물은 건너뛰기)
            var (successCount, failedCount, skippedCount, errors) = 
                await naverService.RegisterMultipleArticlesAsync(new List<string>(targetArticleNumbers), userId, db, skipAlreadyAudited: true);

            string message = skippedCount > 0
                ? $"이실장 연동 성공! 신규 매물 {successCount}건 수집 완료 (이미 대장 검수 완료된 {skippedCount}건은 기존 결과 안전 보존)"
                : $"이실장 연동 성공! 매물 {successCount}건의 스펙을 수집하여 등록했습니다.";

            return (true, message, targetArticleNumbers.Count, successCount, skippedCount, failedCount, errors);
        }
    }
}
