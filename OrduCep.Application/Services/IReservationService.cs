using OrduCep.Application.DTOs;
using OrduCep.Domain.Entities;

namespace OrduCep.Application.Services;

public interface IReservationService
{
    Task<List<TimeSlotDto>> GetAvailableTimeSlotsAsync(Guid facilityId, DateTime date);
    Task<bool> LockTimeSlotAsync(Guid facilityId, DateTime startTime, DateTime endTime, string userId);
    Task<bool> ConfirmReservationAsync(Guid facilityId, DateTime startTime, string userId);
}
