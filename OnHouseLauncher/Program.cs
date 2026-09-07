using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace OnHouseLauncher
{
    class Program
    {
        private const string AppExeName = "OnHouseLocal.exe";
        private const string VersionFileName = "version.txt";
        private const string GitHubApiUrl = "https://api.github.com/repos/kook-lee/onhouse/releases/latest";
        private const string OciUpdateUrl = "http://134.185.122.169/updates/version.json";

        static async Task Main(string[] args)
        {
            Console.Title = "OnHouse 스마트 자동 업데이트 런처";
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            PrintHeader();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string targetExePath = Path.Combine(baseDir, AppExeName);
            string versionFilePath = Path.Combine(baseDir, VersionFileName);

            string currentVersion = GetCurrentVersion(versionFilePath, targetExePath);
            Console.WriteLine($"📌 현재 설치 버전: v{currentVersion}");

            bool updateSuccessful = false;

            try
            {
                Console.Write("🔍 최신 업데이트 버전 확인 중... ");
                var updateInfo = await CheckForUpdatesAsync(currentVersion);

                if (updateInfo != null && updateInfo.HasUpdate)
                {
                    Console.WriteLine("\n");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"✨ 새로운 버전(v{updateInfo.NewVersion})이 출시되었습니다!");
                    Console.ResetColor();

                    if (!string.IsNullOrEmpty(updateInfo.ReleaseNotes))
                    {
                        Console.WriteLine($"📝 업데이트 내용: {updateInfo.ReleaseNotes}\n");
                    }

                    KillRunningApp();

                    Console.WriteLine("📥 최신 프로그램 다운로드를 시작합니다...");
                    string tempDownloadPath = Path.Combine(baseDir, $"{AppExeName}.tmp");

                    bool downloaded = await DownloadWithProgressAsync(updateInfo.DownloadUrl, tempDownloadPath);

                    if (downloaded && File.Exists(tempDownloadPath))
                    {
                        string backupPath = Path.Combine(baseDir, $"{AppExeName}.bak");
                        try { if (File.Exists(backupPath)) File.Delete(backupPath); } catch { }
                        try { if (File.Exists(targetExePath)) File.Move(targetExePath, backupPath); } catch { }

                        File.Move(tempDownloadPath, targetExePath);
                        File.WriteAllText(versionFilePath, updateInfo.NewVersion);

                        try { if (File.Exists(backupPath)) File.Delete(backupPath); } catch { }

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n🎉 업데이트 적용이 성공적으로 완료되었습니다!");
                        Console.ResetColor();
                        updateSuccessful = true;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\n⚠️ 다운로드에 실패하여 기존 버전으로 시작합니다.");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("최신 버전입니다! (업데이트 불필요)");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n⚠️ 업데이트 확인 실패 (오프라인 모드): {ex.Message}");
                Console.WriteLine("기존 설치된 프로그램으로 바로 시작합니다.");
                Console.ResetColor();
            }

            // 프로그램 실행
            LaunchMainApp(targetExePath);
        }

        private static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================================");
            Console.WriteLine("🏢 OnHouse 스마트 런처 & 자동 업데이터");
            Console.WriteLine("   (GitHub CI/CD 및 OCI 클라우드 자동 동기화)");
            Console.WriteLine("===============================================================");
            Console.ResetColor();
        }

        private static string GetCurrentVersion(string versionFilePath, string exePath)
        {
            if (File.Exists(versionFilePath))
            {
                string v = File.ReadAllText(versionFilePath).Trim();
                if (!string.IsNullOrEmpty(v)) return v.TrimStart('v', 'V');
            }

            if (File.Exists(exePath))
            {
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrEmpty(vi.FileVersion)) return vi.FileVersion;
                }
                catch { }
            }

            return "1.0.0";
        }

        private static void KillRunningApp()
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName("OnHouseLocal"))
                {
                    try
                    {
                        Console.WriteLine("기존 실행 중인 OnHouse 프로그램을 종료하는 중...");
                        proc.Kill();
                        proc.WaitForExit(2000);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static async Task<UpdateInfo?> CheckForUpdatesAsync(string currentVersion)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(6);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OnHouseLauncher", "1.0"));

            // 1. GitHub Releases API 확인 (Primary)
            try
            {
                var response = await client.GetAsync(GitHubApiUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string tag = root.GetProperty("tag_name").GetString() ?? "";
                    string cleanTag = tag.TrimStart('v', 'V');
                    string body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

                    string downloadUrl = "";
                    if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            string name = asset.GetProperty("name").GetString() ?? "";
                            if (name.Equals(AppExeName, StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        downloadUrl = $"https://github.com/kook-lee/onhouse/releases/download/{tag}/{AppExeName}";
                    }

                    if (IsNewerVersion(cleanTag, currentVersion))
                    {
                        return new UpdateInfo
                        {
                            HasUpdate = true,
                            NewVersion = cleanTag,
                            DownloadUrl = downloadUrl,
                            ReleaseNotes = body.Length > 150 ? body.Substring(0, 150) + "..." : body
                        };
                    }
                    else
                    {
                        return new UpdateInfo { HasUpdate = false };
                    }
                }
            }
            catch { }

            // 2. OCI VPS 확인 (Fallback)
            try
            {
                var ociResponse = await client.GetAsync(OciUpdateUrl);
                if (ociResponse.IsSuccessStatusCode)
                {
                    string ociJson = await ociResponse.Content.ReadAsStringAsync();
                    using var ociDoc = JsonDocument.Parse(ociJson);
                    var root = ociDoc.RootElement;

                    string version = root.GetProperty("version").GetString() ?? "";
                    string cleanVersion = version.TrimStart('v', 'V');
                    string downloadUrl = root.GetProperty("downloadUrl").GetString() ?? "";
                    string notes = root.TryGetProperty("releaseNotes", out var n) ? n.GetString() ?? "" : "";

                    if (IsNewerVersion(cleanVersion, currentVersion))
                    {
                        return new UpdateInfo
                        {
                            HasUpdate = true,
                            NewVersion = cleanVersion,
                            DownloadUrl = downloadUrl,
                            ReleaseNotes = notes
                        };
                    }
                    else
                    {
                        return new UpdateInfo { HasUpdate = false };
                    }
                }
            }
            catch { }

            return null;
        }

        private static bool IsNewerVersion(string remote, string local)
        {
            try
            {
                Version vRemote = Version.Parse(remote);
                Version vLocal = Version.Parse(local);
                return vRemote > vLocal;
            }
            catch
            {
                return string.Compare(remote, local, StringComparison.OrdinalIgnoreCase) > 0;
            }
        }

        private static async Task<bool> DownloadWithProgressAsync(string downloadUrl, string destinationPath)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(3);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OnHouseLauncher", "1.0"));

            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return false;

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            int lastPercent = -1;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fs.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    int percent = (int)((totalRead * 100) / totalBytes);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        DrawProgressBar(percent, totalRead, totalBytes);
                    }
                }
                else
                {
                    Console.Write($"\r다운로드 중: {totalRead / (1024 * 1024):F1} MB");
                }
            }

            Console.WriteLine();
            return true;
        }

        private static void DrawProgressBar(int percent, long current, long total)
        {
            int barWidth = 30;
            int filled = (percent * barWidth) / 100;
            string bar = new string('=', Math.Max(0, filled - 1)) + (filled > 0 ? ">" : "") + new string(' ', Math.Max(0, barWidth - filled));

            double currentMb = current / 1048576.0;
            double totalMb = total / 1048576.0;

            Console.Write($"\r진행률: [{bar}] {percent,3}% ({currentMb:F1}MB / {totalMb:F1}MB)");
        }

        private static void LaunchMainApp(string exePath)
        {
            if (!File.Exists(exePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ 오류: 프로그램 실행 파일({AppExeName})을 찾을 수 없습니다!");
                Console.WriteLine("엔터 키를 누르면 종료합니다...");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("\n🚀 OnHouse 프로그램을 실행합니다...");

            try
            {
                var p = new Process();
                p.StartInfo.FileName = exePath;
                p.StartInfo.WorkingDirectory = Path.GetDirectoryName(exePath) ?? "";
                p.StartInfo.UseShellExecute = true;
                p.Start();

                // 런처는 1.5초 후 깔끔하게 자동 종료
                Task.Delay(1500).Wait();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"실행 실패: {ex.Message}");
                Console.WriteLine("엔터 키를 누르면 종료합니다...");
                Console.ReadLine();
            }
        }

        private class UpdateInfo
        {
            public bool HasUpdate { get; set; }
            public string NewVersion { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public string ReleaseNotes { get; set; } = "";
        }
    }
}
