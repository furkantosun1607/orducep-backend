using Microsoft.EntityFrameworkCore;
using OrduCep.Infrastructure.Persistence;
using OrduCep.Domain.Entities;
using OrduCep.Domain.Enums;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OrduCep.API;

public static class SeedData
{
    private sealed record ScrapedOrdueviRecord(
        int SourceId,
        string Title,
        string Slug,
        string SourceUrl,
        string Description,
        string Address,
        string ContactNumber,
        string Amenities,
        string FeaturedImageUrl,
        string FeaturedImageLocalPath,
        string RawJson);

    private sealed record AmenityFacilityTemplate(
        string Name,
        string Description,
        FacilityCategory Category,
        AppointmentMode AppointmentMode,
        int MaxConcurrency,
        int BufferMinutes,
        int DefaultSlotDurationMinutes,
        TimeSpan OpeningTime,
        TimeSpan ClosingTime,
        string Icon,
        string[] Keywords);

    private sealed record DefaultFacilityServiceCatalogItem(
        string ServiceName,
        decimal Price,
        int DurationMinutes,
        int BufferMinutes = 0);

    private static readonly AmenityFacilityTemplate[] AmenityFacilityTemplates =
    {
        new("Konaklama", "Bu tesiste konaklama hizmeti sunulmaktadır.", FacilityCategory.SpaceBased, AppointmentMode.AppointmentOnly, 20, 0, 1440, new TimeSpan(14, 0, 0), new TimeSpan(12, 0, 0), "bed", new[] { "konaklama", "misafirhane", "misafirhanesi" }),
        new("Restoran", "Tesisin yeme-içme birimi.", FacilityCategory.CapacityBased, AppointmentMode.Mixed, 40, 10, 60, new TimeSpan(8, 0, 0), new TimeSpan(22, 0, 0), "utensils", new[] { "restoran", "restorani", "yemek salonu", "yemekhane", "lokanta", "lokantasi" }),
        new("Izgara Salonu", "Izgara ve sıcak yemek servisi sunan salon.", FacilityCategory.CapacityBased, AppointmentMode.Mixed, 20, 10, 60, new TimeSpan(11, 0, 0), new TimeSpan(22, 0, 0), "flame", new[] { "izgara", "ızgara", "grill" }),
        new("Pide-Lahmacun Salonu", "Pide ve lahmacun servisi bulunan yeme-içme birimi.", FacilityCategory.CapacityBased, AppointmentMode.Mixed, 16, 10, 60, new TimeSpan(11, 0, 0), new TimeSpan(21, 0, 0), "pizza", new[] { "pide", "lahmacun" }),
        new("Fast-food", "Hızlı servis yiyecek birimi.", FacilityCategory.CapacityBased, AppointmentMode.WalkInOnly, 20, 0, 30, new TimeSpan(10, 0, 0), new TimeSpan(22, 0, 0), "sandwich", new[] { "fast food", "fastfood" }),
        new("Kafeterya", "Kafeterya ve dinlenme alanı.", FacilityCategory.CapacityBased, AppointmentMode.WalkInOnly, 30, 0, 45, new TimeSpan(8, 0, 0), new TimeSpan(22, 0, 0), "coffee", new[] { "kafeterya", "kafe", "cafe" }),
        new("Pastane", "Pastane ve tatlı servisi.", FacilityCategory.CapacityBased, AppointmentMode.WalkInOnly, 20, 0, 45, new TimeSpan(8, 0, 0), new TimeSpan(22, 0, 0), "cake", new[] { "pastane", "acik pastane", "kapali pastane" }),
        new("Bar", "Sosyal içecek servisi alanı.", FacilityCategory.SpaceBased, AppointmentMode.WalkInOnly, 20, 0, 60, new TimeSpan(16, 0, 0), new TimeSpan(23, 0, 0), "glass", new[] { "bar" }),
        new("Düğün Salonu", "Düğün, davet ve organizasyon salonu.", FacilityCategory.SpaceBased, AppointmentMode.AppointmentOnly, 1, 60, 240, new TimeSpan(10, 0, 0), new TimeSpan(23, 0, 0), "party", new[] { "dugun salonu", "dugun" }),
        new("Toplantı Salonu", "Toplantı ve organizasyonlar için ayrılmış salon.", FacilityCategory.SpaceBased, AppointmentMode.AppointmentOnly, 4, 30, 120, new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0), "presentation", new[] { "toplanti salonu", "toplanti salonlari", "konferans salonu", "organizasyonlar duzenlemek" }),
        new("Berber", "Berber hizmetleri.", FacilityCategory.TimeBased, AppointmentMode.AppointmentOnly, 2, 5, 30, new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0), "scissors", new[] { "berber" }),
        new("Kuaför", "Kuaför hizmetleri.", FacilityCategory.TimeBased, AppointmentMode.AppointmentOnly, 2, 5, 45, new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0), "sparkles", new[] { "kuafor", "kuaforu", "kadin kuaforu", "erkek kuaforu" }),
        new("Hamam", "Hamam hizmeti.", FacilityCategory.TimeBased, AppointmentMode.Mixed, 8, 15, 60, new TimeSpan(9, 0, 0), new TimeSpan(21, 0, 0), "bath", new[] { "hamam" }),
        new("Sauna", "Sauna kullanım alanı.", FacilityCategory.TimeBased, AppointmentMode.Mixed, 6, 15, 45, new TimeSpan(9, 0, 0), new TimeSpan(21, 0, 0), "waves", new[] { "sauna" }),
        new("Termal Banyo", "Termal banyo hizmeti.", FacilityCategory.TimeBased, AppointmentMode.Mixed, 6, 15, 60, new TimeSpan(9, 0, 0), new TimeSpan(21, 0, 0), "hot-springs", new[] { "termal banyo" }),
        new("Termal Havuz", "Termal havuz kullanım alanı.", FacilityCategory.CapacityBased, AppointmentMode.Mixed, 20, 15, 90, new TimeSpan(9, 0, 0), new TimeSpan(21, 0, 0), "pool", new[] { "termal havuz" }),
        new("Spor Salonu", "Spor salonu ve fitness alanı.", FacilityCategory.TimeBased, AppointmentMode.Mixed, 12, 10, 60, new TimeSpan(7, 0, 0), new TimeSpan(22, 0, 0), "dumbbell", new[] { "spor salonu", "fitness salonu", "fitness" }),
        new("Spor Alanı", "Açık veya kapalı spor alanı.", FacilityCategory.CapacityBased, AppointmentMode.Mixed, 20, 10, 60, new TimeSpan(8, 0, 0), new TimeSpan(22, 0, 0), "activity", new[] { "spor alani", "hali saha", "hali sahalar", "basketbol sahasi", "basketbol sahalari", "futbol sahasi", "futbol sahalari" }),
        new("Yüzme Havuzu", "Yüzme havuzu kullanım alanı.", FacilityCategory.CapacityBased, AppointmentMode.Mixed, 30, 15, 90, new TimeSpan(9, 0, 0), new TimeSpan(20, 0, 0), "pool", new[] { "yuzme havuzu", "olimpik" }),
        new("Çocuk Havuzu", "Çocuklar için havuz alanı.", FacilityCategory.CapacityBased, AppointmentMode.WalkInOnly, 20, 0, 60, new TimeSpan(9, 0, 0), new TimeSpan(20, 0, 0), "child-pool", new[] { "cocuk havuzu" }),
        new("Tenis Kortu", "Tenis kortu kullanım alanı.", FacilityCategory.SpaceBased, AppointmentMode.AppointmentOnly, 2, 15, 60, new TimeSpan(8, 0, 0), new TimeSpan(22, 0, 0), "tennis", new[] { "tenis kortu", "tenis kortlari" }),
        new("Mini Golf", "Mini golf alanı.", FacilityCategory.SpaceBased, AppointmentMode.Mixed, 6, 10, 60, new TimeSpan(9, 0, 0), new TimeSpan(20, 0, 0), "golf", new[] { "mini golf" }),
        new("Oyun Salonu", "Bilardo, tavla veya oyun alanı.", FacilityCategory.SpaceBased, AppointmentMode.WalkInOnly, 20, 0, 60, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0), "gamepad", new[] { "oyun salonu", "bilardo", "playstation", "tavla" }),
        new("Çocuk Oyun Alanı", "Çocuklar için oyun alanı.", FacilityCategory.SpaceBased, AppointmentMode.WalkInOnly, 20, 0, 60, new TimeSpan(9, 0, 0), new TimeSpan(21, 0, 0), "baby", new[] { "cocuk oyun alani" }),
        new("Okuma Salonu", "Okuma ve dinlenme salonu.", FacilityCategory.SpaceBased, AppointmentMode.WalkInOnly, 12, 0, 60, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0), "book-open", new[] { "okuma salonu" }),
        new("Market", "Market ve alışveriş birimi.", FacilityCategory.CapacityBased, AppointmentMode.WalkInOnly, 10, 0, 30, new TimeSpan(9, 0, 0), new TimeSpan(21, 0, 0), "shopping-bag", new[] { "market", "mini market", "alisveris birimi" }),
        new("Kuru Temizleme", "Kuru temizleme hizmeti.", FacilityCategory.TimeBased, AppointmentMode.Mixed, 2, 5, 30, new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0), "shirt", new[] { "kuru temizleme" }),
        new("Terzi", "Terzi hizmeti.", FacilityCategory.TimeBased, AppointmentMode.Mixed, 2, 5, 30, new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0), "needle", new[] { "terzi" }),
        new("Araç Yıkama", "Araç yıkama hizmeti.", FacilityCategory.TimeBased, AppointmentMode.Mixed, 3, 10, 45, new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0), "car", new[] { "arac yikama", "oto yikama" }),
        new("Özel Plaj", "Plaj kullanım alanı.", FacilityCategory.CapacityBased, AppointmentMode.WalkInOnly, 40, 0, 120, new TimeSpan(9, 0, 0), new TimeSpan(20, 0, 0), "umbrella", new[] { "ozel plaj", "plaj yakinligi" }),
        new("Su Sporları", "Su sporları etkinlik alanı.", FacilityCategory.SpaceBased, AppointmentMode.AppointmentOnly, 8, 15, 60, new TimeSpan(9, 0, 0), new TimeSpan(20, 0, 0), "sailboat", new[] { "su sporlari" })
    };

    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '\"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrduCepDbContext>();

        // ── 1. Orduevleri (CSV'den) — DOKUNULMAZ ──
        if (!await context.Orduevleri.AnyAsync())
        {
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "orduevleri.csv");
            if (File.Exists(csvPath))
            {
                var lines = await File.ReadAllLinesAsync(csvPath);

                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = SplitCsvLine(line);
                    if (parts.Length < 6)
                        continue;

                    if (!Guid.TryParse(parts[0], out var id))
                        continue;

                    var orduevi = new Orduevi
                    {
                        Id = id,
                        Name = parts[1],
                        Description = parts[2],
                        Address = parts[3],
                        ContactNumber = parts[4],
                        AdminUserId = parts[5]
                    };

                    context.Orduevleri.Add(orduevi);
                }

                await context.SaveChangesAsync();
            }
        }

        await ImportScrapedOrduevleriAsync(context);

        // ── 2. Örnek Tesisler — İlk orduevine berber, pide salonu, meyhane ekle ──
        if (!await context.Facilities.AnyAsync())
        {
            var firstOrduevi = await context.Orduevleri.FirstOrDefaultAsync();
            if (firstOrduevi == null) return;

            // ── BERBER (TimeBased, AppointmentOnly, 3 koltuk) ──
            var berberId = Guid.NewGuid();
            var berber = new Facility
            {
                Id = berberId,
                OrdueviId = firstOrduevi.Id,
                Name = "Yıldız Berber Salonu",
                Category = FacilityCategory.TimeBased,
                AppointmentMode = AppointmentMode.AppointmentOnly,
                MaxConcurrency = 3,
                BufferMinutes = 5,
                DefaultSlotDurationMinutes = 30,
                OpeningTime = new TimeSpan(8, 30, 0),
                ClosingTime = new TimeSpan(17, 0, 0),
                IsActive = true,
                Description = "Erkek ve çocuk saç kesimi, sakal tıraşı.",
                Icon = "scissors"
            };

            // ── PİDE SALONU (CapacityBased, Mixed, 5 masa) ──
            var pideId = Guid.NewGuid();
            var pideSalonu = new Facility
            {
                Id = pideId,
                OrdueviId = firstOrduevi.Id,
                Name = "Açık Teras Pide Salonu",
                Category = FacilityCategory.CapacityBased,
                AppointmentMode = AppointmentMode.Mixed,
                MaxConcurrency = 5,
                BufferMinutes = 10,
                DefaultSlotDurationMinutes = 60,
                OpeningTime = new TimeSpan(11, 0, 0),
                ClosingTime = new TimeSpan(21, 0, 0),
                IsActive = true,
                Description = "Pide ve fırın ürünleri servisi.",
                Icon = "pizza-slice"
            };

            // ── MEYHANE (SpaceBased, AppointmentOnly, 4 masa) ──
            var meyhaneId = Guid.NewGuid();
            var meyhane = new Facility
            {
                Id = meyhaneId,
                OrdueviId = firstOrduevi.Id,
                Name = "Sahil Meyhane",
                Category = FacilityCategory.SpaceBased,
                AppointmentMode = AppointmentMode.AppointmentOnly,
                MaxConcurrency = 4,
                BufferMinutes = 0,
                DefaultSlotDurationMinutes = 120,
                OpeningTime = new TimeSpan(18, 0, 0),
                ClosingTime = new TimeSpan(23, 0, 0),
                IsActive = true,
                Description = "Meyhane ve canlı müzik.",
                Icon = "wine-glass"
            };

            context.Facilities.AddRange(berber, pideSalonu, meyhane);
            await context.SaveChangesAsync();

            // ── KAYNAKLAR (Resources) ──

            // Berber: 3 koltuk
            context.Resources.AddRange(
                new Resource { Id = Guid.NewGuid(), FacilityId = berberId, Name = "Ali Usta", Type = ResourceType.Staff, Capacity = 1, IsActive = true },
                new Resource { Id = Guid.NewGuid(), FacilityId = berberId, Name = "Mehmet Usta", Type = ResourceType.Staff, Capacity = 1, IsActive = true },
                new Resource { Id = Guid.NewGuid(), FacilityId = berberId, Name = "Hasan Usta", Type = ResourceType.Staff, Capacity = 1, IsActive = true }
            );

            // Pide Salonu: 5 masa (farklı kapasiteler)
            context.Resources.AddRange(
                new Resource { Id = Guid.NewGuid(), FacilityId = pideId, Name = "Masa 1", Type = ResourceType.Table, Capacity = 2, IsActive = true, Tags = "Teras" },
                new Resource { Id = Guid.NewGuid(), FacilityId = pideId, Name = "Masa 2", Type = ResourceType.Table, Capacity = 4, IsActive = true, Tags = "Teras" },
                new Resource { Id = Guid.NewGuid(), FacilityId = pideId, Name = "Masa 3", Type = ResourceType.Table, Capacity = 4, IsActive = true, Tags = "İç Salon" },
                new Resource { Id = Guid.NewGuid(), FacilityId = pideId, Name = "Masa 4", Type = ResourceType.Table, Capacity = 6, IsActive = true, Tags = "İç Salon" },
                new Resource { Id = Guid.NewGuid(), FacilityId = pideId, Name = "Masa 5", Type = ResourceType.Table, Capacity = 8, IsActive = true, Tags = "VIP" }
            );

            // Meyhane: 4 masa (VIP dahil)
            context.Resources.AddRange(
                new Resource { Id = Guid.NewGuid(), FacilityId = meyhaneId, Name = "Sahne Önü 1", Type = ResourceType.Table, Capacity = 4, IsActive = true, Tags = "Sahne Önü" },
                new Resource { Id = Guid.NewGuid(), FacilityId = meyhaneId, Name = "Sahne Önü 2", Type = ResourceType.Table, Capacity = 4, IsActive = true, Tags = "Sahne Önü" },
                new Resource { Id = Guid.NewGuid(), FacilityId = meyhaneId, Name = "Arka Bölüm 1", Type = ResourceType.Table, Capacity = 6, IsActive = true },
                new Resource { Id = Guid.NewGuid(), FacilityId = meyhaneId, Name = "VIP Oda", Type = ResourceType.Room, Capacity = 10, IsActive = true, Tags = "VIP" }
            );

            await context.SaveChangesAsync();

            // ── HİZMETLER (Services) ──

            // Berber hizmetleri
            context.FacilityServices.AddRange(
                new FacilityService
                {
                    Id = Guid.NewGuid(), FacilityId = berberId,
                    ServiceName = "Saç Kesimi", Price = 45.00m,
                    DurationMinutes = 30, BufferMinutes = 5, IsActive = true
                },
                new FacilityService
                {
                    Id = Guid.NewGuid(), FacilityId = berberId,
                    ServiceName = "Sakal Tıraşı", Price = 25.00m,
                    DurationMinutes = 15, BufferMinutes = 5, IsActive = true
                },
                new FacilityService
                {
                    Id = Guid.NewGuid(), FacilityId = berberId,
                    ServiceName = "Saç & Sakal", Price = 60.00m,
                    DurationMinutes = 40, BufferMinutes = 5, IsActive = true
                }
            );

            // Pide Salonu hizmetleri
            context.FacilityServices.AddRange(
                new FacilityService
                {
                    Id = Guid.NewGuid(), FacilityId = pideId,
                    ServiceName = "Kıymalı Pide", Price = 85.00m,
                    DurationMinutes = 0, BufferMinutes = 0, IsActive = true
                },
                new FacilityService
                {
                    Id = Guid.NewGuid(), FacilityId = pideId,
                    ServiceName = "Kuşbaşılı Pide", Price = 110.00m,
                    DurationMinutes = 0, BufferMinutes = 0, IsActive = true
                }
            );

            await context.SaveChangesAsync();
        }

        await SyncFacilitiesFromAmenitiesAsync(context);
        await EnsureDefaultFacilityServicesAsync(context);
        await EnsureDefaultFacilityResourcesAsync(context);

        // ── 3. Varsayılan Kullanıcı ──
        if (!await context.MilitaryIdentityUsers.AnyAsync())
        {
            var testUser = new MilitaryIdentityUser
            {
                Id = Guid.NewGuid(),
                IdentityNumber = "11223344556",
                PasswordHash = PasswordHashing.Hash("password123"),
                FirstName = "Test",
                LastName = "Kullanıcı",
                PhoneNumber = "05000000000",
                Relation = "Kendisi",
                OwnerRank = "Yüzbaşı",
                CreatedAtUtc = DateTime.UtcNow
            };

            context.MilitaryIdentityUsers.Add(testUser);
            await context.SaveChangesAsync();
        }
    }

    private static async Task ImportScrapedOrduevleriAsync(OrduCepDbContext context)
    {
        var scrapedPath = FindFileInParentTree("scraped_data/orduevleri_askeri_gazinolar.json");
        if (scrapedPath == null)
        {
            Console.WriteLine("[SeedData] scraped_data bulunamadı, orduevi/gazino importu atlandı.");
            return;
        }

        await using var stream = File.OpenRead(scrapedPath);
        using var document = await JsonDocument.ParseAsync(stream);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return;

        var existing = await context.Orduevleri.ToListAsync();
        var imported = 0;
        var added = 0;
        var updated = 0;
        var usedOrdueviIds = new HashSet<Guid>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var record = CreateScrapedRecord(item);
            if (string.IsNullOrWhiteSpace(record.Title))
                continue;

            var orduevi = FindMatchingOrduevi(record, existing, usedOrdueviIds);
            if (orduevi == null)
            {
                var id = CreateStableGuid(record);
                if (existing.Any(o => o.Id == id))
                    id = Guid.NewGuid();

                orduevi = new Orduevi
                {
                    Id = id,
                    AdminUserId = "admin-123",
                    CreatedAt = DateTime.UtcNow
                };
                context.Orduevleri.Add(orduevi);
                existing.Add(orduevi);
                added++;
            }

            if (ApplyScrapedRecord(orduevi, record) && context.Entry(orduevi).State != EntityState.Added)
                updated++;

            usedOrdueviIds.Add(orduevi.Id);
            imported++;
        }

        if (imported > 0)
            await context.SaveChangesAsync();

        Console.WriteLine($"[SeedData] scraped_data importu tamamlandı. Okunan: {imported}, eklenen: {added}, güncellenen: {updated}.");
    }

    private static async Task SyncFacilitiesFromAmenitiesAsync(OrduCepDbContext context)
    {
        var orduevleri = await context.Orduevleri
            .Include(o => o.Facilities)
            .ToListAsync();

        var added = 0;
        var updated = 0;

        foreach (var orduevi in orduevleri)
        {
            var existingFacilities = orduevi.Facilities.ToList();

            foreach (var existingFacility in existingFacilities)
            {
                var matchingTemplate = AmenityFacilityTemplates
                    .FirstOrDefault(template => FacilityLooksLikeTemplate(existingFacility, template));

                if (matchingTemplate != null && ApplyTemplatePresentation(existingFacility, matchingTemplate))
                    updated++;
            }

            var searchableText = NormalizeText(string.Join(' ',
                orduevi.Name,
                orduevi.Amenities,
                orduevi.Description,
                orduevi.ScrapedMetadataJson));

            if (string.IsNullOrWhiteSpace(searchableText))
                continue;

            foreach (var template in AmenityFacilityTemplates)
            {
                if (!TemplateMatches(searchableText, template))
                    continue;

                var existingFacility = existingFacilities.FirstOrDefault(f => FacilityLooksLikeTemplate(f, template));
                if (existingFacility != null)
                {
                    if (ApplyTemplatePresentation(existingFacility, template))
                        updated++;
                    continue;
                }

                var facility = CreateFacilityFromTemplate(orduevi.Id, template);
                context.Facilities.Add(facility);
                existingFacilities.Add(facility);
                added++;
            }
        }

        if (added > 0 || updated > 0)
            await context.SaveChangesAsync();

        Console.WriteLine($"[SeedData] Olanaklardan tesis servisi üretimi tamamlandı. Eklenen: {added}, güncellenen: {updated}.");
    }

    private static async Task EnsureDefaultFacilityServicesAsync(OrduCepDbContext context)
    {
        var facilities = await context.Facilities
            .Include(f => f.Services)
            .ToListAsync();

        var added = 0;

        foreach (var facility in facilities)
        {
            if (facility.Services.Any())
                continue;

            foreach (var item in BuildDefaultServiceCatalog(facility))
            {
                context.FacilityServices.Add(new FacilityService
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facility.Id,
                    ServiceName = item.ServiceName,
                    Price = item.Price,
                    DurationMinutes = item.DurationMinutes,
                    BufferMinutes = item.BufferMinutes,
                    IsActive = true
                });
                added++;
            }
        }

        if (added > 0)
            await context.SaveChangesAsync();

        Console.WriteLine($"[SeedData] Varsayılan hizmet/fiyat katalogları tamamlandı. Eklenen: {added}.");
    }

    private static async Task EnsureDefaultFacilityResourcesAsync(OrduCepDbContext context)
    {
        var facilities = await context.Facilities
            .Include(f => f.Resources)
            .ToListAsync();

        var added = 0;

        foreach (var facility in facilities)
        {
            if (facility.Resources.Any() || !NeedsSelectableStaffResource(facility))
                continue;

            var count = Math.Clamp(facility.MaxConcurrency, 1, 12);
            for (var i = 1; i <= count; i++)
            {
                context.Resources.Add(new Resource
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facility.Id,
                    Name = $"Berber {i}",
                    Type = ResourceType.Staff,
                    Capacity = 1,
                    IsActive = true
                });
                added++;
            }
        }

        if (added > 0)
            await context.SaveChangesAsync();

        Console.WriteLine($"[SeedData] Varsayılan berber/personel kaynakları tamamlandı. Eklenen: {added}.");
    }

    private static bool NeedsSelectableStaffResource(Facility facility)
    {
        var key = NormalizeText(string.Join(' ', facility.Icon, facility.Name, facility.Description));
        return key.Contains("berber") || key.Contains("kuafor") || key.Contains("terzi") || key.Contains("scissors");
    }

    private static IReadOnlyList<DefaultFacilityServiceCatalogItem> BuildDefaultServiceCatalog(Facility facility)
    {
        var key = NormalizeText(string.Join(' ', facility.Icon, facility.Name, facility.Description));

        if (key.Contains("berber"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Saç Kesimi", 120m, 30, 5),
                new DefaultFacilityServiceCatalogItem("Sakal Tıraşı", 80m, 20, 5),
                new DefaultFacilityServiceCatalogItem("Saç & Sakal", 180m, 45, 5),
                new DefaultFacilityServiceCatalogItem("Çocuk Saç Kesimi", 100m, 25, 5),
                new DefaultFacilityServiceCatalogItem("Yıkama & Fön", 70m, 20, 5)
            };

        if (key.Contains("kuafor"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Saç Kesimi", 180m, 45, 10),
                new DefaultFacilityServiceCatalogItem("Fön", 120m, 30, 5),
                new DefaultFacilityServiceCatalogItem("Saç Bakımı", 250m, 60, 10),
                new DefaultFacilityServiceCatalogItem("Manikür", 150m, 40, 5),
                new DefaultFacilityServiceCatalogItem("Pedikür", 180m, 45, 5)
            };

        if (key.Contains("pide") || key.Contains("lahmacun") || key.Contains("pizza"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Kıymalı Pide", 120m, 0),
                new DefaultFacilityServiceCatalogItem("Kuşbaşılı Pide", 150m, 0),
                new DefaultFacilityServiceCatalogItem("Kaşarlı Pide", 115m, 0),
                new DefaultFacilityServiceCatalogItem("Lahmacun", 55m, 0),
                new DefaultFacilityServiceCatalogItem("Ayran", 20m, 0)
            };

        if (key.Contains("restoran") || key.Contains("yemek") || key.Contains("yemekhane") || key.Contains("lokanta") || key.Contains("utensils"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Günün Çorbası", 45m, 0),
                new DefaultFacilityServiceCatalogItem("Izgara Köfte", 180m, 0),
                new DefaultFacilityServiceCatalogItem("Tavuk Şiş", 150m, 0),
                new DefaultFacilityServiceCatalogItem("Et Sote", 220m, 0),
                new DefaultFacilityServiceCatalogItem("Pilav / Makarna", 55m, 0),
                new DefaultFacilityServiceCatalogItem("Salata", 70m, 0)
            };

        if (key.Contains("izgara") || key.Contains("grill") || key.Contains("flame"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Izgara Köfte", 180m, 0),
                new DefaultFacilityServiceCatalogItem("Tavuk Izgara", 155m, 0),
                new DefaultFacilityServiceCatalogItem("Karışık Izgara", 280m, 0),
                new DefaultFacilityServiceCatalogItem("Patates Kızartması", 65m, 0)
            };

        if (key.Contains("fast") || key.Contains("sandwich"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Hamburger Menü", 145m, 0),
                new DefaultFacilityServiceCatalogItem("Tost", 75m, 0),
                new DefaultFacilityServiceCatalogItem("Sandviç", 85m, 0),
                new DefaultFacilityServiceCatalogItem("Patates Kızartması", 65m, 0)
            };

        if (key.Contains("kafeterya") || key.Contains("kafe") || key.Contains("cafe") || key.Contains("coffee"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Çay", 15m, 0),
                new DefaultFacilityServiceCatalogItem("Türk Kahvesi", 45m, 0),
                new DefaultFacilityServiceCatalogItem("Filtre Kahve", 55m, 0),
                new DefaultFacilityServiceCatalogItem("Tost", 75m, 0),
                new DefaultFacilityServiceCatalogItem("Soğuk İçecek", 35m, 0)
            };

        if (key.Contains("pastane") || key.Contains("cake"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Dilim Pasta", 80m, 0),
                new DefaultFacilityServiceCatalogItem("Sütlü Tatlı", 70m, 0),
                new DefaultFacilityServiceCatalogItem("Kuru Pasta", 90m, 0),
                new DefaultFacilityServiceCatalogItem("Çay / Kahve", 35m, 0)
            };

        if (key.Contains("bar") || key.Contains("meyhane") || key.Contains("glass") || key.Contains("wine"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Alkolsüz Kokteyl", 90m, 0),
                new DefaultFacilityServiceCatalogItem("Meşrubat", 40m, 0),
                new DefaultFacilityServiceCatalogItem("Karışık Çerez", 85m, 0),
                new DefaultFacilityServiceCatalogItem("Meyve Tabağı", 140m, 0)
            };

        if (key.Contains("konaklama") || key.Contains("misafirhane") || key.Contains("bed"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Tek Kişilik Oda", 450m, 1440),
                new DefaultFacilityServiceCatalogItem("Çift Kişilik Oda", 700m, 1440),
                new DefaultFacilityServiceCatalogItem("Aile Odası", 950m, 1440)
            };

        if (key.Contains("dugun") || key.Contains("davet") || key.Contains("party"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Salon Kullanımı", 7500m, 240),
                new DefaultFacilityServiceCatalogItem("Kişi Başı Yemek Menüsü", 450m, 0),
                new DefaultFacilityServiceCatalogItem("Kokteyl İkram Paketi", 280m, 0)
            };

        if (key.Contains("toplanti") || key.Contains("konferans") || key.Contains("presentation"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Saatlik Salon Kullanımı", 500m, 60),
                new DefaultFacilityServiceCatalogItem("Yarım Gün Salon", 1800m, 240),
                new DefaultFacilityServiceCatalogItem("Tam Gün Salon", 3200m, 480)
            };

        if (key.Contains("hamam") || key.Contains("bath"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Hamam Girişi", 180m, 60, 15),
                new DefaultFacilityServiceCatalogItem("Kese", 120m, 30, 10),
                new DefaultFacilityServiceCatalogItem("Köpük Masajı", 180m, 30, 10)
            };

        if (key.Contains("sauna") || key.Contains("termal") || key.Contains("havuz") || key.Contains("pool") || key.Contains("plaj") || key.Contains("umbrella"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Günlük Giriş", 150m, 90, 15),
                new DefaultFacilityServiceCatalogItem("Çocuk Girişi", 90m, 90, 15),
                new DefaultFacilityServiceCatalogItem("Aile Kullanımı", 420m, 90, 15)
            };

        if (key.Contains("spor") || key.Contains("fitness") || key.Contains("tenis") || key.Contains("golf") || key.Contains("saha") || key.Contains("dumbbell") || key.Contains("activity"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Günlük Kullanım", 80m, 60, 10),
                new DefaultFacilityServiceCatalogItem("Saatlik Saha", 300m, 60, 10),
                new DefaultFacilityServiceCatalogItem("Aylık Üyelik", 650m, 60, 10)
            };

        if (key.Contains("market") || key.Contains("shopping"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Su", 10m, 0),
                new DefaultFacilityServiceCatalogItem("Atıştırmalık", 35m, 0),
                new DefaultFacilityServiceCatalogItem("Temel İhtiyaç Ürünleri", 75m, 0)
            };

        if (key.Contains("kuru") || key.Contains("temizleme") || key.Contains("shirt"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Gömlek Ütü", 45m, 0),
                new DefaultFacilityServiceCatalogItem("Takım Elbise Temizleme", 220m, 0),
                new DefaultFacilityServiceCatalogItem("Pantolon Temizleme", 110m, 0)
            };

        if (key.Contains("terzi") || key.Contains("needle"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Paça Tadilatı", 90m, 30),
                new DefaultFacilityServiceCatalogItem("Fermuar Değişimi", 140m, 45),
                new DefaultFacilityServiceCatalogItem("Rütbe / Arma Dikimi", 60m, 20)
            };

        if (key.Contains("arac") || key.Contains("oto") || key.Contains("yikama") || key.Contains("car"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Dış Yıkama", 150m, 30, 10),
                new DefaultFacilityServiceCatalogItem("İç-Dış Yıkama", 250m, 45, 10),
                new DefaultFacilityServiceCatalogItem("Detaylı Temizlik", 650m, 120, 15)
            };

        if (key.Contains("oyun") || key.Contains("bilardo") || key.Contains("gamepad"))
            return new[]
            {
                new DefaultFacilityServiceCatalogItem("Saatlik Oyun Masası", 80m, 60),
                new DefaultFacilityServiceCatalogItem("Bilardo", 120m, 60),
                new DefaultFacilityServiceCatalogItem("Masa Oyunu Kullanımı", 0m, 60)
            };

        return new[]
        {
            new DefaultFacilityServiceCatalogItem("Standart Kullanım", 0m, Math.Max(facility.DefaultSlotDurationMinutes, 30), facility.BufferMinutes)
        };
    }

    private static Facility CreateFacilityFromTemplate(Guid ordueviId, AmenityFacilityTemplate template)
    {
        return new Facility
        {
            Id = Guid.NewGuid(),
            OrdueviId = ordueviId,
            Name = template.Name,
            Category = template.Category,
            AppointmentMode = template.AppointmentMode,
            MaxConcurrency = template.MaxConcurrency,
            BufferMinutes = template.BufferMinutes,
            DefaultSlotDurationMinutes = template.DefaultSlotDurationMinutes,
            OpeningTime = template.OpeningTime,
            ClosingTime = template.ClosingTime,
            IsActive = true,
            Description = BuildFacilityDescription(template.Name),
            Icon = template.Icon,
            Image = GetFacilityImagePath(template.Name)
        };
    }

    private static bool ApplyTemplatePresentation(Facility facility, AmenityFacilityTemplate template)
    {
        var changed = false;
        var description = BuildFacilityDescription(template.Name);
        var imagePath = GetFacilityImagePath(template.Name);

        if (ShouldRefreshTemplateDescription(facility.Description))
            changed |= SetIfChanged(facility.Description, description, value => facility.Description = value);

        if (string.IsNullOrWhiteSpace(facility.Image) && !string.IsNullOrWhiteSpace(imagePath))
        {
            facility.Image = imagePath;
            changed = true;
        }

        return changed;
    }

    private static bool ShouldRefreshTemplateDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return true;

        var normalized = NormalizeText(description);
        return LegacyTemplateDescriptions.Any(item => NormalizeText(item) == normalized);
    }

    private static readonly string[] LegacyTemplateDescriptions =
    {
        "Bu tesiste konaklama hizmeti sunulmaktadır.",
        "Tesisin yeme-içme birimi.",
        "Izgara ve sıcak yemek servisi sunan salon.",
        "Pide ve lahmacun servisi bulunan yeme-içme birimi.",
        "Hızlı servis yiyecek birimi.",
        "Kafeterya ve dinlenme alanı.",
        "Pastane ve tatlı servisi.",
        "Sosyal içecek servisi alanı.",
        "Düğün, davet ve organizasyon salonu.",
        "Toplantı ve organizasyonlar için ayrılmış salon.",
        "Berber hizmetleri.",
        "Kuaför hizmetleri.",
        "Hamam hizmeti.",
        "Sauna kullanım alanı.",
        "Termal banyo hizmeti.",
        "Termal havuz kullanım alanı.",
        "Spor salonu ve fitness alanı.",
        "Açık veya kapalı spor alanı.",
        "Yüzme havuzu kullanım alanı.",
        "Çocuklar için havuz alanı.",
        "Tenis kortu kullanım alanı.",
        "Mini golf alanı.",
        "Bilardo, tavla veya oyun alanı.",
        "Çocuklar için oyun alanı.",
        "Okuma ve dinlenme salonu.",
        "Market ve alışveriş birimi.",
        "Kuru temizleme hizmeti.",
        "Terzi hizmeti.",
        "Araç yıkama hizmeti.",
        "Plaj kullanım alanı.",
        "Su sporları etkinlik alanı.",
        "Erkek ve çocuk saç kesimi, sakal tıraşı.",
        "Pide ve fırın ürünleri servisi.",
        "Meyhane ve canlı müzik."
    };

    private static string BuildFacilityDescription(string name)
    {
        var key = NormalizeText(name);

        if (key.Contains("konaklama"))
            return "Konaklama birimi; görev, izin veya ziyaret planı olan hak sahipleri için düzenli oda kullanımı ve sakin dinlenme alanı sunar. Giriş-çıkış saatleri tesis yoğunluğuna göre yönetilir.";
        if (key.Contains("restoran"))
            return "Restoran; günlük yemek, aile buluşması ve toplu servis ihtiyaçları için hazırlanmış ana yeme-içme alanıdır. Masa düzeni ve servis akışı yoğun saatlere göre planlanır.";
        if (key.Contains("izgara"))
            return "Izgara salonu; sıcak servis, ana yemek ve akşam kullanımı için ayrılmış yeme-içme birimidir. Siparişler mutfak hazırlık süresine göre yönetilir.";
        if (key.Contains("pide"))
            return "Pide-lahmacun salonu; fırın ürünleri, hızlı öğle/akşam yemeği ve aile masaları için kullanılan sıcak servis alanıdır.";
        if (key.Contains("fast"))
            return "Fast-food birimi; kısa sürede servis alınabilecek pratik yiyecek ve içecek seçenekleri için hazırlanmıştır.";
        if (key.Contains("kafeterya"))
            return "Kafeterya; çay, kahve, hafif atıştırmalık ve dinlenme kullanımı için tesisin sosyal buluşma alanıdır.";
        if (key.Contains("pastane"))
            return "Pastane; tatlı, pasta, sıcak-soğuk içecek ve misafir ağırlama ihtiyaçları için çalışan yeme-içme birimidir.";
        if (key == "bar")
            return "Bar alanı; sosyal içecek servisi ve akşam dinlenme kullanımı için ayrılmış kontrollü servis bölümüdür.";
        if (key.Contains("dugun"))
            return "Düğün salonu; davet, nişan, tören ve toplu organizasyonlar için rezervasyonla kullanılan geniş etkinlik alanıdır.";
        if (key.Contains("toplanti"))
            return "Toplantı salonu; brifing, eğitim, seminer ve kapalı grup organizasyonları için teknik düzene sahip çalışma alanıdır.";
        if (key.Contains("berber"))
            return "Berber birimi; saç kesimi, sakal tıraşı ve kişisel bakım randevuları için zaman bazlı çalışan hizmet alanıdır.";
        if (key.Contains("kuafor"))
            return "Kuaför birimi; saç bakım, kesim, şekillendirme ve kişisel bakım işlemleri için randevu düzeniyle hizmet verir.";
        if (key.Contains("hamam"))
            return "Hamam; belirlenen seanslarda kullanılan geleneksel bakım ve dinlenme alanıdır. Kullanım yoğunluğu kapasiteye göre sınırlandırılır.";
        if (key.Contains("sauna"))
            return "Sauna; kısa seanslarla kullanılan dinlenme ve yenilenme alanıdır. Seanslar hazırlık ve temizlik aralıklarına göre planlanır.";
        if (key.Contains("termal banyo"))
            return "Termal banyo; sıcak su ve bakım seansları için ayrılmış kontrollü kullanım alanıdır.";
        if (key.Contains("termal havuz"))
            return "Termal havuz; kapasite kontrollü seanslarla kullanılan sıcak su dinlenme alanıdır.";
        if (key.Contains("spor salonu"))
            return "Spor salonu; fitness ekipmanları, kondisyon çalışması ve düzenli spor kullanımı için ayrılmış alandır.";
        if (key.Contains("spor alani"))
            return "Spor alanı; açık veya kapalı saha kullanımı, takım etkinlikleri ve randevulu spor organizasyonları için uygundur.";
        if (key.Contains("yuzme havuzu"))
            return "Yüzme havuzu; sezon ve seans düzenine göre kullanılan kapasite kontrollü yüzme alanıdır.";
        if (key.Contains("cocuk havuzu"))
            return "Çocuk havuzu; aile kullanımı için ayrılmış, kapasite ve gözetim kurallarına göre işletilen havuz alanıdır.";
        if (key.Contains("tenis"))
            return "Tenis kortu; saatlik saha kullanımı ve bireysel spor randevuları için ayrılmış açık/kapalı oyun alanıdır.";
        if (key.Contains("mini golf"))
            return "Mini golf alanı; sosyal spor ve kısa süreli eğlence kullanımı için hazırlanmış açık etkinlik bölümüdür.";
        if (key.Contains("oyun salonu"))
            return "Oyun salonu; bilardo, masa oyunları ve sosyal vakit geçirme seçenekleri için kullanılan kapalı eğlence alanıdır.";
        if (key.Contains("cocuk oyun"))
            return "Çocuk oyun alanı; ailelerin tesis kullanımı sırasında çocuklar için ayrılmış güvenli sosyal alandır.";
        if (key.Contains("okuma"))
            return "Okuma salonu; sessiz çalışma, gazete-kitap okuma ve kısa dinlenme için ayrılmış sakin alandır.";
        if (key.Contains("market"))
            return "Market; günlük ihtiyaç, atıştırmalık ve temel ürün alışverişi için tesis içi pratik satış birimidir.";
        if (key.Contains("kuru temizleme"))
            return "Kuru temizleme; kıyafet teslim, bakım ve temizlik işlemleri için takipli hizmet veren birimdir.";
        if (key.Contains("terzi"))
            return "Terzi; paça, tadilat, küçük onarım ve ölçü işlemleri için zaman bazlı çalışan bakım birimidir.";
        if (key.Contains("arac yikama"))
            return "Araç yıkama; araç dış/iç temizlik işlemleri için randevulu veya kapasiteye bağlı kullanılan hizmet alanıdır.";
        if (key.Contains("plaj"))
            return "Özel plaj; sezonluk deniz, güneşlenme ve aile kullanımı için ayrılmış kontrollü sosyal alandır.";
        if (key.Contains("su sporlari"))
            return "Su sporları; deniz etkinlikleri, ekipmanlı aktiviteler ve kontrollü seanslar için planlanan etkinlik alanıdır.";

        return "Bu hizmet birimi tesisin kullanım imkanlarına göre düzenlenmiştir. Saat, kapasite ve kullanım koşulları yoğunluğa göre yönetilir.";
    }

    private static string GetFacilityImagePath(string name)
    {
        var key = NormalizeText(name);

        if (key.Contains("berber") || key.Contains("kuafor") || key.Contains("terzi"))
            return "scraped_data/images/istanbul-selimiye-astsubay-orduevi/003-berber-koltugu.jpeg";
        if (key.Contains("pastane"))
            return "scraped_data/images/izmir-konak-subay-orduevi/004-pastane.png";
        if (key == "bar")
            return "scraped_data/images/istanbul-selimiye-astsubay-orduevi/007-bar.jpeg";
        if (key.Contains("dugun"))
            return "scraped_data/images/ankara-gazi-orduevi/009-gazi-orduevi-dugun-salonu-yenimahalle-ankara-2.png";
        if (key.Contains("toplanti"))
            return "scraped_data/images/izmir-konak-subay-orduevi/003-izmir-orduevi-personel-yatakhanesiozel-brifing-ve-toplanti-salonu-2.jpg";
        if (key.Contains("konaklama"))
            return "scraped_data/images/istanbul-selimiye-astsubay-orduevi/020-oda-manzarasi.jpeg";
        if (key.Contains("kafeterya"))
            return "scraped_data/images/antalya-side-jandarma-ozel-egitim-merkezi/006-cafetarya.jpeg";
        if (key.Contains("restoran") || key.Contains("izgara") || key.Contains("pide") || key.Contains("fast"))
            return "scraped_data/images/aydin-kusadasi-guzelcamli-ozel-egitim-merkezi/005-plaj-kenari-lokanta.png";
        if (key.Contains("plaj") || key.Contains("havuz") || key.Contains("su sporlari"))
            return "scraped_data/images/canakkale-gelibolu-hamzakoy-ozel-egitim-merkezi/004-pot43955-30355-gelibolu-hamzakoy-plaji-halkin-hizmetind.jpg";

        return string.Empty;
    }

    private static bool TemplateMatches(string normalizedSource, AmenityFacilityTemplate template)
    {
        return template.Keywords.Any(keyword => ContainsNormalizedPhrase(normalizedSource, keyword));
    }

    private static bool FacilityLooksLikeTemplate(Facility facility, AmenityFacilityTemplate template)
    {
        var normalizedFacility = NormalizeText($"{facility.Name} {facility.Description} {facility.Icon}");
        var normalizedTemplateName = NormalizeText(template.Name);

        return ContainsNormalizedPhrase(normalizedFacility, normalizedTemplateName) ||
               template.Keywords.Any(keyword => ContainsNormalizedPhrase(normalizedFacility, keyword));
    }

    private static bool ContainsNormalizedPhrase(string normalizedSource, string phrase)
    {
        var normalizedPhrase = NormalizeText(phrase);
        return !string.IsNullOrWhiteSpace(normalizedPhrase) &&
               $" {normalizedSource} ".Contains($" {normalizedPhrase} ", StringComparison.Ordinal);
    }

    private static ScrapedOrdueviRecord CreateScrapedRecord(JsonElement item)
    {
        var title = GetString(item, "title");
        var sourceId = GetInt(item, "id");
        var slug = GetString(item, "slug");
        var sourceUrl = GetString(item, "url");
        var featuredImageUrl = GetString(item, "featured_image");
        var featuredImageLocalPath = GetString(item, "featured_image_local_path");

        var infobox = GetObject(item, "infobox");
        var article = GetObject(item, "article");
        var contact = article.HasValue ? GetObject(article.Value, "contact") : null;

        var address = FirstNonEmpty(
            GetString(infobox, "address"),
            GetFirstString(contact, "addresses"));

        var contactNumber = FirstNonEmpty(
            JoinStrings(GetStringArray(contact, "phones")),
            GetString(infobox, "phone"));

        var amenities = JoinStrings(GetStringArray(article, "amenities"));
        var description = BuildDescription(item, infobox, article, amenities, sourceUrl, featuredImageLocalPath);

        return new ScrapedOrdueviRecord(
            sourceId,
            title,
            slug,
            sourceUrl,
            description,
            address,
            contactNumber,
            amenities,
            featuredImageUrl,
            featuredImageLocalPath,
            item.GetRawText());
    }

    private static string BuildDescription(
        JsonElement item,
        JsonElement? infobox,
        JsonElement? article,
        string amenities,
        string sourceUrl,
        string featuredImageLocalPath)
    {
        var parts = new List<string>();

        AddIfNotBlank(parts, FirstNonEmpty(
            GetString(infobox, "description"),
            GetString(item, "excerpt")));

        AddIfNotBlank(parts, Prefix("Konum", GetString(article, "location_text")));
        AddIfNotBlank(parts, Prefix("İletişim", GetString(article, "contact_text")));
        AddIfNotBlank(parts, Prefix("Kimler yararlanabilir", GetString(article, "eligibility_text")));
        AddIfNotBlank(parts, Prefix("Ulaşım", GetString(article, "directions_text")));
        AddIfNotBlank(parts, Prefix("Notlar", GetString(article, "notes_text")));
        AddIfNotBlank(parts, Prefix("Olanaklar", amenities));
        AddIfNotBlank(parts, Prefix("Kaynak", sourceUrl));
        AddIfNotBlank(parts, Prefix("Görsel", featuredImageLocalPath));

        var description = string.Join("\n\n", parts.Distinct());
        return string.IsNullOrWhiteSpace(description)
            ? GetString(article, "text")
            : description;
    }

    private static bool ApplyScrapedRecord(Orduevi orduevi, ScrapedOrdueviRecord record)
    {
        var changed = false;

        if (orduevi.ScrapedSourceId != record.SourceId)
        {
            orduevi.ScrapedSourceId = record.SourceId;
            changed = true;
        }

        changed |= SetIfChanged(orduevi.Name, record.Title.Trim(), value => orduevi.Name = value);
        changed |= SetIfChanged(orduevi.Description, record.Description.Trim(), value => orduevi.Description = value);

        if (!string.IsNullOrWhiteSpace(record.Address))
            changed |= SetIfChanged(orduevi.Address, record.Address.Trim(), value => orduevi.Address = value);

        if (!string.IsNullOrWhiteSpace(record.ContactNumber))
            changed |= SetIfChanged(orduevi.ContactNumber, record.ContactNumber.Trim(), value => orduevi.ContactNumber = value);

        changed |= SetIfChanged(orduevi.Slug, record.Slug.Trim(), value => orduevi.Slug = value);
        changed |= SetIfChanged(orduevi.SourceUrl, record.SourceUrl.Trim(), value => orduevi.SourceUrl = value);
        changed |= SetIfChanged(orduevi.FeaturedImageUrl, record.FeaturedImageUrl.Trim(), value => orduevi.FeaturedImageUrl = value);
        changed |= SetIfChanged(orduevi.FeaturedImageLocalPath, record.FeaturedImageLocalPath.Trim(), value => orduevi.FeaturedImageLocalPath = value);
        changed |= SetIfChanged(orduevi.Amenities, record.Amenities.Trim(), value => orduevi.Amenities = value);
        changed |= SetIfChanged(orduevi.ScrapedMetadataJson, record.RawJson, value => orduevi.ScrapedMetadataJson = value);

        if (string.IsNullOrWhiteSpace(orduevi.AdminUserId))
        {
            orduevi.AdminUserId = "admin-123";
            changed = true;
        }

        if (changed)
            orduevi.UpdatedAt = DateTime.UtcNow;

        return changed;
    }

    private static Orduevi? FindMatchingOrduevi(
        ScrapedOrdueviRecord record,
        List<Orduevi> existing,
        HashSet<Guid> usedOrdueviIds)
    {
        var bySource = existing.FirstOrDefault(o => o.ScrapedSourceId == record.SourceId);
        if (bySource != null)
            return bySource;

        var normalizedTitle = NormalizeText(record.Title);
        var exactName = existing.FirstOrDefault(o =>
            !usedOrdueviIds.Contains(o.Id) &&
            NormalizeText(o.Name) == normalizedTitle);
        if (exactName != null)
            return exactName;

        var stableId = CreateStableGuid(record);
        var byStableId = existing.FirstOrDefault(o =>
            o.Id == stableId &&
            !usedOrdueviIds.Contains(o.Id));
        if (byStableId != null)
            return byStableId;

        var recordKey = NormalizeTitleKey(record.Title);
        var recordIsGazino = IsGazinoLike(record.Title);

        foreach (var candidate in existing)
        {
            if (usedOrdueviIds.Contains(candidate.Id))
                continue;

            var candidateIsGazino = IsGazinoLike(candidate.Name);
            if (recordIsGazino != candidateIsGazino)
                continue;

            var candidateKey = NormalizeTitleKey(candidate.Name);
            if (KeysLookRelated(recordKey, candidateKey))
                return candidate;

            if (KeysShareDistinctiveToken(recordKey, candidateKey) &&
                (HasSharedPhone(record.ContactNumber, candidate.ContactNumber) ||
                 AddressesLookRelated(record.Address, candidate.Address)))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool KeysLookRelated(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        if (left == right)
            return true;

        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (leftTokens.Count < 2 || rightTokens.Count < 2)
            return false;

        var smaller = leftTokens.Count <= rightTokens.Count ? leftTokens : rightTokens;
        var larger = leftTokens.Count <= rightTokens.Count ? rightTokens : leftTokens;

        return smaller.All(larger.Contains);
    }

    private static bool KeysShareDistinctiveToken(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var generic = new HashSet<string> { "istanbul", "ankara", "izmir", "merkez", "gazino" };
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => !generic.Contains(t)).ToHashSet();
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => !generic.Contains(t)).ToHashSet();

        return leftTokens.Intersect(rightTokens).Any();
    }

    private static bool HasSharedPhone(string left, string right)
    {
        var leftPhones = ExtractPhoneKeys(left);
        var rightPhones = ExtractPhoneKeys(right);
        return leftPhones.Any(p => rightPhones.Contains(p));
    }

    private static HashSet<string> ExtractPhoneKeys(string value)
    {
        var keys = new HashSet<string>();
        var current = new StringBuilder();

        foreach (var c in value ?? string.Empty)
        {
            if (char.IsDigit(c))
                current.Append(c);
            else
                AddPhoneKey(keys, current);
        }

        AddPhoneKey(keys, current);
        return keys;
    }

    private static void AddPhoneKey(HashSet<string> keys, StringBuilder current)
    {
        if (current.Length >= 7)
        {
            var digits = current.ToString();
            keys.Add(digits[Math.Max(0, digits.Length - 7)..]);
        }

        current.Clear();
    }

    private static bool AddressesLookRelated(string left, string right)
    {
        var leftTokens = NormalizeText(left).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 3).ToHashSet();
        var rightTokens = NormalizeText(right).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 3).ToHashSet();
        return leftTokens.Intersect(rightTokens).Count() >= 2;
    }

    private static bool IsGazinoLike(string value)
    {
        var text = NormalizeText(value);
        return text.Contains("gazino") ||
               text.Contains("misafirhane") ||
               text.Contains("sosyal tesis") ||
               text.Contains("ozel egitim") ||
               text.Contains("kisla") ||
               text.Contains("lojman");
    }

    private static string NormalizeTitleKey(string value)
    {
        var ignored = new HashSet<string>
        {
            "askeri", "orduevi", "ordu", "evi", "subay", "astsubay",
            "kisla", "mudurlugu", "mudurluk", "fiyatlari", "fiyati", "2026"
        };

        var tokens = NormalizeText(value).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t == "gazinosu" ? "gazino" : t)
            .Where(t => !ignored.Contains(t));

        return string.Join(' ', tokens);
    }

    private static string NormalizeText(string value)
    {
        value = value
            .Replace('İ', 'i')
            .Replace('I', 'i')
            .Replace('ı', 'i')
            .Replace('Ğ', 'g')
            .Replace('ğ', 'g')
            .Replace('Ü', 'u')
            .Replace('ü', 'u')
            .Replace('Ş', 's')
            .Replace('ş', 's')
            .Replace('Ö', 'o')
            .Replace('ö', 'o')
            .Replace('Ç', 'c')
            .Replace('ç', 'c')
            .ToLowerInvariant();

        var builder = new StringBuilder();
        var previousWasSpace = true;

        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static Guid CreateStableGuid(ScrapedOrdueviRecord record)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"scraped-orduevi:{record.SourceId}:{record.Slug}:{record.Title}"));
        return new Guid(bytes.Take(16).ToArray());
    }

    private static string? FindFileInParentTree(string relativePath)
    {
        var startPoints = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var start in startPoints)
        {
            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static JsonElement? GetObject(JsonElement? element, string propertyName)
    {
        if (!element.HasValue ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return property;
    }

    private static string GetString(JsonElement? element, string propertyName)
    {
        if (!element.HasValue ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null ||
            property.ValueKind == JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : property.ToString();
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static List<string> GetStringArray(JsonElement? element, string propertyName)
    {
        if (!element.HasValue ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(i => i.ValueKind == JsonValueKind.String)
            .Select(i => i.GetString() ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static string GetFirstString(JsonElement? element, string propertyName)
    {
        return GetStringArray(element, propertyName).FirstOrDefault() ?? string.Empty;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }

    private static string JoinStrings(IEnumerable<string> values)
    {
        return string.Join(" | ", values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct());
    }

    private static bool SetIfChanged(string current, string next, Action<string> assign)
    {
        current ??= string.Empty;
        next ??= string.Empty;

        if (current == next)
            return false;

        assign(next);
        return true;
    }

    private static string Prefix(string label, string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $"{label}: {value.Trim()}";
    }

    private static void AddIfNotBlank(List<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values.Add(value.Trim());
    }

}
