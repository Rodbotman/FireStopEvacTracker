using System;

namespace FireStopEvacTracker.Helpers;

/// <summary>
/// Helper methods for displaying dates and times in Sydney timezone (AEST/AEDT)
/// </summary>
public static class DateTimeHelper
{
    private static readonly TimeZoneInfo SydneyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    /// <summary>
    /// Convert UTC DateTime to Sydney time
    /// </summary>
    public static DateTime ToSydneyTime(this DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTime(utcDateTime, SydneyTimeZone);
    }

    /// <summary>
    /// Format DateTime as Sydney time with pattern
    /// </summary>
    public static string ToSydneyString(this DateTime utcDateTime, string format = "dd/MM/yyyy HH:mm")
    {
        return utcDateTime.ToSydneyTime().ToString(format);
    }
}
