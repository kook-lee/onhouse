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

            // 3. 이실장 (https://www.aipartner.com) 웹 로그인 및 광고 목록 크롤링 시도
            try
            {
                using var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    UseCookies = true,
                    CookieContainer = new System.Net.CookieContainer()
                };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                
                string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";
                client.DefaultRequestHeaders.Add("User-Agent", userAgent);
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "ko-KR,ko;q=0.9,en-US;q=0.8");

                string loginPageUrl = "https://www.aipartner.com/integrated/login?serviceCode=1000";
                Log($"이실장 로그인 게이트웨이 접속 중: {loginPageUrl}");

                var getRes = await client.GetAsync(loginPageUrl);
                Log($"로그인 페이지 응답: HTTP {(int)getRes.StatusCode} {getRes.ReasonPhrase}");

                if (getRes.IsSuccessStatusCode)
                {
                    string html = await getRes.Content.ReadAsStringAsync();
                    string csrfToken = "";
                    var m = Regex.Match(html, @"name=""csrf-token""\s+content=""([^""]+)""");
                    if (m.Success) csrfToken = m.Groups[1].Value;
                    
                    if (!string.IsNullOrEmpty(csrfToken))
                    {
                        Log($"보안 토큰(CSRF-TOKEN) 획득 완료: {csrfToken.Substring(0, Math.Min(8, csrfToken.Length))}...");
                    }
                    else
                    {
                        Log("CSRF 토큰 없음 - 세션 쿠키 기반으로 계속 시도");
                    }

                    string cleanId = memberId.Trim().Replace("-", "");
                    bool isPhone = Regex.IsMatch(cleanId, @"^01[0-9]{8,9}$");

                    if (isPhone)
                    {
                        Log($"휴대폰 계정({cleanId}) 감지 -> 이실장 인증 API(loginStore) 호출...");
                    }
                    else
                    {
                        Log($"일반 아이디({cleanId}) 감지 -> 이실장 인증 API(loginStore) 호출...");
                    }

                    var postData = new Dictionary<string, string>
                    {
                        { "member-id", cleanId },
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
                    req.Headers.Add("Origin", "https://www.aipartner.com");
                    req.Headers.Add("X-Requested-With", "XMLHttpRequest");
                    if (!string.IsNullOrEmpty(csrfToken)) req.Headers.Add("X-CSRF-TOKEN", csrfToken);

                    var postRes = await client.SendAsync(req);
                    Log($"로그인 요청 전송 -> 서버 응답: HTTP {(int)postRes.StatusCode}");

                    string postBody = await postRes.Content.ReadAsStringAsync();
                    bool loginSuccess = false;
                    string serverMsg = "";

                    try
                    {
                        using var doc = JsonDocument.Parse(postBody);
                        if (doc.RootElement.TryGetProperty("result", out var resElem))
                        {
                            loginSuccess = resElem.GetBoolean();
                        }
                        if (doc.RootElement.TryGetProperty("message", out var msgElem))
                        {
                            serverMsg = msgElem.GetString() ?? "";
                        }
                    }
                    catch
                    {
                        serverMsg = postBody.Length > 200 ? postBody.Substring(0, 200) : postBody;
                    }

                    if (loginSuccess)
                    {
                        Log("🎉 이실장 로그인 성공! 인증 세션 발급 완료.");
                        
                        string[] candidateUrls = new[]
                        {
                            "https://www.aipartner.com/offerings/adlist",
                            "https://www.aipartner.com/offerings/admanage",
                            "https://www.aipartner.com/home"
                        };

                        foreach (var url in candidateUrls)
                        {
                            Log($"매물 페이지 스캔: {url}");
                            try
                            {
                                var adRes = await client.GetAsync(url);
                                if (adRes.IsSuccessStatusCode)
                                {
                                    string adHtml = await adRes.Content.ReadAsStringAsync();
                                    var matches = Regex.Matches(adHtml, @"(?:articleNo|articleNumber|articles|info|offerings)[/=:""'\s]+([0-9]{9,11})");
                                    int beforeCount = targetArticleNumbers.Count;
                                    foreach (Match match in matches)
                                    {
                                        if (match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                                        {
                                            targetArticleNumbers.Add(match.Groups[1].Value);
                                        }
                                    }
                                    int newlyFound = targetArticleNumbers.Count - beforeCount;
                                    if (newlyFound > 0)
                                    {
                                        Log($"✨ {url} 에서 매물번호 {newlyFound}건 발견 (총 {targetArticleNumbers.Count}건)");
                                    }
                                }
                            }
                            catch (Exception crawlEx)
                            {
                                Log($"페이지 조회 알림: {crawlEx.Message}");
                            }
                        }
                    }
                    else
                    {
                        Log($"❌ 이실장 로그인 실패 응답: {serverMsg}");
                        if (!isPhone)
                        {
                            Log("ℹ️ 원인 분석: 이실장은 영문 아이디에 대해 보안 모듈(IssacWeb 암호화) 또는 브라우저 인증을 필수로 요구합니다.");
                        }
                    }
                }
                else
                {
                    Log($"❌ 이실장 서버 게이트웨이 접근 실패: HTTP {(int)getRes.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Log($"통신 중 예외 발생: {ex.Message}");
            }

            // 4. 수집된 매물이 아직 없는 경우
            if (targetArticleNumbers.Count == 0)
            {
                Log("⚠️ 수집된 매물 번호가 0건입니다.");
                return (false, 
                    $"이실장에서 매물을 자동 수집하지 못했습니다.\n\n" +
                    "💡 지금 바로 매물을 가져오는 가장 확실한 방법:\n" +
                    "대표님 브라우저에 띄워두신 AI실장 화면(광고중 109건)에서 매물 목록을 복사(Ctrl+C)하시거나, [⭐ 1초 전송 북마클릿]을 누르시면 온하우스로 109건이 1초 만에 전송됩니다!",
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
    }
}
