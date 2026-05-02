using Microsoft.EntityFrameworkCore;
using OrduCep.Application.Services;
using OrduCep.Domain.Entities;
using OrduCep.Domain.Enums;
using OrduCep.Infrastructure.Persistence;
using Xunit;

namespace OrduCep.Tests;

public class ReservationServiceTests
{
    private static OrduCepDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<OrduCepDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OrduCepDbContext(options);
    }

    private static Facility CreateFacility(Guid facilityId)
    {
        return new Facility
        {
            Id = facilityId,
            OpeningTime = new TimeSpan(8, 0, 0),
            ClosingTime = new TimeSpan(10, 0, 0),
            DefaultSlotDurationMinutes = 30,
            MaxConcurrency = 1,
            AppointmentMode = AppointmentMode.AppointmentOnly,
            IsActive = true
        };
    }

    [Fact]
    public async Task GetAvailableTimeSlots_Returns_Correct_Slots()
    {
        var context = GetInMemoryDbContext();
        var facilityId = Guid.NewGuid();
        var testDate = DateTime.Today.AddDays(1);

        context.Facilities.Add(CreateFacility(facilityId));
        context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            StartTime = testDate.AddHours(8),
            EndTime = testDate.AddHours(8).AddMinutes(30),
            Status = ReservationStatus.Approved,
            UserId = "ahmet"
        });

        await context.SaveChangesAsync();

        var service = new ReservationService(context);
        var slots = await service.GetAvailableTimeSlotsAsync(facilityId, testDate);

        Assert.Equal(4, slots.Count);
        Assert.False(slots[0].IsAvailable);
        Assert.True(slots[1].IsAvailable);
    }

    [Fact]
    public async Task LockTimeSlot_Prevents_DoubleBooking()
    {
        var context = GetInMemoryDbContext();
        var service = new ReservationService(context);
        var facilityId = Guid.NewGuid();
        var targetTime = DateTime.Today.AddDays(1).AddHours(8);

        context.Facilities.Add(CreateFacility(facilityId));
        await context.SaveChangesAsync();

        var firstLock = await service.LockTimeSlotAsync(facilityId, targetTime, null, null, "mehmet");
        var secondLock = await service.LockTimeSlotAsync(facilityId, targetTime, null, null, "ali");

        Assert.True(firstLock);
        Assert.False(secondLock);
    }

    [Fact]
    public async Task LockTimeSlot_Allows_SameTime_For_Different_Resources()
    {
        var context = GetInMemoryDbContext();
        var service = new ReservationService(context);
        var facilityId = Guid.NewGuid();
        var targetTime = DateTime.Today.AddDays(1).AddHours(8);
        var barberOneId = Guid.NewGuid();
        var barberTwoId = Guid.NewGuid();
        var barberThreeId = Guid.NewGuid();

        context.Facilities.Add(CreateFacility(facilityId));
        context.Resources.AddRange(
            new Resource { Id = barberOneId, FacilityId = facilityId, Name = "Helin", Type = ResourceType.Staff, IsActive = true },
            new Resource { Id = barberTwoId, FacilityId = facilityId, Name = "Kerem", Type = ResourceType.Staff, IsActive = true },
            new Resource { Id = barberThreeId, FacilityId = facilityId, Name = "Furkan", Type = ResourceType.Staff, IsActive = true }
        );
        await context.SaveChangesAsync();

        var firstLock = await service.LockTimeSlotAsync(facilityId, targetTime, null, barberOneId, "user-1");
        var secondLock = await service.LockTimeSlotAsync(facilityId, targetTime, null, barberTwoId, "user-2");
        var thirdLock = await service.LockTimeSlotAsync(facilityId, targetTime, null, barberThreeId, "user-3");
        var duplicateBarberLock = await service.LockTimeSlotAsync(facilityId, targetTime, null, barberOneId, "user-4");

        Assert.True(firstLock);
        Assert.True(secondLock);
        Assert.True(thirdLock);
        Assert.False(duplicateBarberLock);
        Assert.Equal(3, await context.Reservations.CountAsync());
    }

    [Fact]
    public async Task LockTimeSlot_Rejects_PastStartTime()
    {
        var context = GetInMemoryDbContext();
        var service = new ReservationService(context);
        var facilityId = Guid.NewGuid();
        var pastTime = DateTime.Now.AddMinutes(-1);

        context.Facilities.Add(CreateFacility(facilityId));
        await context.SaveChangesAsync();

        var locked = await service.LockTimeSlotAsync(facilityId, pastTime, null, null, "mehmet");

        Assert.False(locked);
        Assert.Empty(context.Reservations);
    }

    [Fact]
    public async Task ConfirmReservation_Rejects_PastLockedSlot()
    {
        var context = GetInMemoryDbContext();
        var service = new ReservationService(context);
        var facilityId = Guid.NewGuid();
        var pastTime = DateTime.Now.AddMinutes(-1);

        context.Facilities.Add(CreateFacility(facilityId));
        context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            StartTime = pastTime,
            EndTime = pastTime.AddMinutes(30),
            Status = ReservationStatus.Locked,
            LockedUntil = DateTime.UtcNow.AddMinutes(5),
            UserId = "mehmet"
        });
        await context.SaveChangesAsync();

        var confirmed = await service.ConfirmReservationAsync(facilityId, pastTime, "mehmet");
        var reservation = await context.Reservations.SingleAsync();

        Assert.False(confirmed);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Null(reservation.LockedUntil);
    }
}
