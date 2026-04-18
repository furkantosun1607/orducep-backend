namespace OrduCep.Domain.Enums;

/// <summary>
/// Randevu durumunu belirleyen type-safe enum. Eskiden string kullanılıyordu.
/// </summary>
public enum ReservationStatus
{
    Locked,
    Pending,
    Approved,
    Cancelled,
    Completed,
    WalkIn
}
