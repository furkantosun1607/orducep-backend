using Microsoft.EntityFrameworkCore;
using OrduCep.Infrastructure.Persistence;

namespace OrduCep.API;

/// <summary>
/// EnsureCreatedAsync() mevcut tablolara yeni kolon ekleyemediği için
/// bu sınıf uygulama ayağa kalkarken eksik kolonları ALTER TABLE ile ekler.
/// </summary>
public static class SchemaMigrator
{
    public static async Task RunAsync(OrduCepDbContext db)
    {
        var migrations = new List<(string checkSql, string alterSql)>
        {
            // ── Orduevleri ──
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Orduevleri' AND COLUMN_NAME = 'CreatedAt'",
                "ALTER TABLE `Orduevleri` ADD COLUMN `CreatedAt` DATETIME NOT NULL DEFAULT '2024-01-01 00:00:00'"
            ),
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Orduevleri' AND COLUMN_NAME = 'UpdatedAt'",
                "ALTER TABLE `Orduevleri` ADD COLUMN `UpdatedAt` DATETIME NULL"
            ),
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Orduevleri' AND COLUMN_NAME = 'ScrapedSourceId'",
                "ALTER TABLE `Orduevleri` ADD COLUMN `ScrapedSourceId` INT NULL"
            ),
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Orduevleri' AND COLUMN_NAME = 'Slug'",
                "ALTER TABLE `Orduevleri` ADD COLUMN `Slug` VARCHAR(255) NOT NULL DEFAULT ''"
            ),
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Orduevleri' AND COLUMN_NAME = 'SourceUrl'",
                "ALTER TABLE `Orduevleri` ADD COLUMN `SourceUrl` LONGTEXT NULL"
            ),
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Orduevleri' AND COLUMN_NAME = 'FeaturedImageUrl'",
                "ALTER TABLE `Orduevleri` ADD COLUMN `FeaturedImageUrl` LONGTEXT NULL"
            ),
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Orduevleri' AND COLUMN_NAME = 'FeaturedImageLocalPath'",
                "ALTER TABLE `Orduevleri` ADD COLUMN `FeaturedImageLocalPath` LONGTEXT NULL"
            ),
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Orduevleri' AND COLUMN_NAME = 'Amenities'",
                "ALTER TABLE `Orduevleri` ADD COLUMN `Amenities` LONGTEXT NULL"
            ),
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Orduevleri' AND COLUMN_NAME = 'ScrapedMetadataJson'",
                "ALTER TABLE `Orduevleri` ADD COLUMN `ScrapedMetadataJson` LONGTEXT NULL"
            ),

            // ── Facilities ──
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Facilities' AND COLUMN_NAME = 'Image'",
                "ALTER TABLE `Facilities` ADD COLUMN `Image` LONGTEXT NULL"
            ),
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Facilities' AND COLUMN_NAME = 'ClosedDays'",
                "ALTER TABLE `Facilities` ADD COLUMN `ClosedDays` VARCHAR(500) NOT NULL DEFAULT ''"
            ),

            // ── FacilityStaffs ──
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'FacilityStaffs' AND COLUMN_NAME = 'Name'",
                "ALTER TABLE `FacilityStaffs` ADD COLUMN `Name` VARCHAR(255) NOT NULL DEFAULT ''"
            ),

            // ── MilitaryIdentityUsers ──
            (
                "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'MilitaryIdentityUsers' AND COLUMN_NAME = 'PhoneNumber'",
                "ALTER TABLE `MilitaryIdentityUsers` ADD COLUMN `PhoneNumber` VARCHAR(32) NOT NULL DEFAULT ''"
            ),
        };

        foreach (var (checkSql, alterSql) in migrations)
        {
            var count = await db.Database.SqlQueryRaw<long>(checkSql).FirstOrDefaultAsync();
            if (count == 0)
            {
                Console.WriteLine($"[SchemaMigrator] Kolon eksik, ekleniyor: {alterSql[..Math.Min(80, alterSql.Length)]}...");
                await db.Database.ExecuteSqlRawAsync(alterSql);
            }
        }

        await NormalizeOrdueviScrapedColumnsAsync(db);

        // UserId kolonu nullable değilse nullable yap
        await MakeUserIdNullableAsync(db);

        Console.WriteLine("[SchemaMigrator] Schema kontrolü tamamlandı.");
    }

    private static async Task NormalizeOrdueviScrapedColumnsAsync(OrduCepDbContext db)
    {
        var updates = new[]
        {
            "UPDATE `Orduevleri` SET `Name` = '' WHERE `Name` IS NULL",
            "UPDATE `Orduevleri` SET `Description` = '' WHERE `Description` IS NULL",
            "UPDATE `Orduevleri` SET `Address` = '' WHERE `Address` IS NULL",
            "UPDATE `Orduevleri` SET `ContactNumber` = '' WHERE `ContactNumber` IS NULL",
            "UPDATE `Orduevleri` SET `AdminUserId` = '' WHERE `AdminUserId` IS NULL",
            "UPDATE `Orduevleri` SET `CreatedAt` = '2024-01-01 00:00:00' WHERE `CreatedAt` IS NULL",
            "UPDATE `Orduevleri` SET `Slug` = '' WHERE `Slug` IS NULL",
            "UPDATE `Orduevleri` SET `SourceUrl` = '' WHERE `SourceUrl` IS NULL",
            "UPDATE `Orduevleri` SET `FeaturedImageUrl` = '' WHERE `FeaturedImageUrl` IS NULL",
            "UPDATE `Orduevleri` SET `FeaturedImageLocalPath` = '' WHERE `FeaturedImageLocalPath` IS NULL",
            "UPDATE `Orduevleri` SET `Amenities` = '' WHERE `Amenities` IS NULL",
            "UPDATE `Orduevleri` SET `ScrapedMetadataJson` = '' WHERE `ScrapedMetadataJson` IS NULL",
            "UPDATE `Facilities` SET `Name` = '' WHERE `Name` IS NULL",
            "UPDATE `Facilities` SET `Description` = '' WHERE `Description` IS NULL",
            "UPDATE `Facilities` SET `Icon` = '' WHERE `Icon` IS NULL",
            "UPDATE `Facilities` SET `ClosedDays` = '' WHERE `ClosedDays` IS NULL",
            "UPDATE `MilitaryIdentityUsers` SET `PhoneNumber` = '' WHERE `PhoneNumber` IS NULL"
        };

        foreach (var updateSql in updates)
        {
            await db.Database.ExecuteSqlRawAsync(updateSql);
        }
    }

    private static async Task MakeUserIdNullableAsync(OrduCepDbContext db)
    {
        // IS_NULLABLE kolonunu AS Value ile alias'la
        const string checkSql = """
            SELECT IS_NULLABLE AS `Value`
            FROM information_schema.COLUMNS 
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = 'FacilityStaffs' 
              AND COLUMN_NAME = 'UserId'
            LIMIT 1
            """;

        var isNullable = await db.Database.SqlQueryRaw<string>(checkSql).FirstOrDefaultAsync();

        if (isNullable == "NO")
        {
            Console.WriteLine("[SchemaMigrator] FacilityStaffs.UserId nullable yapılıyor...");
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `FacilityStaffs` MODIFY COLUMN `UserId` VARCHAR(255) NULL"
            );
        }
    }
}
