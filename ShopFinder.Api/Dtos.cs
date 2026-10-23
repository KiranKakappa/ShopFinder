using System.Globalization;
using ShopFinder.Api.Models;
using ShopFinder.Api.Services;

namespace ShopFinder.Api;

// ---------- Responses ----------

public record ProductDto(
    int Id, int ShopId, string Name, string Description,
    decimal Price, int DiscountPercent, decimal FinalPrice, bool DealActive,
    DateTime? DealEndsUtc, bool InStock);

public record ShopSummaryDto(
    int Id, string Name, string Category, string Address,
    double Latitude, double Longitude,
    bool IsOpen, string StatusSource, string StatusMessage, string Hours,
    double? DistanceKm, int ActiveDeals, int ClosedReportsToday);

public record ShopDetailDto(
    int Id, string Name, string Category, string Description, string Address, string Phone,
    double Latitude, double Longitude,
    bool IsOpen, string StatusSource, string StatusMessage, string Hours,
    string OpenTime, string CloseTime, int[] OpenDays,
    int ClosedReportsToday, DateTime LastStatusChangeUtc,
    List<ProductDto> Products);

public record DealDto(
    ProductDto Product, int ShopId, string ShopName, string ShopCategory,
    bool ShopIsOpen, string ShopStatusMessage, double? DistanceKm);

public record AuthResponse(string Token, ShopDetailDto Shop);

// ---------- Requests ----------

public record RegisterRequest(
    string? Name, string? Category, string? Description, string? Address, string? Phone,
    double? Latitude, double? Longitude, string? OpenTime, string? CloseTime, int[]? OpenDays,
    string? Password);

public record LoginRequest(string? Phone, string? Password);

public record UpdateShopRequest(
    string? Name, string? Category, string? Description, string? Address,
    double? Latitude, double? Longitude, string? OpenTime, string? CloseTime, int[]? OpenDays);

/// <summary>Mode: "open", "closed" (override for today) or "auto" (follow regular hours).</summary>
public record StatusRequest(string? Mode);

public record ProductRequest(
    string? Name, string? Description, decimal? Price, int? DiscountPercent,
    DateTime? DealEndsUtc, bool? InStock);

// ---------- Errors ----------

/// <summary>Thrown from endpoints; turned into { "error": "..." } with the given status code.</summary>
public class ApiError : Exception
{
    public ApiError(int status, string message) : base(message) => Status = status;
    public int Status { get; }
}

// ---------- Mapping & validation ----------

public static class ShopMapper
{
    public static readonly string[] Categories =
    {
        "Grocery", "Bakery", "Pharmacy", "Electronics", "Clothing",
        "Restaurant", "Hardware", "Books", "Beauty", "Other"
    };

    public static ProductDto ToDto(this Product p, DateTime nowUtc) => new(
        p.Id, p.ShopId, p.Name, p.Description,
        p.Price, p.DiscountPercent, p.FinalPrice(nowUtc), p.IsDealActive(nowUtc),
        p.DealEndsUtc is null ? null : DateTime.SpecifyKind(p.DealEndsUtc.Value, DateTimeKind.Utc),
        p.InStock);

    public static ShopSummaryDto ToSummary(this Shop s, Clock clock, double? lat, double? lng)
    {
        var st = ShopStatus.Compute(s, clock.LocalNow);
        return new ShopSummaryDto(
            s.Id, s.Name, s.Category, s.Address, s.Latitude, s.Longitude,
            st.IsOpen, st.Source, st.Message, ShopStatus.DescribeHours(s),
            Geo.DistanceKm(lat, lng, s.Latitude, s.Longitude),
            s.Products.Count(p => p.IsDealActive(clock.UtcNow)),
            ReportsToday(s, clock));
    }

    public static ShopDetailDto ToDetail(this Shop s, Clock clock)
    {
        var st = ShopStatus.Compute(s, clock.LocalNow);
        var now = clock.UtcNow;
        var products = s.Products
            .OrderByDescending(p => p.IsDealActive(now))
            .ThenByDescending(p => p.InStock)
            .ThenBy(p => p.Name)
            .Select(p => p.ToDto(now))
            .ToList();
        return new ShopDetailDto(
            s.Id, s.Name, s.Category, s.Description, s.Address, s.Phone,
            s.Latitude, s.Longitude,
            st.IsOpen, st.Source, st.Message, ShopStatus.DescribeHours(s),
            s.OpenTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            s.CloseTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            ShopStatus.ParseDays(s.OpenDays).OrderBy(d => d).ToArray(),
            ReportsToday(s, clock),
            DateTime.SpecifyKind(s.LastStatusChangeUtc, DateTimeKind.Utc),
            products);
    }

    public static int ReportsToday(Shop s, Clock clock) =>
        s.ClosedReportsDate == clock.LocalToday ? s.ClosedReportsCount : 0;

    // --- validation helpers ---

    public static string Required(string? value, string field, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) throw new ApiError(400, $"{field} is required.");
        if (v.Length > max) throw new ApiError(400, $"{field} must be at most {max} characters.");
        return v;
    }

    public static string Optional(string? value, string field, int max)
    {
        var v = value?.Trim() ?? "";
        if (v.Length > max) throw new ApiError(400, $"{field} must be at most {max} characters.");
        return v;
    }

    public static string CategoryOrThrow(string? value)
    {
        var v = Required(value, "Category", 60);
        var match = Categories.FirstOrDefault(c => c.Equals(v, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ApiError(400, $"Category must be one of: {string.Join(", ", Categories)}.");
    }

    public static TimeOnly TimeOrThrow(string? value, string field)
    {
        if (TimeOnly.TryParseExact(value?.Trim(), new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var t))
            return t;
        throw new ApiError(400, $"{field} must be a time like 09:00 or 21:30.");
    }

    public static (double lat, double lng) CoordsOrThrow(double? lat, double? lng)
    {
        if (lat is null || lng is null) throw new ApiError(400, "Shop location (latitude and longitude) is required.");
        if (lat is < -90 or > 90 || lng is < -180 or > 180) throw new ApiError(400, "Shop location is out of range.");
        return (lat.Value, lng.Value);
    }

    public static string DaysOrThrow(int[]? days)
    {
        if (days is null || days.Length == 0) throw new ApiError(400, "Choose at least one open day.");
        if (days.Any(d => d is < 0 or > 6)) throw new ApiError(400, "Open days must be numbers 0 (Sun) to 6 (Sat).");
        return ShopStatus.FormatDays(days);
    }

    public static string NormalizePhone(string? phone)
    {
        var raw = Required(phone, "Phone", 30);
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length < 7) throw new ApiError(400, "Enter a valid phone number.");
        return raw.TrimStart().StartsWith('+') ? "+" + digits : digits;
    }
}

public static class Geo
{
    /// <summary>Great-circle distance in km, or null when the customer's location is unknown.</summary>
    public static double? DistanceKm(double? lat1, double? lng1, double lat2, double lng2)
    {
        if (lat1 is null || lng1 is null) return null;
        const double R = 6371.0;
        double ToRad(double x) => x * Math.PI / 180.0;
        var dLat = ToRad(lat2 - lat1.Value);
        var dLng = ToRad(lng2 - lng1.Value);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1.Value)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return Math.Round(R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)), 2);
    }
}
