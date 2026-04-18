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

        // UserId kolonu nullable değilse nullable yap
        await MakeUserIdNullableAsync(db);

        Console.WriteLine("[SchemaMigrator] Schema kontrolü tamamlandı.");
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
