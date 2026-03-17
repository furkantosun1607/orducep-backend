using Microsoft.EntityFrameworkCore;
using OrduCep.Infrastructure.Persistence;
using OrduCep.Domain.Entities;
using System.Globalization;

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

        // Eğer hiç orduevi yoksa CSV'den yükle
        if (!await context.Orduevleri.AnyAsync())
        {
            // Çalışma dizininden CSV'yi oku (API'yi OrduCep.API klasöründen çalıştırıyoruz)
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "orduevleri.csv");
            if (File.Exists(csvPath))
            {
                var lines = await File.ReadAllLinesAsync(csvPath);

                // İlk satır header, o yüzden atlıyoruz
                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Basit CSV parse (virgülle ayrılmış, metinler tırnak içinde olabilir)
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
            }
        }

        // 2. Tesis şablonları yoksa Berber, Pide Salonu ve Kantin şablonlarını ekle
        if (!await context.FacilityTemplates.AnyAsync())
        {
            context.FacilityTemplates.AddRange(
                new FacilityTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = "Berber",
                    Description = "Saç ve sakal hizmetleri.",
                    Icon = "scissors",
                    HasBookingSystem = true
                },
                new FacilityTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = "Pide Salonu",
                    Description = "Pide ve fırın ürünleri servisi.",
                    Icon = "pizza-slice",
                    HasBookingSystem = false
                },
                new FacilityTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = "Kantin",
                    Description = "Günlük ihtiyaçlar için kantin.",
                    Icon = "store",
                    HasBookingSystem = false
                }
            );
            await context.SaveChangesAsync();
        }

        // 3. Eğer hiç tesis yoksa (şablonlar olsa bile) her orduevi için Berber + Pide Salonu tesisleri ekle
        if (!await context.Facilities.AnyAsync())
        {
            var orduevleri = await context.Orduevleri.ToListAsync();

            var berberTemplate = await context.FacilityTemplates.FirstOrDefaultAsync(t => t.Name == "Berber");
            var pideTemplate = await context.FacilityTemplates.FirstOrDefaultAsync(t => t.Name == "Pide Salonu");

            if (berberTemplate != null && pideTemplate != null)
            {
                foreach (var o in orduevleri)
                {
                    var berberFacility = new Facility
                    {
                        Id = Guid.NewGuid(),
                        OrdueviId = o.Id,
                        FacilityTemplateId = berberTemplate.Id,
                        Name = "Erkek Berberi",
                        IsAppointmentBased = true,
                        OpeningTime = new TimeSpan(8, 30, 0),
                        ClosingTime = new TimeSpan(17, 0, 0),
                        SlotDurationInMinutes = 15,
                        IsActive = true
                    };

                    var pideFacility = new Facility
                    {
                        Id = Guid.NewGuid(),
                        OrdueviId = o.Id,
                        FacilityTemplateId = pideTemplate.Id,
                        Name = "Açık Teras Pide Salonu",
                        IsAppointmentBased = false,
                        OpeningTime = new TimeSpan(11, 0, 0),
                        ClosingTime = new TimeSpan(21, 0, 0),
                        SlotDurationInMinutes = 0,
                        IsActive = true
                    };

                    context.Facilities.AddRange(berberFacility, pideFacility);

                    // Basit örnek hizmetler
                    context.FacilityServices.AddRange(
                        new FacilityService
                        {
                            Id = Guid.NewGuid(),
                            FacilityId = berberFacility.Id,
                            ServiceName = "Saç Kesimi",
                            Price = 45.00m,
                            EstimatedDurationInMinutes = 30,
                            IsActive = true
                        },
                        new FacilityService
                        {
                            Id = Guid.NewGuid(),
                            FacilityId = berberFacility.Id,
                            ServiceName = "Saç & Sakal",
                            Price = 60.00m,
                            EstimatedDurationInMinutes = 30,
                            IsActive = true
                        },
                        new FacilityService
                        {
                            Id = Guid.NewGuid(),
                            FacilityId = pideFacility.Id,
                            ServiceName = "Kıymalı Pide",
                            Price = 85.00m,
                            EstimatedDurationInMinutes = 0,
                            IsActive = true
                        },
                        new FacilityService
                        {
                            Id = Guid.NewGuid(),
                            FacilityId = pideFacility.Id,
                            ServiceName = "Kuşbaşılı Pide",
                            Price = 110.00m,
                            EstimatedDurationInMinutes = 0,
                            IsActive = true
                        }
                    );
                }
            }
        }

        await context.SaveChangesAsync();
    }
}
