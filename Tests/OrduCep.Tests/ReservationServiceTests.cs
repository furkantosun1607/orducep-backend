using Microsoft.EntityFrameworkCore;
using OrduCep.Application.Services;
using OrduCep.Domain.Entities;
using OrduCep.Infrastructure.Persistence;
using Xunit;

namespace OrduCep.Tests;

public class ReservationServiceTests
{
    private OrduCepDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<OrduCepDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new OrduCepDbContext(options);
    }

    [Fact]
    public async Task GetAvailableTimeSlots_Returns_Correct_Slots()
    {
        // 1. Arrange (Hazırlık)
        var context = GetInMemoryDbContext();
        var facilityId = Guid.NewGuid();
        
        // Örnek bir tesis oluşturuyoruz: 08:00 ile 10:00 arası açık (Sadece 2 saat = 4 slot)
        context.Facilities.Add(new Facility
        {
            Id = facilityId,
            OpeningTime = new TimeSpan(8, 0, 0),
            ClosingTime = new TimeSpan(10, 0, 0),
            SlotDurationInMinutes = 30, // Yarım saatte bir
            IsActive = true
        });

        // 08:00 - 08:30 arasına dolu bir randevu ekliyoruz
        var testDate = DateTime.UtcNow.Date;
        context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            StartTime = testDate.AddHours(8),
            EndTime = testDate.AddHours(8).AddMinutes(30),
            Status = "Approved",
            UserId = "ahmet"
        });

        await context.SaveChangesAsync();

        var service = new ReservationService(context);

        // 2. Act (Çalıştırma)
        var slots = await service.GetAvailableTimeSlotsAsync(facilityId, testDate);

        // 3. Assert (Doğrulama)
        Assert.Equal(4, slots.Count); // Toplam 4 slot dönmeli (08:00, 08:30, 09:00, 09:30)
        
        // İlk slotun dolu olması gerekiyor (ahmet almıştı)
        Assert.False(slots[0].IsAvailable); 
        Assert.Equal("ahmet", slots[0].OccupiedByUserId);

        // İkinci slot boş olmalı
        Assert.True(slots[1].IsAvailable);
    }

    [Fact]
    public async Task LockTimeSlot_Prevents_DoubleBooking()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var service = new ReservationService(context);
        var facilityId = Guid.NewGuid();
        var targetTime = DateTime.UtcNow.Date.AddHours(14); // Saat 14:00

        // Act & Assert 1: Mehmet 14:00'ı kilitler ve başarılı olur
        bool firstLock = await service.LockTimeSlotAsync(facilityId, targetTime, targetTime.AddMinutes(30), "mehmet");
        Assert.True(firstLock);

        // Act & Assert 2: Salihesinde başka biri (Ali) aynı saniyede 14:00'ı seçmeye çalışır.
        // HATA ve FALSE dönmelidir çünkü sistem onu kilitledi
        bool secondLock = await service.LockTimeSlotAsync(facilityId, targetTime, targetTime.AddMinutes(30), "ali");
        Assert.False(secondLock);
    }
}
