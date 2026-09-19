using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using OnHouseLocal.Models;

namespace OnHouseLocal.Services
{
    public class DatabaseService
    {
        // 윈도우 영구 저장소 (%LocalAppData%\OnHouseLocal\onhouse_v2.db)
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "OnHouseLocal"
        );
        private static readonly string DbPath = Path.Combine(AppDataDir, "onhouse_v2.db");
        private static readonly string ConnectionString = $"Data Source={DbPath};";

        public DatabaseService()
        {
            if (!Directory.Exists(AppDataDir))
            {
                Directory.CreateDirectory(AppDataDir);
            }
            InitializeDatabaseAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeDatabaseAsync()
        {
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string createTablesSql = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT UNIQUE NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    Salt TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    AgencyName TEXT,
                    CreatedAt DATETIME NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Properties (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    Title TEXT NOT NULL,
                    PropertyType TEXT NOT NULL,
                    TransactionType TEXT NOT NULL,
                    Deposit INTEGER NOT NULL,
                    MonthlyRent INTEGER NOT NULL,
                    MaintenanceFee INTEGER NOT NULL,
                    Address TEXT NOT NULL,
                    DetailAddress TEXT,
                    Floor INTEGER NOT NULL,
                    TotalFloor INTEGER NOT NULL,
                    AreaM2 REAL NOT NULL,
                    HasElevator INTEGER NOT NULL,
                    HasParking INTEGER NOT NULL,
                    AllowsPets INTEGER NOT NULL,
                    IsLoanAvailable INTEGER NOT NULL,
                    IsViolatingBuilding INTEGER NOT NULL,
                    Status TEXT DEFAULT '공실',
                    SourceChannel TEXT DEFAULT '직접등록',
                    ImageUrl TEXT DEFAULT '',
                    OwnerName TEXT,
                    OwnerPhone TEXT,
                    SecretMemo TEXT,
                    CreatedAt DATETIME,
                    UpdatedAt DATETIME
                );

                CREATE TABLE IF NOT EXISTS ContactLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PropertyId INTEGER NOT NULL,
                    ContactDate DATETIME NOT NULL,
                    ContactType TEXT DEFAULT '전화',
                    Result TEXT DEFAULT '생존확인',
                    Memo TEXT
                );

                CREATE TABLE IF NOT EXISTS CustomerRequests (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    CustomerName TEXT NOT NULL,
                    CustomerPhone TEXT DEFAULT '',
                    PropertyType TEXT DEFAULT '원룸',
                    TransactionType TEXT DEFAULT '월세',
                    MinDeposit INTEGER DEFAULT 0,
                    MaxDeposit INTEGER DEFAULT 10000,
                    MaxMonthlyRent INTEGER DEFAULT 100,
                    TargetRegion TEXT DEFAULT '관악구',
                    Memo TEXT DEFAULT '',
                    IsActive INTEGER DEFAULT 1,
                    CreatedAt DATETIME NOT NULL
                );

                CREATE TABLE IF NOT EXISTS CrawledDeals (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ArticleId TEXT UNIQUE NOT NULL,
                    Title TEXT NOT NULL,
                    Region TEXT NOT NULL,
                    PriceDisplay TEXT NOT NULL,
                    Deposit INTEGER NOT NULL,
                    MonthlyRent INTEGER NOT NULL,
                    Description TEXT,
                    AuthorName TEXT,
                    AuthorType TEXT,
                    ArticleUrl TEXT,
                    ImageUrl TEXT DEFAULT '',
                    DetectedAt DATETIME,
                    IsImported INTEGER DEFAULT 0,
                    Status TEXT DEFAULT '신규',
                    SuspiciousSignal TEXT DEFAULT '',
                    MatchedCustomerInfo TEXT DEFAULT ''
                );

                CREATE TABLE IF NOT EXISTS NaverListings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    ArticleNumber TEXT NOT NULL,
                    ArticleName TEXT DEFAULT '',
                    TradeType TEXT DEFAULT '',
                    RealEstateType TEXT DEFAULT '',
                    PriceDisplay TEXT DEFAULT '',
                    FloorInfo TEXT DEFAULT '',
                    AreaM2 REAL DEFAULT 0,
                    Address TEXT DEFAULT '',
                    HasElevator INTEGER DEFAULT 0,
                    TotalParking INTEGER DEFAULT 0,
                    ApprovalDate TEXT DEFAULT '',
                    RawJson TEXT DEFAULT '',
                    LedgerStatus TEXT DEFAULT 'Pending',
                    LedgerMessage TEXT DEFAULT '',
                    LedgerDiscrepanciesJson TEXT DEFAULT '',
                    InspectedAt DATETIME,
                    IsImported INTEGER DEFAULT 0,
                    PlatArea REAL DEFAULT 0,
                    ArchArea REAL DEFAULT 0,
                    TotArea REAL DEFAULT 0,
                    BcRat REAL DEFAULT 0,
                    VlRat REAL DEFAULT 0,
                    BuildingStructure TEXT DEFAULT '',
                    PublicPrice INTEGER DEFAULT 0,
                    PublicPriceYear TEXT DEFAULT '',
                    HugGuaranteeLimit INTEGER DEFAULT 0,
                    LedgerRawJson TEXT DEFAULT '',
                    CreatedAt DATETIME NOT NULL
                );

                CREATE TABLE IF NOT EXISTS RealtorSettings (
                    UserId INTEGER PRIMARY KEY,
                    RealtorName TEXT DEFAULT '',
                    AgencyName TEXT DEFAULT '',
                    RealtorId TEXT DEFAULT '',
                    NaverId TEXT DEFAULT '',
                    NaverPassword TEXT DEFAULT '',
                    AutoSync INTEGER DEFAULT 0,
                    UpdatedAt DATETIME
                );
            ";

            await connection.ExecuteAsync(createTablesSql);

            // [안전한 컬럼 마이그레이션]
            try { await connection.ExecuteAsync("ALTER TABLE Properties ADD COLUMN Status TEXT DEFAULT '신규';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE Properties ADD COLUMN LastCheckedAt DATETIME;"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE Properties ADD COLUMN CheckedBy TEXT DEFAULT '';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE Properties ADD COLUMN SuspiciousSignal TEXT DEFAULT '';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE Properties ADD COLUMN MatchedCustomerInfo TEXT DEFAULT '';"); } catch { }

            try { await connection.ExecuteAsync("ALTER TABLE CrawledDeals ADD COLUMN Status TEXT DEFAULT '신규';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE CrawledDeals ADD COLUMN SuspiciousSignal TEXT DEFAULT '';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE CrawledDeals ADD COLUMN MatchedCustomerInfo TEXT DEFAULT '';"); } catch { }

            await connection.ExecuteAsync(createTablesSql);
            try { await connection.ExecuteAsync("DELETE FROM CrawledDeals;"); } catch { }

            try { await connection.ExecuteAsync("ALTER TABLE Properties ADD COLUMN SourceChannel TEXT DEFAULT '직접등록';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE Properties ADD COLUMN ImageUrl TEXT DEFAULT '';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE Properties ADD COLUMN UserId INTEGER DEFAULT 1;"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE CrawledDeals ADD COLUMN ImageUrl TEXT DEFAULT '';"); } catch { }

            // NaverListings 신규 스펙 컬럼 마이그레이션
            try { await connection.ExecuteAsync("ALTER TABLE NaverListings ADD COLUMN PlatArea REAL DEFAULT 0;"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE NaverListings ADD COLUMN ArchArea REAL DEFAULT 0;"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE NaverListings ADD COLUMN TotArea REAL DEFAULT 0;"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE NaverListings ADD COLUMN BcRat REAL DEFAULT 0;"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE NaverListings ADD COLUMN VlRat REAL DEFAULT 0;"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE NaverListings ADD COLUMN BuildingStructure TEXT DEFAULT '';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE NaverListings ADD COLUMN PublicPrice INTEGER DEFAULT 0;"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE NaverListings ADD COLUMN PublicPriceYear TEXT DEFAULT '';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE NaverListings ADD COLUMN HugGuaranteeLimit INTEGER DEFAULT 0;"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE NaverListings ADD COLUMN LedgerRawJson TEXT DEFAULT '';"); } catch { }
            try { await connection.ExecuteAsync("ALTER TABLE NaverListings ADD COLUMN IsViolatingBuilding INTEGER DEFAULT 0;"); } catch { }
        }

        // --- 사용자 계정 관리 ---
        public async Task<UserItem?> GetUserByUsernameAsync(string username)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "SELECT * FROM Users WHERE Username = @Username;";
            return await connection.QueryFirstOrDefaultAsync<UserItem>(sql, new { Username = username });
        }

        public async Task<UserItem?> GetUserByIdAsync(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "SELECT * FROM Users WHERE Id = @Id;";
            return await connection.QueryFirstOrDefaultAsync<UserItem>(sql, new { Id = id });
        }

        public async Task<int> CreateUserAsync(UserItem user)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = @"
                INSERT INTO Users (Username, PasswordHash, Salt, Name, AgencyName, CreatedAt)
                VALUES (@Username, @PasswordHash, @Salt, @Name, @AgencyName, @CreatedAt);
                SELECT last_insert_rowid();";
            return await connection.ExecuteScalarAsync<int>(sql, user);
        }

        // --- 내 장부 매물 관리 (사용자별) ---
        public async Task<List<PropertyItem>> GetAllPropertiesAsync(int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "SELECT * FROM Properties WHERE UserId = @UserId ORDER BY Id DESC;";
            var result = await connection.QueryAsync<PropertyItem>(sql, new { UserId = userId });
            return result.AsList();
        }

        public async Task<PropertyItem?> GetPropertyByIdAsync(int id, int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "SELECT * FROM Properties WHERE Id = @Id AND UserId = @UserId;";
            return await connection.QueryFirstOrDefaultAsync<PropertyItem>(sql, new { Id = id, UserId = userId });
        }

        public async Task<List<PropertyItem>> SearchPropertiesAsync(PropertyFilter filter, int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            var sb = new StringBuilder("SELECT * FROM Properties WHERE UserId = @UserId ");
            var param = new DynamicParameters();
            param.Add("UserId", userId);

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                sb.Append("AND (Title LIKE @Q OR Address LIKE @Q OR DetailAddress LIKE @Q OR SecretMemo LIKE @Q) ");
                param.Add("Q", $"%{filter.SearchText}%");
            }

            if (!string.IsNullOrWhiteSpace(filter.PropertyType) && filter.PropertyType != "전체")
            {
                sb.Append("AND PropertyType = @PropertyType ");
                param.Add("PropertyType", filter.PropertyType);
            }

            if (!string.IsNullOrWhiteSpace(filter.TransactionType) && filter.TransactionType != "전체")
            {
                sb.Append("AND TransactionType = @TransactionType ");
                param.Add("TransactionType", filter.TransactionType);
            }

            if (filter.MaxDeposit > 0)
            {
                sb.Append("AND Deposit <= @MaxDeposit ");
                param.Add("MaxDeposit", filter.MaxDeposit);
            }

            if (filter.MaxMonthlyRent > 0)
            {
                sb.Append("AND MonthlyRent <= @MaxMonthlyRent ");
                param.Add("MaxMonthlyRent", filter.MaxMonthlyRent);
            }

            if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status != "전체")
            {
                sb.Append("AND Status = @Status ");
                param.Add("Status", filter.Status);
            }

            if (filter.OnlyWithElevator) sb.Append("AND HasElevator = 1 ");
            if (filter.OnlyWithParking) sb.Append("AND HasParking = 1 ");
            if (filter.OnlyPetsAllowed) sb.Append("AND AllowsPets = 1 ");
            if (filter.ExcludeViolatingBuilding) sb.Append("AND IsViolatingBuilding = 0 ");

            sb.Append("ORDER BY Id DESC;");

            var result = await connection.QueryAsync<PropertyItem>(sb.ToString(), param);
            return result.AsList();
        }

        public async Task<int> InsertPropertyAsync(PropertyItem item, int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = @"
                INSERT INTO Properties (
                    UserId, Title, PropertyType, TransactionType, Deposit, MonthlyRent, MaintenanceFee,
                    Address, DetailAddress, Floor, TotalFloor, AreaM2,
                    HasElevator, HasParking, AllowsPets, IsLoanAvailable, IsViolatingBuilding,
                    Status, SourceChannel, ImageUrl, OwnerName, OwnerPhone, SecretMemo, CreatedAt, UpdatedAt
                ) VALUES (
                    @UserId, @Title, @PropertyType, @TransactionType, @Deposit, @MonthlyRent, @MaintenanceFee,
                    @Address, @DetailAddress, @Floor, @TotalFloor, @AreaM2,
                    @HasElevator, @HasParking, @AllowsPets, @IsLoanAvailable, @IsViolatingBuilding,
                    @Status, @SourceChannel, @ImageUrl, @OwnerName, @OwnerPhone, @SecretMemo, @CreatedAt, @UpdatedAt
                );
                SELECT last_insert_rowid();";

            var param = new DynamicParameters(item);
            param.Add("UserId", userId);
            return await connection.ExecuteScalarAsync<int>(sql, param);
        }

        public async Task<bool> UpdatePropertyAsync(PropertyItem item, int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = @"
                UPDATE Properties SET
                    Title = @Title,
                    PropertyType = @PropertyType,
                    TransactionType = @TransactionType,
                    Deposit = @Deposit,
                    MonthlyRent = @MonthlyRent,
                    MaintenanceFee = @MaintenanceFee,
                    Address = @Address,
                    DetailAddress = @DetailAddress,
                    Floor = @Floor,
                    TotalFloor = @TotalFloor,
                    AreaM2 = @AreaM2,
                    HasElevator = @HasElevator,
                    HasParking = @HasParking,
                    AllowsPets = @AllowsPets,
                    IsLoanAvailable = @IsLoanAvailable,
                    IsViolatingBuilding = @IsViolatingBuilding,
                    Status = @Status,
                    SourceChannel = @SourceChannel,
                    ImageUrl = @ImageUrl,
                    OwnerName = @OwnerName,
                    OwnerPhone = @OwnerPhone,
                    SecretMemo = @SecretMemo,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id AND UserId = @UserId;";

            var param = new DynamicParameters(item);
            param.Add("UserId", userId);
            int rows = await connection.ExecuteAsync(sql, param);
            return rows > 0;
        }

        public async Task<bool> DeletePropertyAsync(int id, int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "DELETE FROM Properties WHERE Id = @Id AND UserId = @UserId;";
            int affected = await connection.ExecuteAsync(sql, new { Id = id, UserId = userId });
            return affected > 0;
        }

        // --- 크롤링 직거래 매물 ---
        public async Task<bool> InsertCrawledDealAsync(DanggeunDealItem deal)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = @"
                INSERT OR IGNORE INTO CrawledDeals (
                    ArticleId, Title, Region, PriceDisplay, Deposit, MonthlyRent,
                    Description, AuthorName, AuthorType, ArticleUrl, ImageUrl, DetectedAt, IsImported
                ) VALUES (
                    @ArticleId, @Title, @Region, @PriceDisplay, @Deposit, @MonthlyRent,
                    @Description, @AuthorName, @AuthorType, @ArticleUrl, @ImageUrl, @DetectedAt, 0
                );";

            int affected = await connection.ExecuteAsync(sql, deal);
            return affected > 0;
        }

        public async Task<bool> ClearCrawledDealsAsync()
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "DELETE FROM CrawledDeals;";
            int affected = await connection.ExecuteAsync(sql);
            return affected >= 0;
        }

        public async Task<List<DanggeunDealItem>> GetCrawledDealsAsync()
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "SELECT * FROM CrawledDeals ORDER BY Id DESC LIMIT 500;";
            var result = await connection.QueryAsync<DanggeunDealItem>(sql);
            return result.AsList();
        }

        public async Task<bool> MarkDealAsImportedAsync(int dealId)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "UPDATE CrawledDeals SET IsImported = 1 WHERE Id = @Id;";
            int affected = await connection.ExecuteAsync(sql, new { Id = dealId });
            return affected > 0;
        }

        public async Task<bool> UpdatePropertyStatusAsync(int id, string status, int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "UPDATE Properties SET Status = @Status, LastCheckedAt = @Now, UpdatedAt = @Now WHERE Id = @Id AND UserId = @UserId;";
            int affected = await connection.ExecuteAsync(sql, new { Id = id, Status = status, Now = DateTime.Now, UserId = userId });
            return affected > 0;
        }

        public async Task<bool> UpdateDealStatusAsync(int dealId, string status)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "UPDATE CrawledDeals SET Status = @Status WHERE Id = @Id;";
            int affected = await connection.ExecuteAsync(sql, new { Id = dealId, Status = status });
            return affected > 0;
        }

        // --- 통화 / 컨택 이력 (ContactLogs) ---
        public async Task<List<ContactLogItem>> GetContactLogsAsync(int propertyId)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "SELECT * FROM ContactLogs WHERE PropertyId = @PropertyId ORDER BY ContactDate DESC;";
            var list = await connection.QueryAsync<ContactLogItem>(sql, new { PropertyId = propertyId });
            return list.AsList();
        }

        public async Task<bool> AddContactLogAsync(ContactLogItem log, int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = @"
                INSERT INTO ContactLogs (PropertyId, ContactDate, ContactType, Result, Memo)
                VALUES (@PropertyId, @ContactDate, @ContactType, @Result, @Memo);";
            int affected = await connection.ExecuteAsync(sql, log);

            // 통화 결과에 따라 매물 상태도 자동 연동 업데이트!
            string newStatus = log.Result switch
            {
                "생존확인" => "생존확인",
                "이미나감" => "이미나감",
                "통화중/부재" => "확인중",
                "거래진행" => "거래진행중",
                _ => "확인중"
            };

            await UpdatePropertyStatusAsync(log.PropertyId, newStatus, userId);
            return affected > 0;
        }

        // --- 손님 조건 관리 (CustomerRequests) ---
        public async Task<List<CustomerRequestItem>> GetCustomerRequestsAsync(int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "SELECT * FROM CustomerRequests WHERE UserId = @UserId ORDER BY Id DESC;";
            var list = await connection.QueryAsync<CustomerRequestItem>(sql, new { UserId = userId });
            return list.AsList();
        }

        public async Task<bool> AddCustomerRequestAsync(CustomerRequestItem req)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = @"
                INSERT INTO CustomerRequests (
                    UserId, CustomerName, CustomerPhone, PropertyType, TransactionType,
                    MinDeposit, MaxDeposit, MaxMonthlyRent, TargetRegion, Memo, IsActive, CreatedAt
                ) VALUES (
                    @UserId, @CustomerName, @CustomerPhone, @PropertyType, @TransactionType,
                    @MinDeposit, @MaxDeposit, @MaxMonthlyRent, @TargetRegion, @Memo, @IsActive, @CreatedAt
                );";
            int affected = await connection.ExecuteAsync(sql, req);
            return affected > 0;
        }

        public async Task<bool> DeleteCustomerRequestAsync(int id, int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "DELETE FROM CustomerRequests WHERE Id = @Id AND UserId = @UserId;";
            int affected = await connection.ExecuteAsync(sql, new { Id = id, UserId = userId });
            return affected > 0;
        }

        // --- 네이버 매물 관리 & 대장 검증 저장소 ---
        public async Task<List<NaverListingItem>> GetNaverListingsAsync(int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "SELECT * FROM NaverListings WHERE UserId = @UserId ORDER BY Id DESC;";
            var list = await connection.QueryAsync<NaverListingItem>(sql, new { UserId = userId });
            return list.AsList();
        }

        public async Task<NaverListingItem?> GetNaverListingByIdAsync(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "SELECT * FROM NaverListings WHERE Id = @Id;";
            return await connection.QueryFirstOrDefaultAsync<NaverListingItem>(sql, new { Id = id });
        }

        public async Task<NaverListingItem?> GetNaverListingByArticleNumberAsync(string articleNumber, int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "SELECT * FROM NaverListings WHERE ArticleNumber = @ArticleNumber AND UserId = @UserId;";
            return await connection.QueryFirstOrDefaultAsync<NaverListingItem>(sql, new { ArticleNumber = articleNumber, UserId = userId });
        }

        public async Task<int> UpsertNaverListingAsync(NaverListingItem item)
        {
            using var connection = new SqliteConnection(ConnectionString);
            var existing = await GetNaverListingByArticleNumberAsync(item.ArticleNumber, item.UserId);
            if (existing != null)
            {
                string updateSql = @"
                    UPDATE NaverListings SET
                        ArticleName = CASE WHEN @ArticleName != '' THEN @ArticleName ELSE ArticleName END,
                        TradeType = CASE WHEN @TradeType != '' THEN @TradeType ELSE TradeType END,
                        RealEstateType = CASE WHEN @RealEstateType != '' THEN @RealEstateType ELSE RealEstateType END,
                        PriceDisplay = CASE WHEN @PriceDisplay != '' THEN @PriceDisplay ELSE PriceDisplay END,
                        FloorInfo = CASE WHEN @FloorInfo != '' THEN @FloorInfo ELSE FloorInfo END,
                        AreaM2 = CASE WHEN @AreaM2 > 0 THEN @AreaM2 ELSE AreaM2 END,
                        Address = CASE WHEN @Address != '' THEN @Address ELSE Address END,
                        HasElevator = @HasElevator,
                        TotalParking = CASE WHEN @TotalParking > 0 THEN @TotalParking ELSE TotalParking END,
                        ApprovalDate = CASE WHEN @ApprovalDate != '' THEN @ApprovalDate ELSE ApprovalDate END,
                        RawJson = CASE WHEN @RawJson != '' THEN @RawJson ELSE RawJson END
                    WHERE Id = @Id;";
                item.Id = existing.Id;
                await connection.ExecuteAsync(updateSql, item);
                return existing.Id;
            }
            else
            {
                string insertSql = @"
                    INSERT INTO NaverListings (
                        UserId, ArticleNumber, ArticleName, TradeType, RealEstateType,
                        PriceDisplay, FloorInfo, AreaM2, Address, HasElevator, TotalParking,
                        ApprovalDate, RawJson, LedgerStatus, LedgerMessage, LedgerDiscrepanciesJson,
                        InspectedAt, IsImported, CreatedAt
                    ) VALUES (
                        @UserId, @ArticleNumber, @ArticleName, @TradeType, @RealEstateType,
                        @PriceDisplay, @FloorInfo, @AreaM2, @Address, @HasElevator, @TotalParking,
                        @ApprovalDate, @RawJson, @LedgerStatus, @LedgerMessage, @LedgerDiscrepanciesJson,
                        @InspectedAt, @IsImported, @CreatedAt
                    );
                    SELECT last_insert_rowid();";
                int newId = await connection.ExecuteScalarAsync<int>(insertSql, item);
                item.Id = newId;
                return newId;
            }
        }

        public async Task<bool> UpdateNaverListingLedgerResultAsync(
            int id, 
            string ledgerStatus, 
            string ledgerMessage, 
            string discrepanciesJson,
            double platArea = 0,
            double archArea = 0,
            double totArea = 0,
            double bcRat = 0,
            double vlRat = 0,
            string buildingStructure = "",
            long publicPrice = 0,
            string publicPriceYear = "",
            long hugGuaranteeLimit = 0,
            string ledgerRawJson = "")
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = @"
                UPDATE NaverListings SET
                    LedgerStatus = @LedgerStatus,
                    LedgerMessage = @LedgerMessage,
                    LedgerDiscrepanciesJson = @LedgerDiscrepanciesJson,
                    PlatArea = CASE WHEN @PlatArea > 0 THEN @PlatArea ELSE PlatArea END,
                    ArchArea = CASE WHEN @ArchArea > 0 THEN @ArchArea ELSE ArchArea END,
                    TotArea = CASE WHEN @TotArea > 0 THEN @TotArea ELSE TotArea END,
                    BcRat = CASE WHEN @BcRat > 0 THEN @BcRat ELSE BcRat END,
                    VlRat = CASE WHEN @VlRat > 0 THEN @VlRat ELSE VlRat END,
                    BuildingStructure = CASE WHEN @BuildingStructure != '' THEN @BuildingStructure ELSE BuildingStructure END,
                    PublicPrice = CASE WHEN @PublicPrice > 0 THEN @PublicPrice ELSE PublicPrice END,
                    PublicPriceYear = CASE WHEN @PublicPriceYear != '' THEN @PublicPriceYear ELSE PublicPriceYear END,
                    HugGuaranteeLimit = CASE WHEN @HugGuaranteeLimit > 0 THEN @HugGuaranteeLimit ELSE HugGuaranteeLimit END,
                    LedgerRawJson = CASE WHEN @LedgerRawJson != '' THEN @LedgerRawJson ELSE LedgerRawJson END,
                    InspectedAt = @InspectedAt
                WHERE Id = @Id;";
            int affected = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                LedgerStatus = ledgerStatus,
                LedgerMessage = ledgerMessage,
                LedgerDiscrepanciesJson = discrepanciesJson,
                PlatArea = platArea,
                ArchArea = archArea,
                TotArea = totArea,
                BcRat = bcRat,
                VlRat = vlRat,
                BuildingStructure = buildingStructure,
                PublicPrice = publicPrice,
                PublicPriceYear = publicPriceYear,
                HugGuaranteeLimit = hugGuaranteeLimit,
                LedgerRawJson = ledgerRawJson,
                InspectedAt = DateTime.Now
            });
            return affected > 0;
        }

        public async Task<bool> MarkNaverListingAsImportedAsync(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "UPDATE NaverListings SET IsImported = 1 WHERE Id = @Id;";
            int affected = await connection.ExecuteAsync(sql, new { Id = id });
            return affected > 0;
        }

        public async Task<bool> UpdateNaverListingViolationAsync(int id, bool isViolating, string? reason = null)
        {
            using var connection = new SqliteConnection(ConnectionString);
            var item = await GetNaverListingByIdAsync(id);
            if (item == null) return false;

            item.IsViolatingBuilding = isViolating;
            var discrepancies = new List<DiscrepancyItem>();
            try
            {
                if (!string.IsNullOrEmpty(item.LedgerDiscrepanciesJson))
                {
                    discrepancies = JsonSerializer.Deserialize<List<DiscrepancyItem>>(item.LedgerDiscrepanciesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<DiscrepancyItem>();
                }
            }
            catch { }

            var dItem = discrepancies.FirstOrDefault(d => d.ItemName.Contains("위반"));
            if (dItem == null)
            {
                dItem = new DiscrepancyItem { ItemName = "위반건축물 여부", NaverValue = "정상" };
                discrepancies.Insert(0, dItem);
            }

            if (isViolating)
            {
                dItem.Status = "Danger";
                dItem.LedgerValue = "🚨 위반건축물 등재 (구청 공식 대장 확인 완료)";
                dItem.Note = string.IsNullOrEmpty(reason)
                    ? "실제 관할 구청 건축물대장에 [위반건축물]로 등재되어 있습니다! (이행강제금 부과 대상 및 HUG 전세보증보험/전세대출 전면 불가)"
                    : reason;
                item.LedgerStatus = "Danger";
                item.LedgerMessage = $"🚨 [위반건축물 등재 건물] {(string.IsNullOrEmpty(reason) ? "실제 관할 구청 건축물대장에 위반건축물로 등재된 매물입니다!" : reason)} 전세대출 및 보증보험 불가!";
            }
            else
            {
                dItem.Status = "Match";
                dItem.LedgerValue = "✅ 정상 (위반 없음)";
                dItem.Note = "사용자 검토를 통해 정상 건축물로 확인되었습니다.";
                bool hasOtherDanger = discrepancies.Any(d => d != dItem && d.Status == "Danger");
                bool hasOtherWarning = discrepancies.Any(d => d != dItem && d.Status == "Warning");
                item.LedgerStatus = hasOtherDanger ? "Danger" : (hasOtherWarning ? "Warning" : "Safe");
                item.LedgerMessage = hasOtherDanger ? "⚠️ 다른 정보 불일치 항목이 존재합니다." : "✅ 네이버 광고 정보가 실제 건축물대장과 일치합니다.";
            }

            string updatedDiscrepanciesJson = JsonSerializer.Serialize(discrepancies);
            string sql = @"
                UPDATE NaverListings SET
                    IsViolatingBuilding = @IsViolatingBuilding,
                    LedgerStatus = @LedgerStatus,
                    LedgerMessage = @LedgerMessage,
                    LedgerDiscrepanciesJson = @LedgerDiscrepanciesJson,
                    InspectedAt = @InspectedAt
                WHERE Id = @Id;";

            int affected = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                IsViolatingBuilding = isViolating ? 1 : 0,
                LedgerStatus = item.LedgerStatus,
                LedgerMessage = item.LedgerMessage,
                LedgerDiscrepanciesJson = updatedDiscrepanciesJson,
                InspectedAt = DateTime.Now
            });
            return affected > 0;
        }

        public async Task<bool> DeleteNaverListingAsync(int id, int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "DELETE FROM NaverListings WHERE Id = @Id AND UserId = @UserId;";
            int affected = await connection.ExecuteAsync(sql, new { Id = id, UserId = userId });
            return affected > 0;
        }

        public async Task<bool> ClearNaverListingsAsync(int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "DELETE FROM NaverListings WHERE UserId = @UserId;";
            int affected = await connection.ExecuteAsync(sql, new { UserId = userId });
            return affected > 0;
        }

        // --- 중개사 및 네이버 연동 설정 ---
        public async Task<RealtorSettingsItem?> GetRealtorSettingsAsync(int userId = 1)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = "SELECT * FROM RealtorSettings WHERE UserId = @UserId;";
            return await connection.QueryFirstOrDefaultAsync<RealtorSettingsItem>(sql, new { UserId = userId });
        }

        public async Task<bool> SaveRealtorSettingsAsync(RealtorSettingsItem settings)
        {
            using var connection = new SqliteConnection(ConnectionString);
            string sql = @"
                INSERT INTO RealtorSettings (UserId, RealtorName, AgencyName, RealtorId, NaverId, NaverPassword, AutoSync, UpdatedAt)
                VALUES (@UserId, @RealtorName, @AgencyName, @RealtorId, @NaverId, @NaverPassword, @AutoSync, @UpdatedAt)
                ON CONFLICT(UserId) DO UPDATE SET
                    RealtorName = excluded.RealtorName,
                    AgencyName = excluded.AgencyName,
                    RealtorId = excluded.RealtorId,
                    NaverId = excluded.NaverId,
                    NaverPassword = excluded.NaverPassword,
                    AutoSync = excluded.AutoSync,
                    UpdatedAt = excluded.UpdatedAt;";
            settings.UpdatedAt = DateTime.Now;
            int affected = await connection.ExecuteAsync(sql, settings);
            return affected > 0;
        }
    }
}
