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
                new Resource { Id = Guid.NewGuid(), FacilityId = berberId, Name = "1 Nolu Koltuk", Type = ResourceType.Chair, Capacity = 1, IsActive = true },
                new Resource { Id = Guid.NewGuid(), FacilityId = berberId, Name = "2 Nolu Koltuk", Type = ResourceType.Chair, Capacity = 1, IsActive = true },
                new Resource { Id = Guid.NewGuid(), FacilityId = berberId, Name = "3 Nolu Koltuk", Type = ResourceType.Chair, Capacity = 1, IsActive = true }
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

        // ── 3. Varsayılan Kullanıcı ──
        if (!await context.MilitaryIdentityUsers.AnyAsync())
        {
            var testUser = new MilitaryIdentityUser
            {
                Id = Guid.NewGuid(),
                IdentityNumber = "11223344556",
                PasswordHash = HashPassword("password123"),
                FirstName = "Test",
                LastName = "Kullanıcı",
                Relation = "Kendisi",
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

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
