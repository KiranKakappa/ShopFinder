namespace ShopFinder.Api.Models;

/// <summary>
/// A shop. Each shop is also the owner's account (logs in with Phone + password).
/// </summary>
public class Shop
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Regular hours. If CloseTime <= OpenTime the shop is open overnight (e.g. 18:00 -> 02:00).
    public TimeOnly OpenTime { get; set; } = new(9, 0);
    public TimeOnly CloseTime { get; set; } = new(21, 0);

    /// <summary>Comma-separated DayOfWeek numbers (0 = Sunday ... 6 = Saturday).</summary>
    public string OpenDays { get; set; } = "1,2,3,4,5,6";

    /// <summary>"Open" or "Closed" when the owner manually overrides today's schedule; null = follow hours.</summary>
    public string? StatusOverride { get; set; }

    /// <summary>The local date the override applies to. Overrides expire automatically at midnight.</summary>
    public DateOnly? OverrideDate { get; set; }

    // Customer "this shop is actually closed" reports, counted per local day.
    public DateOnly? ClosedReportsDate { get; set; }
    public int ClosedReportsCount { get; set; }

    public DateTime LastStatusChangeUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public string PasswordHash { get; set; } = "";

    public List<Product> Products { get; set; } = new();
}
