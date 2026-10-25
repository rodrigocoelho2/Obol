using System.Globalization;

namespace Obol.WebApi.Extensions.Data;

/// <summary>
/// Dates and times as the stored procedures read them from JSON (OPENJSON … DATETIME2 / DATE).
/// </summary>
internal static class DbTime
{
    private const string TimeFormat = "yyyy-MM-ddTHH:mm:ss";
    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>A UTC time, to the second.</summary>
    /// <param name="utc">The time.</param>
    /// <returns>yyyy-MM-ddTHH:mm:ss.</returns>
    public static string Time(DateTime utc) => utc.ToString(TimeFormat, CultureInfo.InvariantCulture);

    /// <summary>A day.</summary>
    /// <param name="day">The day.</param>
    /// <returns>yyyy-MM-dd.</returns>
    public static string Day(DateOnly day) => day.ToString(DayFormat, CultureInfo.InvariantCulture);

    /// <summary>Reads a time written by <see cref="Time"/> (the memory repositories).</summary>
    /// <param name="text">The text.</param>
    /// <returns>The time (Kind Utc).</returns>
    public static DateTime ParseTime(string text) =>
        DateTime.SpecifyKind(DateTime.ParseExact(text, TimeFormat, CultureInfo.InvariantCulture), DateTimeKind.Utc);

    /// <summary>Reads a day written by <see cref="Day"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The day.</returns>
    public static DateOnly ParseDay(string text) => DateOnly.ParseExact(text, DayFormat, CultureInfo.InvariantCulture);
}
