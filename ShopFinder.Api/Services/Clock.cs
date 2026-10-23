namespace ShopFinder.Api.Services;

/// <summary>
/// Gives "now" in the city's local time zone so opening hours are evaluated correctly
/// even if the server runs in UTC. Configure App:TimeZone with an IANA id such as
/// "America/New_York" or "Asia/Kolkata". Empty = the server's local time zone.
/// </summary>
public class Clock
{
    public TimeZoneInfo Zone { get; }

    public Clock(IConfiguration config)
    {
        var id = config["App:TimeZone"];
        Zone = string.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(id);
    }

    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
    public DateOnly LocalToday => DateOnly.FromDateTime(LocalNow);

    public DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
}
