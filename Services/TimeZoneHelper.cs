using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

// Eorzea Time related codes were done using explicitly specific names and there is
// a large amount of Notes about Eorzea Time relateds because I struggled a bit with it.

namespace Clock;

public sealed class CountryTimeZoneOption
{
    public CountryTimeZoneOption(string countryName, string timeZoneId)
    {
        CountryName = countryName;
        TimeZoneId = timeZoneId;
    }

    public string CountryName { get; }
    public string TimeZoneId { get; }

    public string Label => $"{CountryName} - ({TimeZoneHelper.ToShortText(TimeZoneId)}) {TimeZoneId}";
}

// Maps user-friendly time zone labels to actual offsets/IDs.
public static class TimeZoneHelper
{
    public const string ServerTimeZoneId = "SERVER_TIME";
    public const string EorzeaTimeZoneId = "EORZEA_TIME";
    private const double EorzeaSecondsPerRealSecond = 3600.0 / 175.0;
    private static readonly Lazy<IReadOnlyList<TimeZoneInfo>> OrderedSystemTimeZones = new(() =>
        TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(tz => tz.BaseUtcOffset)
            .ThenBy(tz => tz.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    private const string DefaultWindowsTimeZoneId = "Eastern Standard Time";
    private const string DefaultIanaTimeZoneId = "America/New_York";

    // This alias map is intentionally explicit instead of being pulled from game data.
    // Keeping the common aliases here lets user input like
    // "EST", "JST", "GMT" or "UTC" resolve consistently across Windows/Linux
    // without comparing against localized display names at runtime.
    private static readonly (string Alias, string WindowsId, string IanaId)[] AliasMap =
    {
        ("est", "Eastern Standard Time", "America/New_York"),
        ("edt", "Eastern Standard Time", "America/New_York"),
        ("eastern", "Eastern Standard Time", "America/New_York"),
        ("new york", "Eastern Standard Time", "America/New_York"),

        ("pst", "Pacific Standard Time", "America/Los_Angeles"),
        ("pdt", "Pacific Standard Time", "America/Los_Angeles"),
        ("pst/pdt", "Pacific Standard Time", "America/Los_Angeles"),
        ("pacific", "Pacific Standard Time", "America/Los_Angeles"),
        ("los angeles", "Pacific Standard Time", "America/Los_Angeles"),

        ("utc", "UTC", "Etc/UTC"),
        ("gmt", "UTC", "Etc/UTC"),
        ("utc/gmt", "UTC", "Etc/UTC"),
        ("universal", "UTC", "Etc/UTC"),

        ("bst", "GMT Standard Time", "Europe/London"),
        ("british", "GMT Standard Time", "Europe/London"),
        ("british summer", "GMT Standard Time", "Europe/London"),
        ("london", "GMT Standard Time", "Europe/London"),

        ("jst", "Tokyo Standard Time", "Asia/Tokyo"),
        ("japan", "Tokyo Standard Time", "Asia/Tokyo"),
        ("tokyo", "Tokyo Standard Time", "Asia/Tokyo"),

        ("mst", "Mountain Standard Time", "America/Denver"),
        ("mdt", "Mountain Standard Time", "America/Denver"),
        ("mountain", "Mountain Standard Time", "America/Denver"),
        ("denver", "Mountain Standard Time", "America/Denver"),

        ("cst", "Central Standard Time", "America/Chicago"),
        ("cdt", "Central Standard Time", "America/Chicago"),
        ("central", "Central Standard Time", "America/Chicago"),
        ("chicago", "Central Standard Time", "America/Chicago"),

        ("cet", "W. Europe Standard Time", "Europe/Berlin"),
        ("cest", "W. Europe Standard Time", "Europe/Berlin"),
        ("berlin", "W. Europe Standard Time", "Europe/Berlin"),

        ("acst", "AUS Central Standard Time", "Australia/Darwin"),
        ("acdt", "AUS Central Standard Time", "Australia/Adelaide"),
        ("australia central", "AUS Central Standard Time", "Australia/Darwin"),
        ("australiacentral", "AUS Central Standard Time", "Australia/Darwin"),
        ("darwin", "AUS Central Standard Time", "Australia/Darwin"),

        ("aest", "AUS Eastern Standard Time", "Australia/Sydney"),
        ("aedt", "AUS Eastern Standard Time", "Australia/Sydney"),
        ("sydney", "AUS Eastern Standard Time", "Australia/Sydney"),

        ("kst", "Korea Standard Time", "Asia/Seoul"),
        ("sgt", "Singapore Standard Time", "Asia/Singapore"),
        ("hkt", "China Standard Time", "Asia/Hong_Kong"),
        ("ist", "India Standard Time", "Asia/Kolkata"),

        ("brt", "E. South America Standard Time", "America/Sao_Paulo"),
        ("brazil", "E. South America Standard Time", "America/Sao_Paulo"),
        ("brasil", "E. South America Standard Time", "America/Sao_Paulo"),
        ("sao paulo", "E. South America Standard Time", "America/Sao_Paulo"),
        ("são paulo", "E. South America Standard Time", "America/Sao_Paulo"),
    };

    // This country map is intentionally maintained as a small curated fallback list.
    // Each entry stores both Windows/IANA ids so the plugin can resolve the same
    // common country shortcut on different systems without relying on localized
    // display names or fragile string comparisons.
    private static readonly (string CountryName, string WindowsId, string IanaId)[] CountryTimeZoneMap =
    {
        ("Brasil", "E. South America Standard Time", "America/Sao_Paulo"),
        ("Brazil", "E. South America Standard Time", "America/Sao_Paulo"),
        ("Estados Unidos", "Eastern Standard Time", "America/New_York"),
        ("Estados Unidos - Pacific", "Pacific Standard Time", "America/Los_Angeles"),
        ("Estados Unidos - Mountain", "Mountain Standard Time", "America/Denver"),
        ("Estados Unidos - Central", "Central Standard Time", "America/Chicago"),
        ("United States", "Eastern Standard Time", "America/New_York"),
        ("United States - Pacific", "Pacific Standard Time", "America/Los_Angeles"),
        ("United States - Mountain", "Mountain Standard Time", "America/Denver"),
        ("United States - Central", "Central Standard Time", "America/Chicago"),
        ("México", "Central Standard Time (Mexico)", "America/Mexico_City"),
        ("Mexico", "Central Standard Time (Mexico)", "America/Mexico_City"),
        ("Canada", "Eastern Standard Time", "America/Toronto"),
        ("Canada - Pacific", "Pacific Standard Time", "America/Vancouver"),
        ("Reino Unido", "GMT Standard Time", "Europe/London"),
        ("United Kingdom", "GMT Standard Time", "Europe/London"),
        ("Portugal", "GMT Standard Time", "Europe/Lisbon"),
        ("Japão", "Tokyo Standard Time", "Asia/Tokyo"),
        ("Japan", "Tokyo Standard Time", "Asia/Tokyo"),
        ("Coreia do Sul", "Korea Standard Time", "Asia/Seoul"),
        ("South Korea", "Korea Standard Time", "Asia/Seoul"),
        ("China", "China Standard Time", "Asia/Shanghai"),
        ("Hong Kong", "China Standard Time", "Asia/Hong_Kong"),
        ("Taiwan", "Taipei Standard Time", "Asia/Taipei"),
        ("Singapura", "Singapore Standard Time", "Asia/Singapore"),
        ("Singapore", "Singapore Standard Time", "Asia/Singapore"),
        ("Austrália", "AUS Eastern Standard Time", "Australia/Sydney"),
        ("Australia", "AUS Eastern Standard Time", "Australia/Sydney"),
        ("Australia - Central", "AUS Central Standard Time", "Australia/Darwin"),
        ("Nova Zelândia", "New Zealand Standard Time", "Pacific/Auckland"),
        ("New Zealand", "New Zealand Standard Time", "Pacific/Auckland"),
        ("Alemanha", "W. Europe Standard Time", "Europe/Berlin"),
        ("Germany", "W. Europe Standard Time", "Europe/Berlin"),
        ("França", "Romance Standard Time", "Europe/Paris"),
        ("France", "Romance Standard Time", "Europe/Paris"),
        ("Espanha", "Romance Standard Time", "Europe/Madrid"),
        ("Spain", "Romance Standard Time", "Europe/Madrid"),
        ("Itália", "W. Europe Standard Time", "Europe/Rome"),
        ("Italy", "W. Europe Standard Time", "Europe/Rome"),
        ("Holanda", "W. Europe Standard Time", "Europe/Amsterdam"),
        ("Netherlands", "W. Europe Standard Time", "Europe/Amsterdam"),
        ("Noruega", "W. Europe Standard Time", "Europe/Oslo"),
        ("Norway", "W. Europe Standard Time", "Europe/Oslo"),
        ("Suécia", "W. Europe Standard Time", "Europe/Stockholm"),
        ("Sweden", "W. Europe Standard Time", "Europe/Stockholm"),
        ("Finlândia", "FLE Standard Time", "Europe/Helsinki"),
        ("Finland", "FLE Standard Time", "Europe/Helsinki"),
        ("Polônia", "Central European Standard Time", "Europe/Warsaw"),
        ("Poland", "Central European Standard Time", "Europe/Warsaw"),
        ("Rússia", "Russian Standard Time", "Europe/Moscow"),
        ("Russia", "Russian Standard Time", "Europe/Moscow"),
        ("Turquia", "Turkey Standard Time", "Europe/Istanbul"),
        ("Turkey", "Turkey Standard Time", "Europe/Istanbul"),
        ("Índia", "India Standard Time", "Asia/Kolkata"),
        ("India", "India Standard Time", "Asia/Kolkata"),
        ("Tailândia", "SE Asia Standard Time", "Asia/Bangkok"),
        ("Thailand", "SE Asia Standard Time", "Asia/Bangkok"),
        ("Vietnã", "SE Asia Standard Time", "Asia/Ho_Chi_Minh"),
        ("Vietnam", "SE Asia Standard Time", "Asia/Ho_Chi_Minh"),
        ("Filipinas", "Singapore Standard Time", "Asia/Manila"),
        ("Philippines", "Singapore Standard Time", "Asia/Manila"),
        ("Indonésia", "SE Asia Standard Time", "Asia/Jakarta"),
        ("Indonesia", "SE Asia Standard Time", "Asia/Jakarta"),
        ("Argentina", "Argentina Standard Time", "America/Argentina/Buenos_Aires"),
        ("Chile", "Pacific SA Standard Time", "America/Santiago"),
        ("Colômbia", "SA Pacific Standard Time", "America/Bogota"),
        ("Colombia", "SA Pacific Standard Time", "America/Bogota"),
        ("Peru", "SA Pacific Standard Time", "America/Lima"),
        ("África do Sul", "South Africa Standard Time", "Africa/Johannesburg"),
        ("South Africa", "South Africa Standard Time", "Africa/Johannesburg"),
    };

    public static IReadOnlyList<TimeZoneInfo> GetSystemTimeZones()
    {
        return OrderedSystemTimeZones.Value;
    }

    public static IReadOnlyList<CountryTimeZoneOption> GetCountryTimeZoneOptions()
    {
        return CountryTimeZoneMap
            .Select(entry => new CountryTimeZoneOption(entry.CountryName, ResolveKnownZoneId(entry.WindowsId, entry.IanaId)))
            .OrderBy(entry => entry.CountryName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static TimeZoneInfo GetTimeZone(string? timeZoneId)
    {
        if (IsServerTime(timeZoneId))
            return TimeZoneInfo.Utc;

        if (IsEorzeaTime(timeZoneId))
            return TimeZoneInfo.Utc;

        if (TryResolveTimeZone(timeZoneId, out var resolvedId) && TryFindDirect(resolvedId, out var resolvedZone))
            return resolvedZone;

        if (TryFindPair(DefaultWindowsTimeZoneId, DefaultIanaTimeZoneId, out var defaultZone))
            return defaultZone;

        return TimeZoneInfo.Local;
    }

    public static TimeZoneInfo GetTimeZone(ClockTimeZone zone)
    {
        return GetTimeZone(ToTimeZoneId(zone));
    }

    public static string NormalizeTimeZoneId(string? timeZoneId)
    {
        if (IsServerTime(timeZoneId))
            return ServerTimeZoneId;

        if (IsEorzeaTime(timeZoneId))
            return EorzeaTimeZoneId;

        return TryResolveTimeZone(timeZoneId, out var resolvedId)
            ? resolvedId
            : ToTimeZoneId(ClockTimeZone.EST);
    }

    public static string ToTimeZoneId(ClockTimeZone zone)
    {
        return zone switch
        {
            ClockTimeZone.Pacific => ResolveKnownZoneId("Pacific Standard Time", "America/Los_Angeles"),
            ClockTimeZone.Universal => ResolveKnownZoneId("UTC", "Etc/UTC"),
            ClockTimeZone.BST => ResolveKnownZoneId("GMT Standard Time", "Europe/London"),
            ClockTimeZone.JST => ResolveKnownZoneId("Tokyo Standard Time", "Asia/Tokyo"),
            ClockTimeZone.MST => ResolveKnownZoneId("Mountain Standard Time", "America/Denver"),
            ClockTimeZone.ACST => ResolveKnownZoneId("AUS Central Standard Time", "Australia/Darwin"),
            _ => ResolveKnownZoneId(DefaultWindowsTimeZoneId, DefaultIanaTimeZoneId),
        };
    }

    public static bool TryResolveTimeZone(string? input, out string timeZoneId)
    {
        timeZoneId = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        if (IsServerTime(trimmed))
        {
            timeZoneId = ServerTimeZoneId;
            return true;
        }

        if (IsEorzeaTime(trimmed))
        {
            timeZoneId = EorzeaTimeZoneId;
            return true;
        }

        var normalized = NormalizeAlias(trimmed);
        var aliasMatch = AliasMap.FirstOrDefault(a => NormalizeAlias(a.Alias) == normalized);
        if (!string.IsNullOrWhiteSpace(aliasMatch.Alias) && TryFindAliasPair(aliasMatch.WindowsId, aliasMatch.IanaId, out var aliasZone))
        {
            timeZoneId = aliasZone.Id;
            return true;
        }

        if (TryFindDirect(trimmed, out var directZone))
        {
            timeZoneId = directZone.Id;
            return true;
        }

        return false;
    }

    public static bool MatchesFilter(TimeZoneInfo timeZone, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        var needle = filter.Trim();
        var abbreviation = ToShortText(timeZone.Id);

        return timeZone.Id.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || timeZone.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || timeZone.StandardName.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || timeZone.DaylightName.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || abbreviation.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesCountryFilter(CountryTimeZoneOption option, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        var needle = filter.Trim();
        return option.CountryName.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || RemoveDiacritics(option.CountryName).Contains(RemoveDiacritics(needle), StringComparison.OrdinalIgnoreCase)
            || option.TimeZoneId.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || ToShortText(option.TimeZoneId).Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldHighlightTimeZone(string? timeZoneId)
    {
        var shortText = ToShortText(timeZoneId);
        if (shortText is "EST" or "PST" or "GMT" or "MDT")
            return true;

        var id = NormalizeIdForComparison(GetTimeZone(timeZoneId).Id);
        return id.Contains("easternstandardtime")
            || id.Contains("americanewyork")
            || id.Contains("pacificstandardtime")
            || id.Contains("americalosangeles")
            || id == "utc"
            || id.Contains("etcutc")
            || id.Contains("greenwich")
            || id.Contains("gmt")
            || id.Contains("mountainstandardtime")
            || id.Contains("americadenver");
    }

    public static string GetComboLabel(string? timeZoneId)
    {
        if (IsServerTime(timeZoneId))
            return "(ST) Server Time";

        if (IsEorzeaTime(timeZoneId))
            return "(ET) Eorzea Time";

        return GetComboLabel(GetTimeZone(timeZoneId));
    }

    public static string GetComboLabel(TimeZoneInfo timeZone)
    {
        var abbreviation = ToShortText(timeZone.Id);
        var details = string.IsNullOrWhiteSpace(timeZone.StandardName) || string.Equals(timeZone.StandardName, timeZone.Id, StringComparison.OrdinalIgnoreCase)
            ? ""
            : $" - {timeZone.StandardName}";

        return $"({abbreviation}) {timeZone.Id}{details}";
    }

    public static string ToShortText(this ClockTimeZone zone)
    {
        return ToShortText(ToTimeZoneId(zone));
    }

    public static string ToShortText(string? timeZoneId)
    {
        if (IsServerTime(timeZoneId))
            return "Server Time";

        if (IsEorzeaTime(timeZoneId))
            return "Eorzea";

        // These abbreviation checks are intentionally kept as a practical display layer.
        // System timezone IDs differ between Windows/IANA platforms, and their
        // StandardName/DaylightName values can also vary by OS language. The common
        // IDs handled here give the UI stable short labels like EST, JST, BRT or GMT
        // for the timezones that players are most likely to search/select. If none of the
        // known IDs match, the method falls back to building an abbreviation from the
        // OS-provided names and finally to the UTC offset so every timezone still has
        // a readable label no matter what.
        var timeZone = GetTimeZone(timeZoneId);
        var id = NormalizeIdForComparison(timeZone.Id);

        if (id.Contains("pacificstandardtime") || id.Contains("americalosangeles") || id.Contains("americavancouver"))
            return "PST";

        if (id.Contains("easternstandardtime") || id.Contains("americanewyork") || id.Contains("americatoronto"))
            return "EST";

        if (id.Contains("mountainstandardtime") || id.Contains("americadenver"))
            return "MDT";

        if (id.Contains("centralstandardtime") || id.Contains("americachicago"))
            return "CST";

        if (id == "utc" || id.Contains("etcutc") || id.Contains("greenwich"))
            return "GMT";

        if (id.Contains("gmtstandardtime") || id.Contains("europelondon") || id.Contains("europelisbon"))
            return "GMT";

        if (id.Contains("tokyo") || id.Contains("asiatokyo"))
            return "JST";

        if (id.Contains("koreastandardtime") || id.Contains("asiaseoul"))
            return "KST";

        if (id.Contains("chinastandardtime") || id.Contains("asiashanghai") || id.Contains("asiahongkong"))
            return "CST";

        if (id.Contains("taipei") || id.Contains("asiataipei"))
            return "TST";

        if (id.Contains("singapore") || id.Contains("asiasingapore") || id.Contains("asiamanila"))
            return "SGT";

        if (id.Contains("auscentral") || id.Contains("australiadarwin") || id.Contains("australiaadelaide"))
            return "ACST";

        if (id.Contains("auseastern") || id.Contains("australiasydney"))
            return "AEST";

        if (id.Contains("newzealand") || id.Contains("pacificauckland"))
            return "NZST";

        if (id.Contains("southamerica") || id.Contains("saopaulo") || id.Contains("sao_paulo"))
            return "BRT";

        if (id.Contains("argentina") || id.Contains("buenosaires"))
            return "ART";

        if (id.Contains("pacificsa") || id.Contains("americasantiago"))
            return "CLT";

        if (id.Contains("sapore") || id.Contains("americabogota") || id.Contains("americalima"))
            return "COT";

        if (id.Contains("southafrica") || id.Contains("africajohannesburg"))
            return "SAST";

        if (id.Contains("india") || id.Contains("asiakolkata"))
            return "IST";

        if (id.Contains("turkey") || id.Contains("europeistanbul"))
            return "TRT";

        if (id.Contains("russian") || id.Contains("europemoscow"))
            return "MSK";

        var standardAbbreviation = BuildAbbreviation(timeZone.StandardName);
        if (!string.IsNullOrWhiteSpace(standardAbbreviation))
            return standardAbbreviation;

        var daylightAbbreviation = BuildAbbreviation(timeZone.DaylightName);
        if (!string.IsNullOrWhiteSpace(daylightAbbreviation))
            return daylightAbbreviation;

        return FormatUtcOffset(timeZone.BaseUtcOffset);
    }

    public static DateTime ConvertFromUtc(DateTime utc, string? timeZoneId)
    {
        if (IsEorzeaTime(timeZoneId))
            return ConvertToEorzeaTime(utc);

        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), GetTimeZone(timeZoneId));
    }

    public static DateTime ConvertFromUtcForDisplay(DateTime utc, string? timeZoneId)
    {
        var local = ConvertFromUtc(utc, timeZoneId);
        if (!IsEorzeaTime(timeZoneId))
            return local;

        return local.AddSeconds(0.5).AddTicks(-(local.Ticks % TimeSpan.TicksPerSecond));
    }

    // This adds minutes using Eorzea Time speed.
    // 1 Eorzea hour lasts 175 real seconds, so one Eorzea minute is
    // 175 / 60 real seconds. This is used for Eorzea snooze timers so a 10-minute
    // snooze means 10 in-game minutes, not 10 real minutes.

    public static DateTime AddEorzeaMinutes(DateTime utc, int minutes)
    {
        minutes = Math.Clamp(minutes, 1, 120);
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddSeconds(minutes * 175.0 / 60.0);
    }

    public static DateTime ConvertFromUtc(DateTime utc, ClockTimeZone zone)
    {
        return ConvertFromUtc(utc, ToTimeZoneId(zone));
    }

    public static bool TryParseInZone(string input, string? timeZoneId, out DateTime utcTime)
    {
        utcTime = DateTime.MinValue;

        if (!DateTime.TryParseExact(
                input.Trim(),
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        return TryConvertLocalToUtc(parsed, timeZoneId, out utcTime);
    }

    public static bool TryParseInZone(string input, ClockTimeZone zone, out DateTime utcTime)
    {
        return TryParseInZone(input, ToTimeZoneId(zone), out utcTime);
    }


    public static bool IsSpecialGameTime(string? timeZoneId)
    {
        return IsServerTime(timeZoneId) || IsEorzeaTime(timeZoneId);
    }

    public static bool IsServerTime(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return false;

        var text = timeZoneId.Trim();
        return string.Equals(text, ServerTimeZoneId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "ST", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "Server Time", StringComparison.OrdinalIgnoreCase);
    }

    // Accepts every internal/user-facing name I used for Eorzea Time.
    // The plugin stores this option with its stable internal id but users may also
    // reach the same mode through short labels like "ET" or visible labels like
    // "Eorzea Time", so this helper keeps all those checks in one place.
    public static bool IsEorzeaTime(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return false;

        var text = timeZoneId.Trim();
        return string.Equals(text, EorzeaTimeZoneId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "ET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "Eorzea Time", StringComparison.OrdinalIgnoreCase);
    }

    // Converts a real UTC instant into the matching Eorzea clock value.
    // The UTC Unix timestamp is scaled by the Eorzea-to-real-time ratio instead.
    // The fixed year 1 base is only used as a neutral DateTime container for the
    // calculated in-game month/day/hour/minute values, not as a real calendar date.
    private static DateTime ConvertToEorzeaTime(DateTime utc)
    {
        var unixSeconds = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var eorzeaSeconds = unixSeconds * EorzeaSecondsPerRealSecond;
        var eorzeaBase = new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return eorzeaBase.AddSeconds(eorzeaSeconds);
    }

    // Eorzea Time is not a real timezone, so it cannot be converted like UTC, local time or a system timezone.
    // This treats the selected hour/minute as an in-game clock value and searches for the next
    // real UTC moment where that Eorzea time occurs. The selected real date is still respected but users may be able to
    // create alarms for past real-world time, while the trigger itself follows the game's accelerated time scale
    // where one Eorzea hour is treated like 175 real seconds. https://ffxiv.consolegameswiki.com/wiki/Eorzea_Time
    public static bool TryGetEditorTriggerUtc(DateTime editorLocal, string? timeZoneId, out DateTime utcTime)
    {
        utcTime = DateTime.MinValue;

        if (IsEorzeaTime(timeZoneId))
            return TryGetNextEorzeaUtcForRealDate(editorLocal.Date, editorLocal.Hour, editorLocal.Minute, out utcTime);

        return TryConvertLocalToUtc(editorLocal, timeZoneId, out utcTime);
    }

    public static bool TryConvertLocalToUtc(DateTime local, string? timeZoneId, out DateTime utcTime)
    {
        utcTime = DateTime.MinValue;
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        if (IsEorzeaTime(timeZoneId))
        {
            utcTime = GetNextEorzeaUtc(unspecified.Hour, unspecified.Minute);
            return true;
        }

        var zone = GetTimeZone(timeZoneId);
        utcTime = TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
        return true;
    }


    public static bool TryGetNextEorzeaUtcForRealDate(DateTime realLocalDate, int hour, int minute, out DateTime utcTime)
    {
        utcTime = DateTime.MinValue;
        hour = Math.Clamp(hour, 0, 23);
        minute = Math.Clamp(minute, 0, 59);

        var localStart = DateTime.SpecifyKind(realLocalDate.Date, DateTimeKind.Local);
        var localEnd = localStart.AddDays(1);
        var startUtc = localStart.ToUniversalTime();
        var endUtc = localEnd.ToUniversalTime();
        var searchUtc = DateTime.UtcNow > startUtc ? DateTime.UtcNow : startUtc;

        if (searchUtc >= endUtc)
            return false;

        utcTime = GetNextEorzeaUtcAfter(searchUtc, hour, minute);
        return utcTime < endUtc;
    }

    public static DateTime GetNextEorzeaUtcAfter(DateTime afterUtc, int hour, int minute)
    {
        hour = Math.Clamp(hour, 0, 23);
        minute = Math.Clamp(minute, 0, 59);

        var fromUtc = DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);
        var rawEt = ConvertToEorzeaTime(fromUtc);
        var currentEt = rawEt.AddSeconds(0.5).AddTicks(-(rawEt.Ticks % TimeSpan.TicksPerSecond));
        var targetEt = new DateTime(currentEt.Year, currentEt.Month, currentEt.Day, hour, minute, 0);

        while (targetEt <= currentEt)
            targetEt = targetEt.AddDays(1);

        var etDeltaSeconds = (targetEt - currentEt).TotalSeconds;
        var realDeltaSeconds = etDeltaSeconds / EorzeaSecondsPerRealSecond;
        return DateTime.SpecifyKind(fromUtc.AddSeconds(realDeltaSeconds + 0.05), DateTimeKind.Utc);
    }

    public static DateTime GetNextEorzeaUtc(int hour, int minute)
    {
        return GetNextEorzeaUtcAfter(DateTime.UtcNow, hour, minute);
    }

    private static string ResolveKnownZoneId(string windowsId, string ianaId)
    {
        if (TryFindPair(windowsId, ianaId, out var timeZone))
            return timeZone.Id;

        return windowsId;
    }

    private static bool TryFindPair(string windowsId, string ianaId, out TimeZoneInfo timeZone)
    {
        return TryFindDirect(windowsId, out timeZone) || TryFindDirect(ianaId, out timeZone);
    }

    private static bool TryFindAliasPair(string windowsId, string ianaId, out TimeZoneInfo timeZone)
    {
        return TryFindDirect(ianaId, out timeZone) || TryFindDirect(windowsId, out timeZone);
    }

    private static bool TryFindDirect(string timeZoneId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }

    private static string NormalizeAlias(string value)
    {
        return value.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");
    }

    private static string NormalizeIdForComparison(string value)
    {
        return RemoveDiacritics(value).ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Replace("/", "")
            .Replace(".", "");
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string? BuildAbbreviation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var clean = value.Trim();
        if (clean.StartsWith("+", StringComparison.Ordinal) || clean.StartsWith("-", StringComparison.Ordinal))
            return null;

        var words = clean
            .Split(new[] { ' ', '-', '_', '/', '(', ')', '&' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !word.Equals("Time", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (words.Length == 0)
            return null;

        var abbreviation = string.Concat(words.Select(word => char.ToUpperInvariant(word[0])));
        return abbreviation.Length is >= 2 and <= 5 ? abbreviation : null;
    }

    private static string FormatUtcOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        return $"UTC{sign}{offset.Hours:00}:{offset.Minutes:00}";
    }
}
