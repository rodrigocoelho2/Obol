namespace Obol.Website.Helpers;

/// <summary>
/// The Website's wall clock, in the same time zone as the WebApi (Website:TimeZone, Obol:TimeZone there), so "today"
/// means the same day on both sides even when the container runs in UTC.
/// </summary>
public static class Clock
{
    private static TimeZoneInfo zone = TimeZoneInfo.Local;

    /// <summary>Gets the time zone.</summary>
    public static TimeZoneInfo Zone => zone;

    /// <summary>Gets the local time now.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);

    /// <summary>Gets today's date.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>
    /// Sets the time zone from its IANA or Windows id; an empty or unknown id keeps the machine's zone.
    /// </summary>
    /// <param name="id">The id (e.g. Europe/Lisbon).</param>
    public static void Use(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Local;
        }
    }
}
