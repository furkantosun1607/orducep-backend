namespace OrduCep.Domain.Enums;

/// <summary>
/// İşletmenin randevu kabul modunu belirler.
/// </summary>
public enum AppointmentMode
{
    /// <summary>Sadece randevulu müşteri kabul edilir.</summary>
    AppointmentOnly,

    /// <summary>Hem randevu hem walk-in kabul edilir.</summary>
    Mixed,

    /// <summary>Randevu alınmaz, sadece anlık doluluk takibi yapılır.</summary>
    WalkInOnly
}
