// Remaking maintenance detection system for Lodestone/RSS check instead
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Clock.Services;

public sealed class LodestoneMaintenanceInfo
{
    public LodestoneMaintenanceInfo(
        string title,
        string url,
        string localStartText,
        string timeZoneText,
        DateTime startUtc,
        DateTime? endUtc)
    {
        Title = title;
        Url = url;
        LocalStartText = localStartText;
        TimeZoneText = timeZoneText;
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public string Title { get; }
    public string Url { get; }
    public string LocalStartText { get; }
    public string TimeZoneText { get; }
    public DateTime StartUtc { get; }
    public DateTime? EndUtc { get; }

    public string BuildSummary()
    {
        return $"{Title} - starts at {LocalStartText} {TimeZoneText}";
    }
}

public sealed class LodestoneMaintenanceService : IDisposable
{
    private const string MaintenanceNewsUrl = "https://na.finalfantasyxiv.com/lodestone/news/category/2";
    private static readonly Uri MaintenanceNewsUri = new(MaintenanceNewsUrl);

    private static readonly Regex DetailLinkRegex = new(
        "href=\\\"(?<path>/lodestone/news/detail/[^\\\"]+)\\\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    private static readonly Regex PageCategoryRegex = new(
        @"#\s*\[(?<category>[^\]]+)\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MaintenanceWindowRegex = new(
        @"(?<startMonth>Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:t|tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\.?\s+" +
        @"(?<startDay>\d{1,2}),\s+(?<startYear>\d{4})\s+" +
        @"(?<startHour>\d{1,2}):(?<startMinute>\d{2})\s*(?<startPeriod>a\.m\.|p\.m\.|am|pm)?\s+to\s+" +
        @"(?:(?<endMonth>Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:t|tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\.?\s+" +
        @"(?<endDay>\d{1,2}),\s+(?<endYear>\d{4})\s+)?" +
        @"(?<endHour>\d{1,2}):(?<endMinute>\d{2})\s*(?<endPeriod>a\.m\.|p\.m\.|am|pm)?\s*\((?<zone>[A-Z]{2,5})\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient httpClient = new();

    public LodestoneMaintenanceService()
    {
        httpClient.Timeout = TimeSpan.FromSeconds(15);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Clock Dalamud Plugin/1.0");
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    public async Task<LodestoneMaintenanceInfo?> GetLatestMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        var indexHtml = await httpClient.GetStringAsync(MaintenanceNewsUri, cancellationToken).ConfigureAwait(false);
        var detailUrls = ExtractDetailUrls(indexHtml).Take(10).ToList();

        var nowUtc = DateTime.UtcNow;
        var candidates = new List<LodestoneMaintenanceInfo>();

        foreach (var detailUrl in detailUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var html = await httpClient.GetStringAsync(detailUrl, cancellationToken).ConfigureAwait(false);
                if (!TryParseMaintenancePage(html, detailUrl, nowUtc, out var maintenance))
                    continue;

                candidates.Add(maintenance);
            }
            catch
            {
                // A single Lodestone page failing should not prevent newer entries from being checked.
            }
        }

        return candidates
            .Where(x => x.EndUtc == null || x.EndUtc.Value >= nowUtc.AddMinutes(-30))
            .OrderBy(x => x.StartUtc)
            .FirstOrDefault();
    }

    private static IEnumerable<string> ExtractDetailUrls(string html)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in DetailLinkRegex.Matches(html))
        {
            var path = match.Groups["path"].Value;
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var url = new Uri(MaintenanceNewsUri, path).ToString();
            if (seen.Add(url))
                yield return url;
        }
    }

    private static bool TryParseMaintenancePage(
        string html,
        string url,
        DateTime nowUtc,
        out LodestoneMaintenanceInfo maintenance)
    {
        maintenance = null!;

        var text = NormalizeHtmlText(html);
        var category = PageCategoryRegex.Match(text).Groups["category"].Value.Trim();
        if (!category.Equals("Maintenance", StringComparison.OrdinalIgnoreCase))
            return false;

        var title = ExtractTitle(html);
        if (title.Contains("follow-up", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("cancellation", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Cancellation of", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dateSection = ExtractDateSection(text);
        var match = MaintenanceWindowRegex.Match(dateSection);
        if (!match.Success)
            return false;

        var zoneText = match.Groups["zone"].Value.ToUpperInvariant();
        if (!TryGetLodestoneTimeZone(zoneText, out var timeZone))
            return false;

        if (!TryBuildLocalDateTime(match, "start", out var localStart))
            return false;

        if (!TryBuildLocalDateTime(match, "end", out var localEnd))
            return false;

        if (!match.Groups["endMonth"].Success)
        {
            localEnd = new DateTime(localStart.Year, localStart.Month, localStart.Day, localEnd.Hour, localEnd.Minute, 0);
            if (localEnd <= localStart)
                localEnd = localEnd.AddDays(1);
        }

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified), timeZone);

        if (endUtc < nowUtc.AddMinutes(-30))
            return false;

        maintenance = new LodestoneMaintenanceInfo(
            title,
            url,
            localStart.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            zoneText,
            startUtc,
            endUtc);

        return true;
    }

    private static string NormalizeHtmlText(string html)
    {
        var withLineBreaks = Regex.Replace(html, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        withLineBreaks = Regex.Replace(withLineBreaks, @"</\s*(p|div|li|h1|h2|h3)\s*>", "\n", RegexOptions.IgnoreCase);
        var withoutTags = HtmlTagRegex.Replace(withLineBreaks, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }

    private static string ExtractTitle(string html)
    {
        var titleMatch = Regex.Match(
            html,
            @"<title>\s*(?<title>.*?)\s*\|\s*FINAL FANTASY XIV",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!titleMatch.Success)
            return "Lodestone Maintenance";

        var decoded = WebUtility.HtmlDecode(titleMatch.Groups["title"].Value);
        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }

    private static string ExtractDateSection(string text)
    {
        var start = text.IndexOf("[Date & Time]", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return text;

        var end = text.IndexOf("[Affected Service]", start, StringComparison.OrdinalIgnoreCase);
        return end > start
            ? text[start..end]
            : text[start..];
    }

    private static bool TryBuildLocalDateTime(Match match, string prefix, out DateTime dateTime)
    {
        dateTime = DateTime.MinValue;

        var monthGroup = match.Groups[$"{prefix}Month"];
        var dayGroup = match.Groups[$"{prefix}Day"];
        var yearGroup = match.Groups[$"{prefix}Year"];

        if (!monthGroup.Success || !dayGroup.Success || !yearGroup.Success)
        {
            monthGroup = match.Groups["startMonth"];
            dayGroup = match.Groups["startDay"];
            yearGroup = match.Groups["startYear"];
        }

        if (!TryGetMonth(monthGroup.Value, out var month) ||
            !int.TryParse(dayGroup.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day) ||
            !int.TryParse(yearGroup.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(match.Groups[$"{prefix}Hour"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(match.Groups[$"{prefix}Minute"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minute))
        {
            return false;
        }

        var period = match.Groups[$"{prefix}Period"].Value.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(period))
        {
            hour %= 12;
            if (period.StartsWith("p", StringComparison.OrdinalIgnoreCase))
                hour += 12;
        }

        if (month < 1 || month > 12)
            return false;

        var maxDay = DateTime.DaysInMonth(year, month);
        if (day < 1 || day > maxDay || hour < 0 || hour > 23 || minute < 0 || minute > 59)
            return false;

        dateTime = new DateTime(year, month, day, hour, minute, 0);
        return true;
    }

    private static bool TryGetMonth(string text, out int month)
    {
        month = text.Trim().TrimEnd('.').ToLowerInvariant()[..3] switch
        {
            "jan" => 1,
            "feb" => 2,
            "mar" => 3,
            "apr" => 4,
            "may" => 5,
            "jun" => 6,
            "jul" => 7,
            "aug" => 8,
            "sep" => 9,
            "oct" => 10,
            "nov" => 11,
            "dec" => 12,
            _ => 0
        };

        return month > 0;
    }

    private static bool TryGetLodestoneTimeZone(string zoneText, out TimeZoneInfo timeZone)
    {
        switch (zoneText.ToUpperInvariant())
        {
            case "PST":
            case "PDT":
                return TryFindTimeZone("Pacific Standard Time", "America/Los_Angeles", out timeZone);

            case "EST":
            case "EDT":
                return TryFindTimeZone("Eastern Standard Time", "America/New_York", out timeZone);

            case "MST":
            case "MDT":
                return TryFindTimeZone("Mountain Standard Time", "America/Denver", out timeZone);

            case "UTC":
            case "GMT":
                timeZone = TimeZoneInfo.Utc;
                return true;

            case "BST":
                timeZone = TimeZoneInfo.CreateCustomTimeZone("BST", TimeSpan.FromHours(1), "BST", "BST");
                return true;

            case "JST":
                timeZone = TimeZoneInfo.CreateCustomTimeZone("JST", TimeSpan.FromHours(9), "JST", "JST");
                return true;

            case "ACST":
                timeZone = TimeZoneInfo.CreateCustomTimeZone("ACST", TimeSpan.FromHours(9.5), "ACST", "ACST");
                return true;

            case "AEST":
                timeZone = TimeZoneInfo.CreateCustomTimeZone("AEST", TimeSpan.FromHours(10), "AEST", "AEST");
                return true;

            case "AEDT":
                timeZone = TimeZoneInfo.CreateCustomTimeZone("AEDT", TimeSpan.FromHours(11), "AEDT", "AEDT");
                return true;

            case "CET":
                timeZone = TimeZoneInfo.CreateCustomTimeZone("CET", TimeSpan.FromHours(1), "CET", "CET");
                return true;

            case "CEST":
                timeZone = TimeZoneInfo.CreateCustomTimeZone("CEST", TimeSpan.FromHours(2), "CEST", "CEST");
                return true;

            default:
                timeZone = TimeZoneInfo.Utc;
                return false;
        }
    }

    private static bool TryFindTimeZone(string windowsId, string ianaId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            return true;
        }
        catch
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
                return true;
            }
            catch
            {
                timeZone = TimeZoneInfo.Utc;
                return false;
            }
        }
    }
}
