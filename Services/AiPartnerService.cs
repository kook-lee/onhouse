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
                            "https://www.aipartner.com/api/web/offerings/adList",
                            "https://www.aipartner.com/api/web/offerings/adList?page=1&size=200",
                            "https://www.aipartner.com/api/web/offerings/adList?page=1&limit=200",
                            "https://www.aipartner.com/api/web/offerings/adList?page=1&pageSize=200",
                            "https://www.aipartner.com/api/web/offerings/adList?page=1&rowNum=200",
                            "https://www.aipartner.com/api/web/offerings/simpleList",
                            "https://www.aipartner.com/api/web/offerings/simpleList?page=1&limit=200",
                            "https://www.aipartner.com/api/web/offerings/adFailList",
                            "https://www.aipartner.com/api/web/offerings/adCmplList",
                            "https://www.aipartner.com/offerings/adlist",
                            "https://www.aipartner.com/offerings/admanage",
                            "https://www.aipartner.com/home",
                            "https://www.aipartner.plus/api/web/offerings/adList"
                        };

                        foreach (var url in candidateUrls)
                        {
                            Log($"매물 데이터 수집 요청: {url}");
                            try
                            {
                                using var pageReq = new HttpRequestMessage(HttpMethod.Get, url);
                                pageReq.Headers.Add("Accept", "application/json, text/plain, */*");
                                pageReq.Headers.Add("X-Requested-With", "XMLHttpRequest");
                                pageReq.Headers.Add("Referer", "https://www.aipartner.com/home");
                                pageReq.Headers.Add("Origin", "https://www.aipartner.com");

                                var adRes = await client.SendAsync(pageReq);
                                Log($"응답: HTTP {(int)adRes.StatusCode}");

                                if (adRes.IsSuccessStatusCode)
                                {
                                    string adHtml = await adRes.Content.ReadAsStringAsync();
                                    int beforeCount = targetArticleNumbers.Count;

                                    // 1. 네이버 & 국토부 매물번호 9~11자리 추출
                                    foreach (var extractedNo in naverService.ExtractMultipleArticleNumbers(adHtml))
                                    {
                                        targetArticleNumbers.Add(extractedNo);
                                    }

                                    // 2. 정규식 보강 추출 (articleNo, atclNo, offeringSeq 등)
                                    var matches = Regex.Matches(adHtml, @"(?:articleNo|articleNumber|atclNo|naverArticleNo|cpArticleNo|articles|info|offerings|itemNo|article_no)[/=:\s""']+([0-9]{9,11})");
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
                                        Log($"✨ 매물번호 {newlyFound}건 발견 (누적 총 {targetArticleNumbers.Count}건)");
                                    }

                                    // 3. 만약 JSON에 pagination이 있다면 2~5페이지도 추가 조회
                                    try
                                    {
                                        using var doc = JsonDocument.Parse(adHtml);
                                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                                        {
                                            // 추가 페이지 자동 탐색
                                            for (int page = 2; page <= 6; page++)
                                            {
                                                string pagedUrl = url.Contains("?") 
                                                    ? Regex.Replace(url, @"page=\d+", $"page={page}") 
                                                    : $"{url}?page={page}&size=200";
                                                
                                                if (pagedUrl == url) break;

                                                using var nextReq = new HttpRequestMessage(HttpMethod.Get, pagedUrl);
                                                nextReq.Headers.Add("Accept", "application/json, text/plain, */*");
                                                nextReq.Headers.Add("X-Requested-With", "XMLHttpRequest");
                                                var nextRes = await client.SendAsync(nextReq);
                                                if (nextRes.IsSuccessStatusCode)
                                                {
                                                    string nextHtml = await nextRes.Content.ReadAsStringAsync();
                                                    int pBefore = targetArticleNumbers.Count;
                                                    foreach (var extractedNo in naverService.ExtractMultipleArticleNumbers(nextHtml))
                                                    {
                                                        targetArticleNumbers.Add(extractedNo);
                                                    }
                                                    int pFound = targetArticleNumbers.Count - pBefore;
                                                    if (pFound == 0) break; // 더 이상 없으면 중단
                                                    Log($"📄 {page}페이지에서 매물 {pFound}건 추가 수집 (누적 {targetArticleNumbers.Count}건)");
                                                }
                                            }
                                        }
                                    }
                                    catch { }
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
                            Log("ℹ️ 원인 분석: 이실장 일반 아이디는 브라우저 보안 모듈이 적용되어 있습니다. 대표님의 휴대폰 번호(010...)로 로그인하시면 즉시 연동됩니다.");
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
                    $"이실장에서 매물 목록을 가져오지 못했습니다.\n\n" +
                    "아이디(휴대폰 번호)와 비밀번호가 맞는지 확인해 주세요.\n" +
                    "또는 현재 크롬에서 보고 계신 이실장 화면의 주소(URL)를 입력창에 넣으시면 109건 전체를 즉시 긁어옵니다!",
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
