using System;
using System.Globalization;

namespace Clock;

[Serializable]
public sealed class AlarmEntry
{
    public Guid Id = Guid.NewGuid();
    public string DateTimeText = "";
    public string Message = "";

    // Legacy enum kept only so existing saved alarms can migrate to TimeZoneId
    public ClockTimeZone TimeZone = ClockTimeZone.EST;
    public string TimeZoneId = "";
    public bool Enabled = true;
    public bool HasTriggered = false;

    public string BuildListLine(ClockTimeFormat displayFormat)
    {
        var effectiveTimeZoneId = GetEffectiveTimeZoneId();
        if (!TimeZoneHelper.TryParseInZone(DateTimeText, effectiveTimeZoneId, out var utc))
            return $"Invalid alarm - {TimeZoneHelper.ToShortText(effectiveTimeZoneId)}";

        var local = TimeZoneHelper.ConvertFromUtc(utc, effectiveTimeZoneId);
        var dateText = local.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var suffix = local.ToString("tt", CultureInfo.InvariantCulture)
            .ToLowerInvariant()
            .Replace("am", "a.m.")
            .Replace("pm", "p.m.");

        string timeText = displayFormat == ClockTimeFormat.TwentyFourHour
            ? $"{local:HH:mm} {suffix}"
            : $"{local:hh:mm} {suffix}";

        var messageText = string.IsNullOrWhiteSpace(Message) ? "Alarm" : Message.Trim();
        return $"{dateText} - {TimeZoneHelper.ToShortText(effectiveTimeZoneId)} - {timeText} | {messageText}";
    }

    public string BuildTriggerMessage(ClockTimeFormat displayFormat)
    {
        var effectiveTimeZoneId = GetEffectiveTimeZoneId();
        if (!TimeZoneHelper.TryParseInZone(DateTimeText, effectiveTimeZoneId, out var utc))
            return "✓ (ERR) --:-- → Invalid alarm";

        var local = TimeZoneHelper.ConvertFromUtc(utc, effectiveTimeZoneId);
        var suffix = local.ToString("tt", CultureInfo.InvariantCulture)
            .ToLowerInvariant()
            .Replace("am", "a.m.")
            .Replace("pm", "p.m.");

        string timeText = displayFormat == ClockTimeFormat.TwentyFourHour
            ? $"{local:HH:mm} {suffix}"
            : $"{local:hh:mm} {suffix}";

        var custom = string.IsNullOrWhiteSpace(Message) ? "Alarm" : Message.Trim();
        return $"✓ ({TimeZoneHelper.ToShortText(effectiveTimeZoneId)}) {timeText} → {custom}";
    }

    public string GetEffectiveTimeZoneId()
    {
        if (string.IsNullOrWhiteSpace(TimeZoneId))
            TimeZoneId = TimeZoneHelper.ToTimeZoneId(TimeZone);

        TimeZoneId = TimeZoneHelper.NormalizeTimeZoneId(TimeZoneId);
        return TimeZoneId;
    }
}
