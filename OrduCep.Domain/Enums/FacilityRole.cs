namespace OrduCep.Domain.Enums;

public enum FacilityRole
{
    Manager,    // Birim Müdürü: Kendi birimindeki her şeye tam yetkili
    Staff,      // Çalışan: Sadece onaylanan randevuları (ad, soyad, saat) veya kendi işini görebilir
    Reception   // Ön Büro: Randevu oluşturabilir, değiştirebilir, ödeme alabilir
}
