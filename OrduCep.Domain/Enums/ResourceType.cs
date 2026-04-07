namespace OrduCep.Domain.Enums;

/// <summary>
/// Tesis kaynak tiplerini belirler.
/// </summary>
public enum ResourceType
{
    /// <summary>Berber koltuğu</summary>
    Chair,

    /// <summary>Masa (restoran, meyhane vb.)</summary>
    Table,

    /// <summary>Oda</summary>
    Room,

    /// <summary>Personel (1:1 hizmet)</summary>
    Staff,

    /// <summary>Genel kapasite birimi</summary>
    Generic
}
