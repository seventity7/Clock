using System;
using System.Globalization;

namespace Clock;

[Serializable]
public sealed class AlarmEntry
{
    public Guid Id = Guid.NewGuid();
    public string DateTimeText = "";
    public string Message = "";
    public AlarmRepeatMode RepeatMode = AlarmRepeatMode.None;

    public ClockTimeZone TimeZone = ClockTimeZone.EST;
    public string TimeZoneId = "";
    public bool Enabled = true;
    public bool HasTriggered = false;
    public bool SnoozeEnabled = false;
    public int SnoozeMinutes = 5;
    public DateTime SnoozedUntilUtc = DateTime.MinValue;
    public bool SnoozeCanceled = false;
    public bool SnoozeTriggered = false;

    public string BuildListLine(ClockTimeFormat displayFormat, string defaultMessage = "Alarm")
    {
        var effectiveTimeZoneId = GetEffectiveTimeZoneId();
        if (!TimeZoneHelper.TryParseInZone(DateTimeText, effectiveTimeZoneId, out var utc))
            return $"Invalid alarm - {TimeZoneHelper.ToShortText(effectiveTimeZoneId)}";

        var local = TimeZoneHelper.ConvertFromUtc(utc, effectiveTimeZoneId);
        var dateText = local.ToString("MMM dd", CultureInfo.InvariantCulture);
        var timeText = TimeFormatHelper.FormatClock(local, displayFormat);
        var messageText = string.IsNullOrWhiteSpace(Message) ? defaultMessage : Message.Trim();
        var repeatText = RepeatMode == AlarmRepeatMode.None ? string.Empty : $" [{AlarmConfigurationService.GetRepeatLabel(RepeatMode)}]";

        return $"{dateText} - {TimeZoneHelper.ToShortText(effectiveTimeZoneId)} - {timeText}{repeatText} | {messageText}";
    }

    public string BuildTriggerMessage(ClockTimeFormat displayFormat, bool isSnooze = false, string defaultMessage = "Alarm")
    {
        var effectiveTimeZoneId = GetEffectiveTimeZoneId();
        if (!TimeZoneHelper.TryParseInZone(DateTimeText, effectiveTimeZoneId, out var utc))
            return "✓ (ERR) --:-- → Invalid alarm";

        var triggerUtc = isSnooze && SnoozedUntilUtc > DateTime.MinValue ? SnoozedUntilUtc : utc;
        var local = TimeZoneHelper.ConvertFromUtc(triggerUtc, effectiveTimeZoneId);
        var timeText = TimeFormatHelper.FormatClock(local, displayFormat);
        var custom = string.IsNullOrWhiteSpace(Message) ? defaultMessage : Message.Trim();
        var snoozePrefix = isSnooze ? "[SNOOZE] " : string.Empty;

        return $"{snoozePrefix}✓ ({TimeZoneHelper.ToShortText(effectiveTimeZoneId)}) {timeText} → {custom}";
    }

    public string GetEffectiveTimeZoneId()
    {
        if (string.IsNullOrWhiteSpace(TimeZoneId))
            TimeZoneId = TimeZoneHelper.ToTimeZoneId(TimeZone);

        TimeZoneId = TimeZoneHelper.NormalizeTimeZoneId(TimeZoneId);
        return TimeZoneId;
    }
}
