using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OnHouseLocal.Models;

namespace OnHouseLocal.Services
{
    public class AiPartnerSyncResult
    {
        public bool success { get; set; }
        public string message { get; set; } = "";
        public List<string> articleNumbers { get; set; } = new();
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

            // 3. 이실장 SSO 자동 로그인 및 전체 광고 매물 수집기 실행 (Node.js 기반)
            string baseDir = AppContext.BaseDirectory;
            string scriptPath = Path.Combine(baseDir, "Scripts", "aipartner", "sync.js");
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "aipartner", "sync.js");
            }

            bool syncSuccess = false;
            string syncMessage = "";

            if (File.Exists(scriptPath))
            {
                try
                {
                    Log("이실장 SSO 자동 연동 모듈 구동 중...");
                    var psi = new ProcessStartInfo
                    {
                        FileName = "node",
                        Arguments = $"\"{scriptPath}\" \"{memberId}\" \"{memberPw}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        string output = await proc.StandardOutput.ReadToEndAsync();
                        string error = await proc.StandardError.ReadToEndAsync();
                        await proc.WaitForExitAsync();

                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            try
                            {
                                var syncResult = JsonSerializer.Deserialize<AiPartnerSyncResult>(output);
                                if (syncResult != null)
                                {
                                    if (syncResult.logs != null)
                                    {
                                        foreach (var l in syncResult.logs) Log(l);
                                    }

                                    syncSuccess = syncResult.success;
                                    syncMessage = syncResult.message;

                                    if (syncResult.articleNumbers != null)
                                    {
                                        foreach (var artNo in syncResult.articleNumbers)
                                        {
                                            targetArticleNumbers.Add(artNo);
                                        }
                                    }
                                }
                            }
                            catch (Exception parseEx)
                            {
                                Log($"응답 파싱 안내: {parseEx.Message}");
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            Log($"보조 로그: {error.Trim()}");
                        }
                    }
                }
                catch (Exception procEx)
                {
                    Log($"SSO 모듈 실행 예외: {procEx.Message}");
                }
            }
            else
            {
                Log($"⚠️ 연동 스크립트를 찾을 수 없습니다: {scriptPath}");
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
    }
}
