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

public sealed class EventInfo
{
    public EventInfo(string key, string name, string url, DateTime startUtc, DateTime endUtc)
    {
        Key = key;
        Name = name;
        Url = url;
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public string Key { get; }
    public string Name { get; }
    public string Url { get; }
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }
}

public sealed class EventService : IDisposable
{
    // I check the public news indexes first because seasonal event URLs move around more often than their page structure does.
    private static readonly string[] NewsUrls =
    {
        "https://na.finalfantasyxiv.com/lodestone/news/",
        "https://eu.finalfantasyxiv.com/lodestone/news/",
        "https://de.finalfantasyxiv.com/lodestone/news/",
        "https://fr.finalfantasyxiv.com/lodestone/news/",
        "https://jp.finalfantasyxiv.com/lodestone/news/"
    };
    private static readonly Regex SpecialLinkRegex = new(
        "href=\\\"(?<url>(?:https://(?:na|eu|de|fr|jp)\\.finalfantasyxiv\\.com)?/lodestone/special/[^\\\"]+)\\\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    private static readonly Regex TitleRegex = new(
        "<title>(?<title>.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex OgTitleRegex = new(
        "<meta\\s+property=\\\"og:title\\\"\\s+content=\\\"(?<title>[^\\\"]+)\\\"",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex DateRangeRegex = new(
        @"From\s+(?:[A-Za-z]+,\s+)?(?<startMonth>[A-Za-z]+)\s+(?<startDay>\d{1,2}),?\s+(?<startYear>\d{4})\s+at\s+(?<startHour>\d{1,2}):(?<startMinute>\d{2})\s*(?<startPeriod>a\.m\.|p\.m\.|am|pm)?\s+to\s+(?:[A-Za-z]+,\s+)?(?<endMonth>[A-Za-z]+)\s+(?<endDay>\d{1,2}),?\s+(?<endYear>\d{4})\s+at\s+(?<endHour>\d{1,2}):(?<endMinute>\d{2})\s*(?<endPeriod>a\.m\.|p\.m\.|am|pm)?\s*\((?<zone>[A-Z]{2,5})\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DateTimeAttributeRegex = new(
        "datetime=\\\"(?<value>[^\\\"]+)\\\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JsonDateRegex = new(
        "\\\"(?:startDate|endDate)\\\"\\s*:\\s*\\\"(?<value>[^\\\"]+)\\\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient httpClient = new();

    public EventService()
    {
    }

    public async Task<IReadOnlyList<EventInfo>> GetActiveEventsAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var events = new List<EventInfo>();
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var newsUrl in NewsUrls)
        {
            try
            {
                var page = await httpClient.GetStringAsync(newsUrl, cancellationToken).ConfigureAwait(false);
                foreach (var url in FindSpecialUrls(page, newsUrl).Take(12))
                    urls.Add(url);
            }
            catch
            {
                // I ignore a failed region here because another Lodestone region can still expose the same seasonal event page.
            }
        }

        foreach (var url in urls.Take(24))
        {
            EventInfo? info;
            try
            {
                var detail = await httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                info = ParseEventPage(url, detail);
            }
            catch
            {
                continue;
            }

            if (info == null)
                continue;

            if (info.StartUtc <= nowUtc && info.EndUtc > nowUtc)
                events.Add(info);
        }

        return events
            .GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(e => e.EndUtc).First())
            .OrderBy(e => e.EndUtc)
            .ToArray();
    }

    private static IEnumerable<string> FindSpecialUrls(string html, string baseUrl)
    {
        var root = new Uri(baseUrl).GetLeftPart(UriPartial.Authority);
        foreach (Match match in SpecialLinkRegex.Matches(html))
        {
            var url = WebUtility.HtmlDecode(match.Groups["url"].Value);
            if (string.IsNullOrWhiteSpace(url))
                continue;

            if (url.StartsWith("/lodestone/", StringComparison.OrdinalIgnoreCase))
                url = root + url;

            yield return url.Split('#')[0];
        }
    }

    private static EventInfo? ParseEventPage(string url, string html)
    {
        var name = ReadTitle(html);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (!TryReadEventWindow(html, out var startUtc, out var endUtc))
            return null;

        if (endUtc <= startUtc)
            return null;

        var key = NormalizeKey(url, name);
        return new EventInfo(key, name, url, startUtc, endUtc);
    }

    private static bool TryReadEventWindow(string html, out DateTime startUtc, out DateTime endUtc)
    {
        startUtc = DateTime.MinValue;
        endUtc = DateTime.MinValue;

        var structuredDates = DateTimeAttributeRegex.Matches(html)
            .Concat(JsonDateRegex.Matches(html))
            .Select(m => WebUtility.HtmlDecode(m.Groups["value"].Value))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(ParseDateValue)
            .Where(d => d > DateTime.MinValue)
            .OrderBy(d => d)
            .ToArray();

        if (structuredDates.Length >= 2)
        {
            // I prefer structured dates when the page gives them because they are less fragile than translated body text.
            startUtc = structuredDates.First();
            endUtc = structuredDates.Last();
            return true;
        }

        var text = CleanText(html);
        var dateMatch = DateRangeRegex.Match(text);
        if (!dateMatch.Success)
            return false;

        var zone = dateMatch.Groups["zone"].Value;
        var startLocal = BuildDate(dateMatch, "start");
        var endLocal = BuildDate(dateMatch, "end");
        var offset = GetZoneOffset(zone);
        startUtc = new DateTimeOffset(startLocal, offset).UtcDateTime;
        endUtc = new DateTimeOffset(endLocal, offset).UtcDateTime;
        return true;
    }

    private static DateTime ParseDateValue(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var offset))
            return offset.UtcDateTime;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);

        return DateTime.MinValue;
    }

    private static string ReadTitle(string html)
    {
        var match = OgTitleRegex.Match(html);
        if (!match.Success)
            match = TitleRegex.Match(html);

        if (!match.Success)
            return string.Empty;

        var title = WebUtility.HtmlDecode(match.Groups["title"].Value);
        title = title.Replace("| FINAL FANTASY XIV, The Lodestone", "", StringComparison.OrdinalIgnoreCase);
        title = title.Replace("FINAL FANTASY XIV, The Lodestone", "", StringComparison.OrdinalIgnoreCase);
        title = WhitespaceRegex.Replace(title, " ").Trim(' ', '-', '|');
        return ShortenName(title);
    }

    private static string ShortenName(string name)
    {
        var separators = new[] { " - ", " – ", " — ", " | ", "｜", "／" };
        foreach (var separator in separators)
        {
            var index = name.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0)
                return name[..index].Trim();
        }

        return name.Trim();
    }

    private static string CleanText(string html)
    {
        var decoded = WebUtility.HtmlDecode(html);
        decoded = HtmlTagRegex.Replace(decoded, " ");
        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }

    private static DateTime BuildDate(Match match, string prefix)
    {
        var monthName = match.Groups[$"{prefix}Month"].Value;
        var day = int.Parse(match.Groups[$"{prefix}Day"].Value, CultureInfo.InvariantCulture);
        var year = int.Parse(match.Groups[$"{prefix}Year"].Value, CultureInfo.InvariantCulture);
        var hour = int.Parse(match.Groups[$"{prefix}Hour"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups[$"{prefix}Minute"].Value, CultureInfo.InvariantCulture);
        var period = match.Groups[$"{prefix}Period"].Value;

        if (!string.IsNullOrWhiteSpace(period))
        {
            var lowered = period.Replace(".", "", StringComparison.Ordinal).ToLowerInvariant();
            if (lowered == "pm" && hour < 12)
                hour += 12;
            if (lowered == "am" && hour == 12)
                hour = 0;
        }

        var month = DateTime.ParseExact(monthName, "MMMM", CultureInfo.InvariantCulture).Month;
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    private static TimeSpan GetZoneOffset(string zone)
    {
        return zone.ToUpperInvariant() switch
        {
            "PST" => TimeSpan.FromHours(-8),
            "PDT" => TimeSpan.FromHours(-7),
            "EST" => TimeSpan.FromHours(-5),
            "EDT" => TimeSpan.FromHours(-4),
            "CST" => TimeSpan.FromHours(-6),
            "CDT" => TimeSpan.FromHours(-5),
            "MST" => TimeSpan.FromHours(-7),
            "MDT" => TimeSpan.FromHours(-6),
            "GMT" or "UTC" => TimeSpan.Zero,
            "BST" => TimeSpan.FromHours(1),
            "CET" => TimeSpan.FromHours(1),
            "CEST" => TimeSpan.FromHours(2),
            "JST" => TimeSpan.FromHours(9),
            "AEST" => TimeSpan.FromHours(10),
            "AEDT" => TimeSpan.FromHours(11),
            _ => TimeSpan.Zero
        };
    }

    private static string NormalizeKey(string url, string name)
    {
        var value = string.IsNullOrWhiteSpace(url) ? name : url;
        return Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}
