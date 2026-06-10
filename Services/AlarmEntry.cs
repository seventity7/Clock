using System;
using System.Globalization;

// Some fields are duplicated on purpose for backwards compatibility.


namespace Clock;

[Serializable]
// Represents one configured alarm and its runtime snooze/trigger bookkeeping.
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
    public DateTime SpecialTriggerUtc = DateTime.MinValue;
    public bool SnoozeCanceled = false;
    public bool SnoozeTriggered = false;

    public string BuildListLine(ClockTimeFormat displayFormat, string defaultMessage = "Alarm")
    {
        var effectiveTimeZoneId = GetEffectiveTimeZoneId();
        DateTime local;
        if (TimeZoneHelper.IsEorzeaTime(effectiveTimeZoneId) &&
            DateTime.TryParseExact(DateTimeText, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var storedEt))
        {
            local = storedEt;
        }
        else
        {
            if (!TryGetStoredTriggerUtc(out var utc))
                return $"Invalid alarm - {TimeZoneHelper.ToShortText(effectiveTimeZoneId)}";

            local = TimeZoneHelper.ConvertFromUtcForDisplay(utc, effectiveTimeZoneId);
        }

        var dateText = local.ToString("MMM dd", CultureInfo.InvariantCulture);
        var timeText = TimeFormatHelper.FormatClock(local, displayFormat);
        var messageText = string.IsNullOrWhiteSpace(Message) ? defaultMessage : Message.Trim();
        var repeatText = RepeatMode == AlarmRepeatMode.None ? string.Empty : $" [{AlarmConfigurationService.GetRepeatLabel(RepeatMode)}]";

        return $"{dateText} - {TimeZoneHelper.ToShortText(effectiveTimeZoneId)} - {timeText}{repeatText} | {messageText}";
    }

    public string BuildTriggerMessage(ClockTimeFormat displayFormat, bool isSnooze = false, string defaultMessage = "Alarm")
    {
        var effectiveTimeZoneId = GetEffectiveTimeZoneId();
        if (!TryGetStoredTriggerUtc(out var utc))
            return "✓ (ERR) --:-- → Invalid alarm";

        var triggerUtc = isSnooze && SnoozedUntilUtc > DateTime.MinValue ? SnoozedUntilUtc : utc;
        var local = TimeZoneHelper.ConvertFromUtcForDisplay(triggerUtc, effectiveTimeZoneId);
        var timeText = TimeFormatHelper.FormatClock(local, displayFormat);
        var custom = string.IsNullOrWhiteSpace(Message) ? defaultMessage : Message.Trim();
        var snoozePrefix = isSnooze ? "[SNOOZE] " : string.Empty;

        return $"{snoozePrefix}✓ ({TimeZoneHelper.ToShortText(effectiveTimeZoneId)}) {timeText} → {custom}";
    }


    public bool TryGetStoredTriggerUtc(out DateTime triggerUtc)
    {
        triggerUtc = DateTime.MinValue;
        var effectiveTimeZoneId = GetEffectiveTimeZoneId();
        if (SpecialTriggerUtc > DateTime.MinValue && TimeZoneHelper.IsEorzeaTime(effectiveTimeZoneId))
        {
            triggerUtc = DateTime.SpecifyKind(SpecialTriggerUtc, DateTimeKind.Utc);
            return true;
        }

        return TimeZoneHelper.IsEorzeaTime(effectiveTimeZoneId)
            ? SpecialTriggerUtc > DateTime.MinValue && (triggerUtc = DateTime.SpecifyKind(SpecialTriggerUtc, DateTimeKind.Utc)) > DateTime.MinValue
            : TimeZoneHelper.TryParseInZone(DateTimeText, effectiveTimeZoneId, out triggerUtc);
    }

    public string GetEffectiveTimeZoneId()
    {
        if (string.IsNullOrWhiteSpace(TimeZoneId))
            TimeZoneId = TimeZoneHelper.ToTimeZoneId(TimeZone);

        TimeZoneId = TimeZoneHelper.NormalizeTimeZoneId(TimeZoneId);
        return TimeZoneId;
    }
}
