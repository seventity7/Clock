using System;
using System.Globalization;

namespace Clock;

public static class TimeFormatHelper
{
    public static readonly string[] Names =
    {
        "12-hour",
        "24-hour",
        "12-hour + seconds",
        "24-hour + seconds",
        "Weekday + 24-hour",
        "Date + 24-hour"
    };

    public static string GetName(ClockTimeFormat format)
    {
        var index = Math.Clamp((int)format, 0, Names.Length - 1);
        return Names[index];
    }

    public static bool UsesTwelveHourClock(ClockTimeFormat format)
    {
        return format is ClockTimeFormat.TwelveHour or ClockTimeFormat.TwelveHourSeconds;
    }

    public static bool UsesStructuredClock(ClockTimeFormat format)
    {
        return true;
    }

    public static string FormatClock(DateTime dateTime, ClockTimeFormat format)
    {
        return format switch
        {
            ClockTimeFormat.TwelveHour => dateTime.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant().Replace("am", "a.m.").Replace("pm", "p.m."),
            ClockTimeFormat.TwentyFourHour => dateTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            ClockTimeFormat.TwelveHourSeconds => dateTime.ToString("h:mm:ss tt", CultureInfo.InvariantCulture).ToLowerInvariant().Replace("am", "a.m.").Replace("pm", "p.m."),
            ClockTimeFormat.TwentyFourHourSeconds => dateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            ClockTimeFormat.WeekdayTwentyFourHour => dateTime.ToString("ddd HH:mm", CultureInfo.InvariantCulture),
            ClockTimeFormat.DateTwentyFourHour => dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            _ => dateTime.ToString("HH:mm", CultureInfo.InvariantCulture)
        };
    }
}
