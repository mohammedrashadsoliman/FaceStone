namespace FaceStone.Web.Models;

/// <summary>
/// Predefined test drivers for the Transportation department.
/// Centralized enum and configuration enabling reliable end-to-end testing
/// across both local and published MonsterASP.NET environments without manual database seeding.
/// </summary>
public enum TransportationTestDriver
{
    AhmedMohamed = 1,
    MohamedAhmed = 2,
    MahmoudHassan = 3,
    MostafaAli = 4,
    OmarKhaled = 5,
    AmrIbrahim = 6,
    HassanMahmoud = 7,
    YoussefMohamed = 8,
    AliMostafa = 9,
    KarimAhmed = 10
}

public class PredefinedDriverInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string DisplayText => $"{Name} ({NameArabic})";
}

public static class PredefinedDrivers
{
    public static readonly IReadOnlyList<PredefinedDriverInfo> List = new List<PredefinedDriverInfo>
    {
        new() { Id = 1,  Name = "Ahmed Mohamed",  NameArabic = "أحمد محمد" },
        new() { Id = 2,  Name = "Mohamed Ahmed",  NameArabic = "محمد أحمد" },
        new() { Id = 3,  Name = "Mahmoud Hassan", NameArabic = "محمود حسن" },
        new() { Id = 4,  Name = "Mostafa Ali",    NameArabic = "مصطفى علي" },
        new() { Id = 5,  Name = "Omar Khaled",    NameArabic = "عمر خالد" },
        new() { Id = 6,  Name = "Amr Ibrahim",    NameArabic = "عمرو إبراهيم" },
        new() { Id = 7,  Name = "Hassan Mahmoud", NameArabic = "حسن محمود" },
        new() { Id = 8,  Name = "Youssef Mohamed",NameArabic = "يوسف محمد" },
        new() { Id = 9,  Name = "Ali Mostafa",    NameArabic = "علي مصطفى" },
        new() { Id = 10, Name = "Karim Ahmed",    NameArabic = "كريم أحمد" }
    };

    public static readonly IReadOnlyList<string> Names = List.Select(d => d.Name).ToList();

    public static bool IsValidDriver(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var trimmed = name.Trim();
        return List.Any(d =>
            string.Equals(d.Name, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.NameArabic, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.DisplayText, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeDriverName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var trimmed = name.Trim();
        var match = List.FirstOrDefault(d =>
            string.Equals(d.Name, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.NameArabic, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.DisplayText, trimmed, StringComparison.OrdinalIgnoreCase));

        return match != null ? match.Name : trimmed;
    }
}
