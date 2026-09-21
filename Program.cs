using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OnHouseLocal.Models;
using OnHouseLocal.Services;

// [핵심] 기존 구버전 인스턴스 정리 및 구버전 캐시 DB 삭제
try
{
    var currentProc = Process.GetCurrentProcess();
    foreach (var p in Process.GetProcessesByName("OnHouseLocal"))
    {
        if (p.Id != currentProc.Id)
        {
            try { p.Kill(); p.WaitForExit(1000); } catch { }
        }
    }

    // 구버전 캐시 DB 파일 삭제
    string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OnHouseLocal");
    if (Directory.Exists(appData))
    {
        string oldDb1 = Path.Combine(appData, "onhouse.db");
        string oldDb2 = Path.Combine(appData, "realestate.db");
        try { if (File.Exists(oldDb1)) File.Delete(oldDb1); } catch { }
        try { if (File.Exists(oldDb2)) File.Delete(oldDb2); } catch { }
    }
}
catch { }

// [핵심] 네이티브 SQLite 초기화 보장
try { SQLitePCL.Batteries_V2.Init(); } catch { }

int port = 5000;

for (int p = 5000; p < 5100; p++)
{
    try
    {
        var listener = new TcpListener(IPAddress.Loopback, p);
        listener.Start();
        listener.Stop();
        port = p;
        break;
    }
    catch { }
}

Console.WriteLine("==================================================");
Console.WriteLine("🏠 OnHouse Local 부동산 매물 관리기 (보안 로그인)");
Console.WriteLine($"🌐 접속 주소: http://localhost:{port}");
Console.WriteLine("==================================================");

// 브라우저 자동 오픈
_ = Task.Run(async () =>
{
    await Task.Delay(1000);
    string targetUrl = $"http://localhost:{port}";
    
    try
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = $"/c start {targetUrl}";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.Start();
        }
        else
        {
            Process.Start(new ProcessStartInfo("xdg-open", targetUrl) { UseShellExecute = false });
        }
    }
    catch { }
});

try
{
    string baseDir = AppContext.BaseDirectory;
    string currentDir = Directory.GetCurrentDirectory();
    string targetWebRoot = Path.Combine(baseDir, "wwwroot");

    // [완전 단일 파일 지원] wwwroot 폴더가 없으면 exe 내부에 압축된 리소스를 자동으로 디스크에 복원
    try
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceNames = asm.GetManifestResourceNames();
        
        foreach (var rName in resourceNames)
        {
            if (rName.StartsWith("OnHouseLocal.wwwroot.", StringComparison.OrdinalIgnoreCase))
            {
                string relPath = rName.Substring("OnHouseLocal.wwwroot.".Length);
                
                // .js, .css, .html 확장자 처리 (네임스페이스 점(.)을 디렉토리 구분자로 복원)
                int lastDot = relPath.LastIndexOf('.');
                if (lastDot > 0)
                {
                    int extDot = relPath.LastIndexOf('.', lastDot - 1);
                    if (extDot > 0)
                    {
                        // 하위 폴더 (예: js.app.js -> js/app.js)
                        string dirPart = relPath.Substring(0, extDot).Replace('.', Path.DirectorySeparatorChar);
                        string filePart = relPath.Substring(extDot + 1);
                        relPath = Path.Combine(dirPart, filePart);
                    }
                }

                string outPath = Path.Combine(targetWebRoot, relPath);
                string? outDir = Path.GetDirectoryName(outPath);
                if (outDir != null && !Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                using var stream = asm.GetManifestResourceStream(rName);
                if (stream != null)
                {
                    using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
                    stream.CopyTo(fs);
                }
            }
            else if (rName.StartsWith("OnHouseLocal.Scripts.", StringComparison.OrdinalIgnoreCase))
            {
                string relPath = rName.Substring("OnHouseLocal.Scripts.".Length);
                int lastDot = relPath.LastIndexOf('.');
                if (lastDot > 0)
                {
                    int extDot = relPath.LastIndexOf('.', lastDot - 1);
                    if (extDot > 0)
                    {
                        string dirPart = relPath.Substring(0, extDot).Replace('.', Path.DirectorySeparatorChar);
                        string filePart = relPath.Substring(extDot + 1);
                        relPath = Path.Combine(dirPart, filePart);
                    }
                }

                string outPath = Path.Combine(baseDir, "Scripts", relPath);
                string? outDir = Path.GetDirectoryName(outPath);
                if (outDir != null && !Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                using var stream = asm.GetManifestResourceStream(rName);
                if (stream != null)
                {
                    using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
                    stream.CopyTo(fs);
                }
            }
        }
    }
    catch { }

    string webRoot = Directory.Exists(targetWebRoot) ? targetWebRoot : baseDir;

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = baseDir,
        WebRootPath = webRoot
    });

    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.Listen(IPAddress.Loopback, port);
    });

    builder.Services.AddSingleton<DatabaseService>();
    builder.Services.AddSingleton<AuthService>();
    builder.Services.AddSingleton<ContractGeneratorService>();
    builder.Services.AddSingleton<CrawlerService>();
    builder.Services.AddSingleton<FakeListingDetector>();
    builder.Services.AddSingleton<BuildingLedgerService>();
    builder.Services.AddSingleton<VWorldHousingPriceService>();
    builder.Services.AddSingleton<NaverLandService>();
    builder.Services.AddSingleton<AiPartnerService>();

    var app = builder.Build();

    app.UseDefaultFiles();
    app.UseStaticFiles();

    // 서버 시작 시 무한 반복 크롤링 제거 (사용자 직접 실행 전용)
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        Console.WriteLine("[OnHouse] 직거래 크롤링 서비스가 준비되었습니다. (수동 실행 모드)");
    });

    // --- AUTH APIs ---
    app.MapPost("/api/auth/register", async (RegisterRequest req, AuthService auth, DatabaseService db) =>
    {
        if (!auth.ValidateInviteCode(req.InviteCode))
        {
            return Results.BadRequest(new { message = "가입 승인 코드가 일치하지 않습니다. 올바른 승인 코드를 입력하세요." });
        }

        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.Name))
        {
            return Results.BadRequest(new { message = "아이디, 비밀번호, 성명은 필수 입력입니다." });
        }

        var existing = await db.GetUserByUsernameAsync(req.Username.Trim());
        if (existing != null)
        {
            return Results.BadRequest(new { message = "이미 사용 중인 아이디입니다." });
        }

        var (hash, salt) = auth.HashPassword(req.Password);
        var newUser = new UserItem
        {
            Username = req.Username.Trim(),
            PasswordHash = hash,
            Salt = salt,
            Name = req.Name.Trim(),
            AgencyName = req.AgencyName?.Trim() ?? "개인중개사",
            CreatedAt = DateTime.Now
        };

        int userId = await db.CreateUserAsync(newUser);
        return Results.Ok(new { message = "회원가입이 완료되었습니다!", userId });
    });

    app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth, DatabaseService db) =>
    {
        var user = await db.GetUserByUsernameAsync(req.Username.Trim());
        if (user == null || !auth.VerifyPassword(req.Password, user.PasswordHash, user.Salt))
        {
            return Results.BadRequest(new { message = "아이디 또는 비밀번호가 올바르지 않습니다." });
        }

        return Results.Ok(new
        {
            message = "로그인 성공",
            user = new
            {
                id = user.Id,
                username = user.Username,
                name = user.Name,
                agencyName = user.AgencyName
            }
        });
    });

    // --- PROPERTY APIs (내 장부) ---
    app.MapGet("/api/properties", async (int? userId, DatabaseService db, FakeListingDetector detector) => 
    {
        int uId = userId ?? 1;
        var list = await db.GetAllPropertiesAsync(uId);
        var customers = await db.GetCustomerRequestsAsync(uId);
        detector.MatchCustomersForProperties(list, customers);
        return Results.Ok(list);
    });

    app.MapPost("/api/properties/search", async (PropertyFilter filter, int? userId, DatabaseService db, FakeListingDetector detector) => 
    {
        int uId = userId ?? 1;
        var list = await db.SearchPropertiesAsync(filter, uId);
        var customers = await db.GetCustomerRequestsAsync(uId);
        detector.MatchCustomersForProperties(list, customers);
        return Results.Ok(list);
    });

    app.MapPost("/api/properties", async (PropertyItem item, int? userId, DatabaseService db) =>
    {
        if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Address))
            return Results.BadRequest(new { message = "제목과 주소는 필수입니다." });
        int id = await db.InsertPropertyAsync(item, userId ?? 1);
        item.Id = id;
        return Results.Created($"/api/properties/{id}", item);
    });

    app.MapPut("/api/properties/{id:int}", async (int id, PropertyItem item, int? userId, DatabaseService db) =>
    {
        item.Id = id;
        return (await db.UpdatePropertyAsync(item, userId ?? 1)) ? Results.Ok(item) : Results.NotFound();
    });

    app.MapDelete("/api/properties/{id:int}", async (int id, int? userId, DatabaseService db) =>
    {
        return (await db.DeletePropertyAsync(id, userId ?? 1)) ? Results.Ok() : Results.NotFound();
    });

    // --- 허위매물/과태료 방지 및 건축물대장 API ---
    app.MapGet("/api/ledger/check", async (string address, BuildingLedgerService ledgerService) =>
    {
        var ledger = await ledgerService.QueryBuildingLedgerAsync(address);
        return Results.Ok(ledger);
    });

    // --- [신규] 주소 기반 건축물대장(표제부/전유부) & 호별 대지지분 & 2026 공시가격 종합 즉시 조회 API ---
    app.MapGet("/api/ledger/lookup-full", async (
        string? address, 
        string? dong, 
        string? ho, 
        string? sigunguCd, 
        string? bjdongCd, 
        string? bun, 
        string? ji, 
        BuildingLedgerService ledgerService, 
        VWorldHousingPriceService vworldService) =>
    {
        if (string.IsNullOrWhiteSpace(address) && (string.IsNullOrWhiteSpace(sigunguCd) || string.IsNullOrWhiteSpace(bun)))
        {
            return Results.BadRequest(new { success = false, message = "조회할 주소 또는 시군구/번지 정보가 필요합니다." });
        }

        string rawAddress = address?.Trim() ?? "";
        string targetDong = dong?.Trim() ?? "";
        string targetHo = ho?.Trim() ?? "";

        // 주소 문자열 내에 동/호수가 포함된 경우 자동 파싱 (예: "불광동 486-17 1202호")
        if (string.IsNullOrEmpty(targetDong))
        {
            var dm = Regex.Match(rawAddress, @"(\d{1,4})\s*동");
            if (dm.Success) targetDong = dm.Groups[1].Value;
        }
        if (string.IsNullOrEmpty(targetHo))
        {
            var hm = Regex.Match(rawAddress, @"([1-9]\d{0,3})\s*호");
            if (hm.Success) targetHo = hm.Groups[1].Value;
        }

        BuildingLedgerInfo? ledger = null;
        string sCd = sigunguCd?.Trim() ?? "";
        string bCd = bjdongCd?.Trim() ?? "";
        string bNum = bun?.Trim() ?? "";
        string jNum = ji?.Trim() ?? "0000";

        if (!string.IsNullOrEmpty(sCd) && !string.IsNullOrEmpty(bCd) && !string.IsNullOrEmpty(bNum))
        {
            bNum = bNum.PadLeft(4, '0');
            jNum = jNum.PadLeft(4, '0');
            ledger = await ledgerService.QueryBuildingLedgerByCodesAsync(sCd, bCd, bNum, jNum, targetDong);
        }
        else
        {
            ledger = await ledgerService.QueryBuildingLedgerAsync(rawAddress, targetDong);
            sCd = ledger.SigunguCd;
            bCd = ledger.BjdongCd;
            bNum = ledger.Bun;
            jNum = ledger.Ji;
        }

        if (ledger == null || !ledger.Success)
        {
            return Results.Ok(new { success = false, message = ledger?.Message ?? "해당 주소의 건축물대장을 조회하지 못했습니다." });
        }

        // 호수가 지정된 경우 전유/공용 및 대지권 상세 연계
        if (!string.IsNullOrEmpty(targetHo))
        {
            await ledgerService.EnrichWithExposPubuseAreaAsync(ledger, sCd, bCd, bNum, jNum, targetHo, targetDong);
        }

        // VWorld 공동주택 공시가격 및 HUG 126% 산출
        string pnu = BuildingLedgerService.BuildPnu(sCd, bCd, bNum, jNum);
        var priceInfo = await vworldService.QueryApartmentPriceAsync(pnu, targetDong, targetHo, ledger.UnitExclusiveArea);

        // UI 모달(openLedgerFullModal)과 100% 완벽 호환되는 NaverListingItem 구성
        string cleanD = !string.IsNullOrEmpty(targetDong) ? (targetDong.EndsWith("동") ? targetDong : $"{targetDong}동") : "";
        string cleanH = !string.IsNullOrEmpty(targetHo) ? (targetHo.EndsWith("호") ? targetHo : $"{targetHo}호") : "";
        string floorInfoStr = !string.IsNullOrEmpty(cleanH)
            ? (!string.IsNullOrEmpty(cleanD) ? $"{cleanD} {cleanH}" : cleanH)
            : (!string.IsNullOrEmpty(cleanD) ? cleanD : $"지상 {ledger.GrndFlrCnt}층");

        var item = new NaverListingItem
        {
            Id = 0,
            ArticleNumber = "주소 직접조회",
            ArticleName = !string.IsNullOrEmpty(ledger.BuildingName.Trim()) ? ledger.BuildingName : (!string.IsNullOrEmpty(rawAddress) ? rawAddress : "건축물대장"),
            Address = !string.IsNullOrEmpty(ledger.PlatAddress) ? ledger.PlatAddress : rawAddress,
            FloorInfo = floorInfoStr,
            PlatArea = ledger.PlatArea,
            ArchArea = ledger.ArchArea,
            TotArea = ledger.TotArea,
            BcRat = ledger.BcRat,
            VlRat = ledger.VlRat,
            BuildingStructure = ledger.Structure,
            LedgerStatus = ledger.IsViolatingBuilding ? "Danger" : (ledger.IsSuspiciousViolation ? "Warning" : "Safe"),
            IsViolatingBuilding = ledger.IsViolatingBuilding,
            LedgerMessage = !string.IsNullOrEmpty(ledger.ViolationSuspicionReason) ? ledger.ViolationSuspicionReason : ledger.Message,
            PublicPrice = priceInfo.PublicPrice,
            PublicPriceYear = priceInfo.BaseYear,
            HugGuaranteeLimit = priceInfo.HugGuaranteeLimit,
            LedgerRawJson = ledger.RawJson,
            ApprovalDate = ledger.UseApprovalDate
        };

        return Results.Ok(new
        {
            success = true,
            item = item,
            ledger = ledger,
            priceInfo = priceInfo
        });
    });

    app.MapPost("/api/properties/audit", async (PropertyItem item, BuildingLedgerService ledgerService) =>
    {
        BuildingLedgerInfo? ledger = null;
        if (!string.IsNullOrWhiteSpace(item.Address))
        {
            ledger = await ledgerService.QueryBuildingLedgerAsync(item.Address);
        }
        var audit = ledgerService.AuditPropertySafety(item, ledger);
        return Results.Ok(audit);
    });

    // --- 네이버 매물 URL 검증 및 건축물대장 / 공시가격 대조 API ---
    app.MapPost("/api/naver/inspect", async (NaverInspectRequest req, NaverLandService naverService, BuildingLedgerService ledgerService, VWorldHousingPriceService vworldService) =>
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Url))
        {
            return Results.BadRequest(new { message = "네이버 매물 URL 또는 매물번호를 입력해주세요." });
        }
        var result = await naverService.InspectAndCompareAsync(req.Url, ledgerService, vworldService);
        return Results.Ok(result);
    });

    // --- VWorld 공동주택 공시가격 & HUG 126% 산출 API ---
    app.MapGet("/api/vworld/housing-price", async (string? pnu, string? address, string? dong, string? ho, double? area, VWorldHousingPriceService vworldService) =>
    {
        if (!string.IsNullOrEmpty(pnu))
        {
            var res = await vworldService.QueryApartmentPriceAsync(pnu, dong ?? "", ho ?? "", area ?? 0);
            return Results.Ok(res);
        }
        else if (!string.IsNullOrEmpty(address))
        {
            var res = await vworldService.QueryApartmentPriceByAddressAsync(address, dong ?? "", ho ?? "", area ?? 0);
            return Results.Ok(res);
        }
        return Results.BadRequest(new { message = "pnu 또는 address 파라미터가 필요합니다." });
    });



    // 매물 상태 즉시 변경 API
    app.MapPut("/api/properties/{id:int}/status", async (int id, StatusUpdateRequest req, int? userId, DatabaseService db) =>
    {
        try
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Status))
            {
                Console.WriteLine($"[API Error] StatusUpdateRequest is null or status is empty for id={id}");
                return Results.BadRequest(new { message = "상태 값이 누락되었거나 비어 있습니다." });
            }

            int uId = userId ?? 1;
            bool ok = await db.UpdatePropertyStatusAsync(id, req.Status, uId);
            if (!ok)
            {
                Console.WriteLine($"[API Warning] UpdatePropertyStatusAsync returned false. id={id}, status={req.Status}, userId={uId}");
                return Results.NotFound(new { message = "매물이 존재하지 않거나 해당 사용자의 매물이 아닙니다." });
            }

            return Results.Ok(new { success = true, status = req.Status });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API Exception] 매물 상태 변경 실패: {ex.Message}\n{ex.StackTrace}");
            return Results.Json(new { message = ex.Message, stackTrace = ex.StackTrace }, statusCode: 500);
        }
    });

    // 통화 이력 목록 및 등록 API
    app.MapGet("/api/properties/{id:int}/contact-logs", async (int id, DatabaseService db) =>
    {
        var logs = await db.GetContactLogsAsync(id);
        return Results.Ok(logs);
    });

    app.MapPost("/api/properties/{id:int}/contact-logs", async (int id, ContactLogItem log, int? userId, DatabaseService db) =>
    {
        log.PropertyId = id;
        log.ContactDate = DateTime.Now;
        bool ok = await db.AddContactLogAsync(log, userId ?? 1);
        return ok ? Results.Ok(log) : Results.BadRequest();
    });

    // --- CUSTOMER APIs (손님 조건 관리) ---
    app.MapGet("/api/customers", async (int? userId, DatabaseService db) =>
    {
        var list = await db.GetCustomerRequestsAsync(userId ?? 1);
        return Results.Ok(list);
    });

    app.MapPost("/api/customers", async (CustomerRequestItem customer, int? userId, DatabaseService db) =>
    {
        customer.UserId = userId ?? 1;
        customer.CreatedAt = DateTime.Now;
        bool ok = await db.AddCustomerRequestAsync(customer);
        return ok ? Results.Ok(customer) : Results.BadRequest();
    });

    app.MapDelete("/api/customers/{id:int}", async (int id, int? userId, DatabaseService db) =>
    {
        bool ok = await db.DeleteCustomerRequestAsync(id, userId ?? 1);
        return ok ? Results.Ok() : Results.NotFound();
    });

    app.MapPost("/api/tools/generate", (PropertyItem item, ContractGeneratorService contractService) =>
    {
        return Results.Ok(new
        {
            briefing = contractService.GenerateBriefingText(item),
            terms = contractService.GenerateSpecialTerms(item)
        });
    });

    // --- CRAWLER / DEALS APIs ---
    app.MapGet("/api/danggeun/deals", async (int? userId, DatabaseService db, FakeListingDetector detector) =>
    {
        var deals = await db.GetCrawledDealsAsync();
        var customers = await db.GetCustomerRequestsAsync(userId ?? 1);
        detector.AnalyzeDeals(deals, customers);
        return Results.Ok(deals);
    });

    app.MapPost("/api/danggeun/import/{id:int}", async (int id, int? userId, DatabaseService db) =>
    {
        var list = await db.GetCrawledDealsAsync();
        var deal = list.Find(d => d.Id == id);
        if (deal == null) return Results.NotFound();
        int newId = await ImportDealToDatabase(deal, userId ?? 1, db);
        await db.MarkDealAsImportedAsync(id);
        return Results.Ok(new { newPropertyId = newId });
    });

    app.MapPost("/api/danggeun/import-bulk", async (BulkImportRequest req, DatabaseService db) =>
    {
        var list = await db.GetCrawledDealsAsync();
        int count = 0;
        foreach (var id in req.Ids)
        {
            var deal = list.Find(d => d.Id == id);
            if (deal != null)
            {
                await ImportDealToDatabase(deal, req.UserId, db);
                await db.MarkDealAsImportedAsync(id);
                count++;
            }
        }
        return Results.Ok(new { importedCount = count });
    });

    // 크롤러 실시간 상태 조회 API
    app.MapGet("/api/crawler/status", (CrawlerService crawler) =>
    {
        return Results.Ok(new
        {
            isCrawling = crawler.IsCrawling,
            currentProgress = crawler.CurrentProgress,
            logs = crawler.ProgressLogs
        });
    });

    // 크롤러 통합 엔드포인트 (JSON Body 및 모든 요청 완벽 수용)
    app.MapPost("/api/crawler/run", async (HttpContext ctx, CrawlerService crawler, DatabaseService db) =>
    {
        bool isReset = false;
        try
        {
            if (ctx.Request.HasJsonContentType())
            {
                var body = await ctx.Request.ReadFromJsonAsync<CrawlerRunRequest>();
                if (body != null && body.Reset) isReset = true;
            }
        }
        catch { }

        if (isReset || ctx.Request.Query.ContainsKey("clean") || ctx.Request.Query.ContainsKey("reset"))
        {
            await db.ClearCrawledDealsAsync();
            Console.WriteLine("[DB 초기화] 기존 직거래 데이터를 0건으로 완전 삭제했습니다.");
        }

        int daysLimit = 7;
        if (ctx.Request.Query.TryGetValue("days", out var qDays) && int.TryParse(qDays, out int parsedDays))
        {
            daysLimit = parsedDays;
        }

        int newCount = await crawler.CrawlDirectDealsAsync("서울", daysLimit);
        var updatedList = await db.GetCrawledDealsAsync();
        return Results.Ok(new { newCount, totalCount = updatedList.Count });
    });

    // --- NAVER AUDIT & LISTING APIs ---
    // 2. 내 네이버 매물 목록 조회
    app.MapGet("/api/naver/listings", async (int? userId, DatabaseService db) =>
    {
        int uId = userId ?? 1;
        var list = await db.GetNaverListingsAsync(uId);
        return Results.Ok(list);
    });

    // 3. 네이버 매물 일괄 등록 (텍스트 / URL 목록 붙여넣기)
    app.MapPost("/api/naver/listings/register-bulk", async (NaverBulkRegisterRequest req, NaverLandService naverService, DatabaseService db) =>
    {
        int uId = req.UserId ?? 1;
        var numbers = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(req.Text))
        {
            foreach (var n in naverService.ExtractMultipleArticleNumbers(req.Text))
            {
                numbers.Add(n);
            }
        }
        if (req.ArticleNumbers != null)
        {
            foreach (var n in req.ArticleNumbers)
            {
                string clean = naverService.ExtractArticleNumber(n);
                if (!string.IsNullOrEmpty(clean)) numbers.Add(clean);
            }
        }

        if (numbers.Count == 0)
        {
            return Results.BadRequest(new { success = false, message = "입력된 내용에서 유효한 네이버 매물번호를 찾지 못했습니다." });
        }

        bool skipAudited = req.SkipAlreadyAudited ?? true;
        var (successCount, failedCount, skippedCount, errors) = await naverService.RegisterMultipleArticlesAsync(numbers.ToList(), uId, db, skipAudited);
        var currentList = await db.GetNaverListingsAsync(uId);
        string summaryMsg = skippedCount > 0
            ? $"신규 매물 {successCount}건 수집 완료! (이미 대장 검수 완료된 {skippedCount}건은 안전하게 건너뛰었습니다)"
            : $"신규 매물 {successCount}건 수집이 완료되었습니다.";
        return Results.Ok(new
        {
            success = true,
            totalRequested = numbers.Count,
            successCount,
            skippedCount,
            failedCount,
            message = summaryMsg,
            errors,
            listings = currentList
        });
    });

    // 3-1. 엑셀/CSV 파일 직접 업로드하여 매물 일괄 등록
    app.MapPost("/api/naver/listings/upload-excel", async (HttpRequest request, NaverLandService naverService, DatabaseService db) =>
    {
        if (!request.HasFormContentType) return Results.BadRequest(new { success = false, message = "파일이 전송되지 않았습니다." });
        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile("file");
        if (file == null || file.Length == 0) return Results.BadRequest(new { success = false, message = "선택된 파일이 비어있습니다." });

        var numbers = new HashSet<string>();
        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        try
        {
            if (ext == ".xlsx")
            {
                using var stream = file.OpenReadStream();
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        string xml = await reader.ReadToEndAsync();
                        var matches = Regex.Matches(xml, @"\b([0-9]{9,11})\b");
                        foreach (Match m in matches)
                        {
                            numbers.Add(m.Groups[1].Value);
                        }
                    }
                }
            }
            else // .csv, .txt
            {
                using var reader = new StreamReader(file.OpenReadStream());
                string text = await reader.ReadToEndAsync();
                foreach (var n in naverService.ExtractMultipleArticleNumbers(text))
                {
                    numbers.Add(n);
                }
            }
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, message = $"파일 분석 중 오류가 발생했습니다: {ex.Message}" });
        }

        if (numbers.Count == 0)
        {
            return Results.BadRequest(new { success = false, message = "업로드된 파일에서 유효한 10자리 매물 번호를 찾지 못했습니다." });
        }

        int uId = 1;
        if (form.TryGetValue("userId", out var uIdVal) && int.TryParse(uIdVal, out int parsedUid))
        {
            uId = parsedUid;
        }

        var (successCount, failedCount, skippedCount, errors) = 
            await naverService.RegisterMultipleArticlesAsync(numbers.ToList(), uId, db, skipAlreadyAudited: true);

        var currentList = await db.GetNaverListingsAsync(uId);
        string resultMsg = skippedCount > 0
            ? $"엑셀 파일에서 총 {numbers.Count}건 매물 번호 추출 완료! (신규 수집: {successCount}건, 이미 대장 검수 완료된 {skippedCount}건은 안전 보존)"
            : $"엑셀 파일에서 총 {numbers.Count}건 매물 번호를 성공적으로 수집하여 등록했습니다.";

        return Results.Ok(new
        {
            success = true,
            totalRequested = numbers.Count,
            successCount,
            skippedCount,
            failedCount,
            message = resultMsg,
            listings = currentList
        });
    });

    // 3-2. URL 또는 텍스트에서 매물 긁어와 SQLite 자동 저장
    app.MapPost("/api/naver/listings/scrape-url", async (ScrapeUrlRequest req, NaverLandService naverService, DatabaseService db) =>
    {
        int uId = req.UserId ?? 1;
        string input = (req.Url ?? "").Trim();
        if (string.IsNullOrEmpty(input))
        {
            return Results.BadRequest(new { success = false, message = "URL 또는 매물 링크를 입력해주세요." });
        }

        var targetNumbers = new HashSet<string>();

        // 1. 입력 문자열 자체에서 매물번호 추출
        foreach (var n in naverService.ExtractMultipleArticleNumbers(input))
        {
            targetNumbers.Add(n);
        }

        // 2. 만약 http(s) URL인 경우, 해당 웹페이지를 실제로 크롤링하여 내부 매물 번호 전부 긁어오기
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var handler = new HttpClientHandler { AllowAutoRedirect = true };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "ko-KR,ko;q=0.9,en-US;q=0.8");

                var res = await client.GetAsync(input);
                if (res.IsSuccessStatusCode)
                {
                    string html = await res.Content.ReadAsStringAsync();
                    foreach (var n in naverService.ExtractMultipleArticleNumbers(html))
                    {
                        targetNumbers.Add(n);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScrapeUrl] 크롤링 경고: {ex.Message}");
            }
        }

        if (targetNumbers.Count == 0)
        {
            return Results.BadRequest(new { 
                success = false, 
                message = "입력하신 URL 또는 텍스트에서 매물 번호를 찾지 못했습니다. 매물 상세 페이지 URL이나 10자리 매물번호를 입력해주세요." 
            });
        }

        // 3. 네이버 & 건축물대장 서비스에 매물 등록 및 SQLite 영구 저장
        var (successCount, failedCount, skippedCount, errors) = 
            await naverService.RegisterMultipleArticlesAsync(targetNumbers.ToList(), uId, db, skipAlreadyAudited: true);

        var currentList = await db.GetNaverListingsAsync(uId);
        string msg = skippedCount > 0
            ? $"총 {targetNumbers.Count}건 긁어와 SQLite 저장 완료! (신규 {successCount}건, 기검수 보존 {skippedCount}건)"
            : $"총 {targetNumbers.Count}건 매물을 성공적으로 긁어와 SQLite에 저장했습니다!";

        return Results.Ok(new
        {
            success = true,
            totalFound = targetNumbers.Count,
            successCount,
            skippedCount,
            failedCount,
            message = msg,
            listings = currentList
        });
    });

    // 4. 단일 매물 대장 & 공시가격 검증 실행
    app.MapPost("/api/naver/listings/audit/{id:int}", async (int id, NaverLandService naverService, BuildingLedgerService ledgerService, VWorldHousingPriceService vworldService, DatabaseService db) =>
    {
        var res = await naverService.AuditListingAsync(id, ledgerService, db, vworldService);
        return Results.Ok(res);
    });

    // 5. 전체/미검증 매물 일괄 대장 전수 검증
    app.MapPost("/api/naver/listings/audit-all", async (int? userId, bool? forceAll, NaverLandService naverService, BuildingLedgerService ledgerService, VWorldHousingPriceService vworldService, DatabaseService db) =>
    {
        int uId = userId ?? 1;
        bool recheckAll = forceAll ?? false;
        var list = await db.GetNaverListingsAsync(uId);

        // 이미 검수(Safe/Warning/Danger) 완료된 매물은 보존하고, 미검증(Pending 또는 실패) 매물만 스마트하게 선별 검증
        var targetList = recheckAll
            ? list
            : list.Where(x => string.IsNullOrEmpty(x.LedgerStatus) || x.LedgerStatus == "Pending" || x.LedgerStatus == "Failed").ToList();

        int skippedAlreadyAudited = list.Count - targetList.Count;
        int audited = 0;
        int safeCount = 0;
        int warningCount = 0;
        int dangerCount = 0;

        foreach (var item in targetList)
        {
            var res = await naverService.AuditListingAsync(item.Id, ledgerService, db, vworldService);
            audited++;
            if (res.OverallStatus == "Safe") safeCount++;
            else if (res.OverallStatus == "Warning") warningCount++;
            else if (res.OverallStatus == "Danger") dangerCount++;

            await Task.Delay(250);
        }

        var updatedList = await db.GetNaverListingsAsync(uId);
        string resultMsg = skippedAlreadyAudited > 0
            ? $"미검증 매물 {audited}건 대장 전수 검증 완료! (이미 검수 완료된 {skippedAlreadyAudited}건은 기존 결과 그대로 안전하게 보존되었습니다)"
            : $"매물 {audited}건의 건축물대장 전수 검증이 완료되었습니다.";

        return Results.Ok(new
        {
            success = true,
            totalAudited = audited,
            skippedAlreadyAudited,
            safeCount,
            warningCount,
            dangerCount,
            message = resultMsg,
            listings = updatedList
        });
    });

    // 5-1. 위반건축물 여부 수동 토글/지정 (중개사가 정부24 대장 실물 확인 후 원클릭 반영)
    app.MapPost("/api/naver/listings/{id:int}/toggle-violation", async (int id, DatabaseService db) =>
    {
        var item = await db.GetNaverListingByIdAsync(id);
        if (item == null) return Results.NotFound(new { message = "매물을 찾을 수 없습니다." });

        bool nextState = !item.IsViolatingBuilding;
        string? reason = nextState 
            ? "실제 관할 구청 건축물대장에 [위반건축물]로 공식 등재 확인 (이행강제금 및 전세대출/보증보험 불가)" 
            : null;

        bool updated = await db.UpdateNaverListingViolationAsync(id, nextState, reason);
        var freshItem = await db.GetNaverListingByIdAsync(id);
        return Results.Ok(new { success = updated, item = freshItem, isViolating = nextState });
    });

    // 6. 검증된 네이버 매물을 내 OnHouse 장부(Properties)로 원클릭 저장
    app.MapPost("/api/naver/listings/import-to-property/{id:int}", async (int id, int? userId, DatabaseService db) =>
    {
        int uId = userId ?? 1;
        var item = await db.GetNaverListingByIdAsync(id);
        if (item == null) return Results.NotFound(new { message = "매물을 찾을 수 없습니다." });

        int deposit = 0;
        int monthlyRent = 0;
        int floor = 1;
        int totalFloor = 1;
        if (!string.IsNullOrEmpty(item.FloorInfo))
        {
            var fMatch = Regex.Match(item.FloorInfo, @"(\d+)");
            if (fMatch.Success) int.TryParse(fMatch.Groups[1].Value, out floor);
            var tfMatch = Regex.Match(item.FloorInfo, @"/(\d+)");
            if (tfMatch.Success) int.TryParse(tfMatch.Groups[1].Value, out totalFloor);
            else totalFloor = floor >= 4 ? floor : 4;
        }

        if (!string.IsNullOrEmpty(item.RawJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(item.RawJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("Price", out var pProp) && pProp.GetInt64() > 0)
                {
                    deposit = (int)pProp.GetInt64();
                }
                if (root.TryGetProperty("PreviousMonthlyRent", out var pmr) && pmr.GetInt64() > 0)
                {
                    monthlyRent = (int)pmr.GetInt64();
                }
            }
            catch { }
        }

        var prop = new PropertyItem
        {
            UserId = uId,
            Title = item.ArticleName,
            PropertyType = item.RealEstateType switch
            {
                "A01" => "아파트",
                "A02" => "오피스텔",
                "C02" => "다세대/빌라",
                "C03" => "연립주택",
                "D01" => "단독주택",
                "D02" => "다가구주택",
                _ => "빌라/다세대"
            },
            TransactionType = string.IsNullOrEmpty(item.TradeType) ? "매매" : item.TradeType,
            Deposit = deposit,
            MonthlyRent = monthlyRent,
            MaintenanceFee = 0,
            Address = string.IsNullOrEmpty(item.Address) ? "서울" : item.Address,
            DetailAddress = $"[네이버 광고 매물 {item.ArticleNumber}]",
            Floor = floor,
            TotalFloor = totalFloor,
            AreaM2 = item.AreaM2 > 0 ? item.AreaM2 : 33.0,
            HasElevator = item.HasElevator,
            HasParking = item.TotalParking > 0,
            AllowsPets = false,
            IsLoanAvailable = item.LedgerStatus != "Danger",
            IsViolatingBuilding = item.LedgerStatus == "Danger",
            Status = "공실",
            SourceChannel = "네이버부동산",
            SecretMemo = $"[네이버 광고 매물 {item.ArticleNumber}]\n대장 검증 결과: {item.LedgerStatus} - {item.LedgerMessage}\n검증일시: {item.InspectedAt:yyyy-MM-dd HH:mm}",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        int newPropertyId = await db.InsertPropertyAsync(prop, uId);
        await db.MarkNaverListingAsImportedAsync(id);
        return Results.Ok(new { success = true, newPropertyId });
    });

    // 7. 매물 개별 삭제
    app.MapDelete("/api/naver/listings/{id:int}", async (int id, int? userId, DatabaseService db) =>
    {
        bool ok = await db.DeleteNaverListingAsync(id, userId ?? 1);
        return ok ? Results.Ok() : Results.NotFound();
    });

    // 8. 매물 전체 비우기
    app.MapDelete("/api/naver/listings/clear", async (int? userId, DatabaseService db) =>
    {
        await db.ClearNaverListingsAsync(userId ?? 1);
        return Results.Ok(new { success = true });
    });
    app.MapPost("/api/naver/listings/clear", async (int? userId, DatabaseService db) =>
    {
        await db.ClearNaverListingsAsync(userId ?? 1);
        return Results.Ok(new { success = true });
    });

    // 9. 중개사 설정 조회 및 저장
    app.MapGet("/api/naver/realtor/settings", async (int? userId, DatabaseService db) =>
    {
        var s = await db.GetRealtorSettingsAsync(userId ?? 1);
        return Results.Ok(s ?? new RealtorSettingsItem { UserId = userId ?? 1 });
    });

    app.MapPost("/api/naver/realtor/settings", async (RealtorSettingsItem req, DatabaseService db) =>
    {
        await db.SaveRealtorSettingsAsync(req);
        return Results.Ok(new { success = true, message = "중개업소 연동 정보가 안전하게 저장되었습니다!" });
    });

    // 10. 이실장(AI실장 / aipartner.plus) 로그인 및 매물 자동 연동 API
    app.MapPost("/api/aipartner/login-and-fetch", async (AiPartnerLoginRequest req, AiPartnerService aiPartnerService, NaverLandService naverService, DatabaseService db) =>
    {
        int uId = req.UserId ?? 1;
        if (string.IsNullOrWhiteSpace(req.Id) || string.IsNullOrWhiteSpace(req.Password))
        {
            return Results.BadRequest(new { success = false, message = "이실장 아이디(휴대폰 번호)와 비밀번호를 모두 입력해주세요.", logs = new List<string> { "아이디 혹은 비밀번호가 입력되지 않았습니다." } });
        }

        var (success, message, extractedCount, successCount, skippedCount, failedCount, errors, logs) =
            await aiPartnerService.LoginAndFetchListingsAsync(req.Id.Trim(), req.Password.Trim(), req.AgencyName, req.RealtorInput, uId, db, naverService);

        var currentList = await db.GetNaverListingsAsync(uId);
        return Results.Ok(new
        {
            success,
            message,
            extractedCount,
            successCount,
            skippedCount,
            failedCount,
            errors,
            logs,
            listings = currentList
        });
    });

    // 정적 파일 및 SPA Fallback (index.html 항상 서빙)
    app.MapFallbackToFile("index.html");

    static async Task<int> ImportDealToDatabase(DanggeunDealItem deal, int userId, DatabaseService db)
    {
        string fullText = $"{deal.Title} {deal.Description}";

        string extractedAddress = deal.Region?.Trim() ?? "서울";
        if (!extractedAddress.StartsWith("서울")) extractedAddress = $"서울 {extractedAddress}";

        // 1. 본문에서 번지수 포함 상세 지번 주소 검색 (예: 묵동 164-32, 중림동 123-45, 신림동 1432번지)
        var detailedLotMatch = Regex.Match(fullText, @"([가-힣]+(?:구)?\s*[가-힣]+(?:동|가)\s*\d{1,4}(?:-\d{1,4})?(?:\s*번지)?)");
        if (detailedLotMatch.Success && !detailedLotMatch.Value.Contains("직거래") && !detailedLotMatch.Value.Contains("부동산"))
        {
            string found = detailedLotMatch.Value.Trim();
            if (!found.StartsWith("서울")) found = $"서울 {found}";
            extractedAddress = found;
        }
        else
        {
            // 2. 도로명 주소 패턴 검색 (예: 동일로 987, 테헤란로 152)
            var roadMatch = Regex.Match(fullText, @"([가-힣]+(?:로|길)\s*\d{1,4}(?:-\d{1,4})?)");
            if (roadMatch.Success)
            {
                string found = roadMatch.Value.Trim();
                if (!found.StartsWith("서울")) found = $"{deal.Region} {found}".Trim();
                if (!found.StartsWith("서울")) found = $"서울 {found}";
                extractedAddress = found;
            }
            else
            {
                // 3. 동 이름만 있는 경우
                var dongMatch = Regex.Match(fullText, @"([가-힣]+(?:동|구))");
                if (dongMatch.Success && !dongMatch.Value.Contains("직거래") && !dongMatch.Value.Contains("부동산") && !dongMatch.Value.Contains("공동"))
                {
                    if (!extractedAddress.Contains(dongMatch.Value))
                    {
                        extractedAddress = $"{extractedAddress} {dongMatch.Value}".Trim();
                    }
                }
            }
        }

        int floor = 2;
        var floorMatch = Regex.Match(fullText, @"(\d+)\s*층");
        if (floorMatch.Success) int.TryParse(floorMatch.Groups[1].Value, out floor);
        if (fullText.Contains("반지하") || fullText.Contains("지하")) floor = -1;
        if (fullText.Contains("옥탑")) floor = 4;

        double areaM2 = 23.0;
        var pyeongMatch = Regex.Match(fullText, @"(\d+)\s*평");
        if (pyeongMatch.Success && double.TryParse(pyeongMatch.Groups[1].Value, out double parsedPyeong))
        {
            areaM2 = Math.Round(parsedPyeong * 3.30578, 1);
        }
        var m2Match = Regex.Match(fullText, @"(\d+)\s*m2|\d+\s*㎡", RegexOptions.IgnoreCase);
        if (m2Match.Success) double.TryParse(Regex.Match(m2Match.Value, @"\d+").Value, out areaM2);

        bool hasElevator = fullText.Contains("엘베") || fullText.Contains("엘리베이터");
        bool hasParking = fullText.Contains("주차");
        bool allowsPets = fullText.Contains("반려동물") || fullText.Contains("애완") || fullText.Contains("강아지") || fullText.Contains("고양이");
        bool isLoan = !fullText.Contains("대출불가");

        var newItem = new PropertyItem
        {
            UserId = userId,
            Title = deal.Title,
            PropertyType = deal.Title.Contains("투룸") ? "투룸/쓰리룸" : (deal.Title.Contains("오피스텔") ? "오피스텔" : "원룸"),
            TransactionType = deal.MonthlyRent > 0 ? "월세" : "전세",
            Deposit = deal.Deposit,
            MonthlyRent = deal.MonthlyRent,
            MaintenanceFee = 5,
            Address = extractedAddress,
            DetailAddress = $"{deal.AuthorType} 수집 매물",
            Floor = floor,
            TotalFloor = floor > 3 ? floor + 1 : 4,
            AreaM2 = areaM2,
            HasElevator = hasElevator,
            HasParking = hasParking,
            AllowsPets = allowsPets,
            IsLoanAvailable = isLoan,
            Status = "공실",
            SourceChannel = deal.AuthorType,
            ImageUrl = deal.ImageUrl,
            OwnerName = deal.AuthorName,
            OwnerPhone = "(직거래 원문 확인)",
            SecretMemo = $"[직거래 출처: {deal.AuthorType}]\n원문 링크: {deal.ArticleUrl}\n작성자: {deal.AuthorName}\n수집 내용: {deal.Description}",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        return await db.InsertPropertyAsync(newItem, userId);
    }

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"[프로그램 실행 오류] {ex.Message}");
    Console.WriteLine("\n창을 닫으려면 엔터 키를 누르세요...");
    Console.ReadLine();
}

public record RegisterRequest(string Username, string Password, string Name, string? AgencyName, string InviteCode);
public record LoginRequest(string Username, string Password);
public record BulkImportRequest(List<int> Ids, int UserId);
public record CrawlerRunRequest(bool Reset = false);
public record NaverInspectRequest(string Url);
public record NaverBulkRegisterRequest(string? Text, List<string>? ArticleNumbers, int? UserId, bool? SkipAlreadyAudited = true);
public record AiPartnerLoginRequest(string Id, string Password, string? AgencyName, string? RealtorInput, int? UserId);
public record ScrapeUrlRequest(string? Url, int? UserId);
public class StatusUpdateRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}
