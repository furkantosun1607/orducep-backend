namespace OrduCep.Domain.Enums;

/// <summary>
/// İşletmenin çalışma karakterini belirler.
/// </summary>
public enum FacilityCategory
{
    /// <summary>Berber, Kuaför — Zaman odaklı hizmetler</summary>
    TimeBased,

    /// <summary>Pide Salonu, Yemekhane — Kapasite (kişi sayısı) odaklı</summary>
    CapacityBased,

    /// <summary>Meyhane, Gazino — Mekan/masa bazlı rezervasyon</summary>
    SpaceBased
}
