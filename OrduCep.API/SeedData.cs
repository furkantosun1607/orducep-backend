using Microsoft.EntityFrameworkCore;
using OrduCep.Infrastructure.Persistence;
using OrduCep.Domain.Entities;
using OrduCep.Domain.Enums;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OrduCep.API;

public static class SeedData
{
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

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
