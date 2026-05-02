using OrduCep.Domain.Enums;

namespace OrduCep.API;

public static class PersonnelAccessRules
{
    public const string SivilMemur = "Sivil Memur";
    public const string Mehmetcik = "Mehmetçik";
    public const string UzmanCavus = "Uzman Çavuş";
    public const string Astsubay = "Astsubay";
    public const string Subay = "Subay";

    public static readonly string[] Statuses =
    [
        SivilMemur,
        Mehmetcik,
        UzmanCavus,
        Astsubay,
        Subay
    ];

    public static string NormalizeStatusLabel(string? value)
    {
        var key = ToKey(value);

        return key switch
        {
            "sivilmemur" => SivilMemur,
            "mehmetcik" => Mehmetcik,
            "uzmancavus" => UzmanCavus,
            "astsubay" => Astsubay,
            "subay" => Subay,
            _ => value?.Trim() ?? string.Empty
        };
    }

    public static bool IsKnownStatus(string? value)
    {
        var normalized = NormalizeStatusLabel(value);
        return Statuses.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public static bool CanUseFacilities(string? ownerRank)
    {
        var status = NormalizeStatusLabel(ownerRank);
        return !string.Equals(status, SivilMemur, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(status, Mehmetcik, StringComparison.OrdinalIgnoreCase);
    }

    public static bool RequiresExistingAccountForAssignment(string? status)
    {
        var normalized = NormalizeStatusLabel(status);
        return string.Equals(normalized, UzmanCavus, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, Astsubay, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, Subay, StringComparison.OrdinalIgnoreCase);
    }

    public static string DisplayStaffRole(string? ownerRank, FacilityRole fallbackRole)
    {
        var status = NormalizeStatusLabel(ownerRank);
        return IsKnownStatus(status) ? status : fallbackRole.ToString();
    }

    private static string ToKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .ToLowerInvariant()
            .Replace("ç", "c")
            .Replace("ğ", "g")
            .Replace("ı", "i")
            .Replace("ö", "o")
            .Replace("ş", "s")
            .Replace("ü", "u")
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);
    }
}
