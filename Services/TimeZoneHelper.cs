using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

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

public static class TimeZoneHelper
{
    private const string DefaultWindowsTimeZoneId = "Eastern Standard Time";
    private const string DefaultIanaTimeZoneId = "America/New_York";

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
        return TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(tz => tz.BaseUtcOffset)
            .ThenBy(tz => tz.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), GetTimeZone(timeZoneId));
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

        var tz = GetTimeZone(timeZoneId);
        var unspecified = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        utcTime = TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
        return true;
    }

    public static bool TryParseInZone(string input, ClockTimeZone zone, out DateTime utcTime)
    {
        return TryParseInZone(input, ToTimeZoneId(zone), out utcTime);
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
