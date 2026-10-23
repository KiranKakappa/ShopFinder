using System.Globalization;
using ShopFinder.Api.Models;

namespace ShopFinder.Api.Services;

public record StatusInfo(bool IsOpen, string Source, string Message);

/// <summary>
/// Decides whether a shop is open right now.
/// 1. A manual override by the owner wins, but only for the day it was set (it expires at midnight,
///    so a shop that forgot to "close" in the app goes back to its normal hours the next day).
/// 2. Otherwise the regular hours decide, including overnight hours such as 18:00-02:00.
/// </summary>
public static class ShopStatus
{
    public const string Open = "Open";
    public const string Closed = "Closed";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly string[] DayShort = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    public static HashSet<int> ParseDays(string? csv) =>
        (csv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var d) ? d : -1)
            .Where(d => d is >= 0 and <= 6)
            .ToHashSet();

    public static string FormatDays(IEnumerable<int> days) =>
        string.Join(",", days.Where(d => d is >= 0 and <= 6).Distinct().OrderBy(d => d));

    public static string Time(TimeOnly t) => t.ToString("h:mm tt", Inv);

    public static StatusInfo Compute(Shop shop, DateTime nowLocal)
    {
        var today = DateOnly.FromDateTime(nowLocal);

        if (shop.StatusOverride is not null && shop.OverrideDate == today)
        {
            var open = shop.StatusOverride == Open;
            return new StatusInfo(open, "manual", open ? "Opened by owner today" : "Closed by owner today");
        }

        var days = ParseDays(shop.OpenDays);
        var isOpen = IsScheduledOpen(shop, nowLocal, days);
        return new StatusInfo(isOpen, "hours", isOpen ? ClosingMessage(shop) : NextOpeningMessage(shop, nowLocal, days));
    }

    public static bool IsScheduledOpen(Shop shop, DateTime nowLocal, HashSet<int> days)
    {
        var t = TimeOnly.FromDateTime(nowLocal);
        var dow = (int)nowLocal.DayOfWeek;
        var prevDow = (dow + 6) % 7;
        var o = shop.OpenTime;
        var c = shop.CloseTime;

        if (o == c) return days.Contains(dow);                       // open 24 hours on open days
        if (o < c) return days.Contains(dow) && t >= o && t < c;     // normal same-day hours
        return (days.Contains(dow) && t >= o)                        // overnight: evening part
            || (days.Contains(prevDow) && t < c);                    // overnight: after-midnight part
    }

    private static string ClosingMessage(Shop shop) =>
        shop.OpenTime == shop.CloseTime ? "Open 24 hours" : $"Closes at {Time(shop.CloseTime)}";

    private static string NextOpeningMessage(Shop shop, DateTime nowLocal, HashSet<int> days)
    {
        if (days.Count == 0) return "Closed";
        var today = DateOnly.FromDateTime(nowLocal);
        for (var i = 0; i <= 7; i++)
        {
            var date = today.AddDays(i);
            if (!days.Contains((int)date.DayOfWeek)) continue;
            var openAt = date.ToDateTime(shop.OpenTime);
            if (openAt <= nowLocal) continue;
            var when = i switch
            {
                0 => "today",
                1 => "tomorrow",
                _ => DayShort[(int)date.DayOfWeek]
            };
            return $"Opens {when} at {Time(shop.OpenTime)}";
        }
        return "Closed";
    }

    /// <summary>Human-readable hours, e.g. "Mon–Sat · 9:00 AM–9:00 PM".</summary>
    public static string DescribeHours(Shop shop)
    {
        var days = ParseDays(shop.OpenDays).OrderBy(d => d).ToList();
        string dayText;
        if (days.Count == 7) dayText = "Every day";
        else if (days.Count == 0) dayText = "No regular days";
        else if (IsConsecutive(days)) dayText = days.Count == 1 ? DayShort[days[0]] : $"{DayShort[days[0]]}–{DayShort[days[^1]]}";
        else dayText = string.Join(", ", days.Select(d => DayShort[d]));

        var timeText = shop.OpenTime == shop.CloseTime
            ? "24 hours"
            : $"{Time(shop.OpenTime)}–{Time(shop.CloseTime)}";
        return $"{dayText} · {timeText}";
    }

    private static bool IsConsecutive(List<int> sorted)
    {
        for (var i = 1; i < sorted.Count; i++)
            if (sorted[i] != sorted[i - 1] + 1) return false;
        return true;
    }
}
