using System;
using System.Globalization;
using System.Linq;

namespace Clock;

public static class AlarmConfigurationService
{
    public static void EnsureInitialized(Configuration configuration)
    {
        if (configuration.Alarms == null)
            configuration.Alarms = new();

        configuration.AlarmSoundId = configuration.AlarmSoundId < 0 || configuration.AlarmSoundId > 16 ? 9 : configuration.AlarmSoundId;

        NormalizeAlarmTimeZones(configuration);
        MigrateLegacyCustomAlarm(configuration);
        NormalizeEditorState(configuration);
    }

    private static void NormalizeAlarmTimeZones(Configuration configuration)
    {
        foreach (var alarm in configuration.Alarms)
            alarm.GetEffectiveTimeZoneId();
    }

    private static void MigrateLegacyCustomAlarm(Configuration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.CustomAlarmDateTimeText))
            return;

        bool alreadyMigrated = configuration.Alarms.Exists(a =>
            a.DateTimeText == configuration.CustomAlarmDateTimeText &&
            a.Message == (configuration.CustomAlarmMessage ?? ""));

        if (!alreadyMigrated)
        {
            configuration.Alarms.Add(new AlarmEntry
            {
                DateTimeText = configuration.CustomAlarmDateTimeText,
                Message = string.IsNullOrWhiteSpace(configuration.CustomAlarmMessage) ? "Alarm" : configuration.CustomAlarmMessage,
                TimeZone = configuration.SelectedTimeZone,
                TimeZoneId = configuration.SelectedTimeZoneId,
                Enabled = configuration.CustomAlarmEnabled,
                HasTriggered = false,
                SnoozedUntilUtc = DateTime.MinValue,
                SnoozeCanceled = false,
                SnoozeTriggered = false
            });
        }

        configuration.CustomAlarmDateTimeText = "";
    }

    private static void NormalizeEditorState(Configuration configuration)
    {
        RefreshEditorDateForLocalDay(configuration, configuration.SelectedTimeZoneId);

        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, configuration.SelectedTimeZoneId);
        configuration.AlarmEditorDay = Math.Clamp(
            configuration.AlarmEditorDay <= 0 ? zoneNow.Day : configuration.AlarmEditorDay,
            1,
            DateTime.DaysInMonth(zoneNow.Year, zoneNow.Month));

        if (TimeFormatHelper.UsesTwelveHourClock(configuration.TimeFormat))
        {
            if (configuration.AlarmEditorHour <= 0 || configuration.AlarmEditorHour > 12)
            {
                configuration.AlarmEditorIsPm = zoneNow.Hour >= 12;
                var hour12 = zoneNow.Hour % 12;
                configuration.AlarmEditorHour = hour12 == 0 ? 12 : hour12;
            }
        }
        else
        {
            configuration.AlarmEditorHour = Math.Clamp(configuration.AlarmEditorHour, 0, 23);
        }

        configuration.AlarmEditorMinute = Math.Clamp(configuration.AlarmEditorMinute, 0, 59);
        configuration.AlarmEditorSnoozeMinutes = Math.Clamp(configuration.AlarmEditorSnoozeMinutes <= 0 ? 5 : configuration.AlarmEditorSnoozeMinutes, 1, 120);
        if (!Enum.IsDefined(configuration.AlarmEditorRepeatMode))
            configuration.AlarmEditorRepeatMode = AlarmRepeatMode.None;
    }

    public static void RefreshEditorDateForLocalDay(Configuration configuration, string editorTimeZoneId)
    {
        var localDateText = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (string.Equals(configuration.AlarmEditorLastLocalDateText, localDateText, StringComparison.Ordinal))
            return;

        configuration.AlarmEditorLastLocalDateText = localDateText;

        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, editorTimeZoneId);
        configuration.AlarmEditorDay = Math.Clamp(
            zoneNow.Day,
            1,
            DateTime.DaysInMonth(zoneNow.Year, zoneNow.Month));
    }

    public static void AddFromEditor(Configuration configuration, string alarmTimeZoneId)
    {
        var dateTimeText = BuildEditorDateTimeText(configuration, alarmTimeZoneId);
        if (string.IsNullOrWhiteSpace(dateTimeText))
            return;

        configuration.Alarms.Add(new AlarmEntry
        {
            DateTimeText = dateTimeText,
            Message = string.IsNullOrWhiteSpace(configuration.AlarmEditorMessage) ? "Alarm" : configuration.AlarmEditorMessage.Trim(),
            TimeZoneId = alarmTimeZoneId,
            Enabled = true,
            HasTriggered = false,
            SnoozedUntilUtc = DateTime.MinValue,
            SnoozeCanceled = false,
            SnoozeTriggered = false,
            SnoozeEnabled = configuration.AlarmEditorSnoozeEnabled,
            SnoozeMinutes = Math.Clamp(configuration.AlarmEditorSnoozeMinutes, 1, 120),
            RepeatMode = configuration.AlarmEditorRepeatMode
        });

        configuration.AlarmEditorDateOverrideText = string.Empty;
    }

    public static bool UpdateFromEditor(Configuration configuration, Guid alarmId, string alarmTimeZoneId)
    {
        var alarm = configuration.Alarms.FirstOrDefault(a => a.Id == alarmId);
        if (alarm == null)
            return false;

        var dateTimeText = BuildEditorDateTimeText(configuration, alarmTimeZoneId);
        if (string.IsNullOrWhiteSpace(dateTimeText))
            return false;

        alarm.DateTimeText = dateTimeText;
        alarm.Message = string.IsNullOrWhiteSpace(configuration.AlarmEditorMessage) ? "Alarm" : configuration.AlarmEditorMessage.Trim();
        alarm.TimeZoneId = alarmTimeZoneId;
        alarm.Enabled = true;
        alarm.HasTriggered = false;
        alarm.SnoozedUntilUtc = DateTime.MinValue;
        alarm.SnoozeCanceled = false;
        alarm.SnoozeTriggered = false;
        alarm.SnoozeEnabled = configuration.AlarmEditorSnoozeEnabled;
        alarm.SnoozeMinutes = Math.Clamp(configuration.AlarmEditorSnoozeMinutes, 1, 120);
        alarm.RepeatMode = configuration.AlarmEditorRepeatMode;
        return true;
    }

    public static bool ReenableForToday(Configuration configuration, Guid alarmId)
    {
        var alarm = configuration.Alarms.FirstOrDefault(a => a.Id == alarmId);
        if (alarm == null)
            return false;

        var alarmTimeZoneId = alarm.GetEffectiveTimeZoneId();
        if (!TimeZoneHelper.TryParseInZone(alarm.DateTimeText, alarmTimeZoneId, out var alarmUtc))
            return false;

        var alarmLocal = TimeZoneHelper.ConvertFromUtc(alarmUtc, alarmTimeZoneId);
        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, alarmTimeZoneId);
        var todayAlarm = new DateTime(zoneNow.Year, zoneNow.Month, zoneNow.Day, alarmLocal.Hour, alarmLocal.Minute, 0);

        alarm.DateTimeText = todayAlarm.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        alarm.Enabled = true;
        alarm.HasTriggered = false;
        alarm.SnoozedUntilUtc = DateTime.MinValue;
        alarm.SnoozeCanceled = false;
        alarm.SnoozeTriggered = false;
        return true;
    }

    public static string BuildEditorDateTimeText(Configuration configuration, string editorTimeZoneId)
    {
        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, editorTimeZoneId);
        var year = zoneNow.Year;
        var month = zoneNow.Month;
        // Chat time conversion can pre-fill an alarm for a future date, so this override keeps month/year intact while the editor still shows the normal day control.
        if (!string.IsNullOrWhiteSpace(configuration.AlarmEditorDateOverrideText) &&
            DateTime.TryParseExact(configuration.AlarmEditorDateOverrideText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOverride))
        {
            year = dateOverride.Year;
            month = dateOverride.Month;
            configuration.AlarmEditorDay = dateOverride.Day;
        }

        var maxDay = DateTime.DaysInMonth(year, month);

        configuration.AlarmEditorDay = Math.Clamp(configuration.AlarmEditorDay, 1, maxDay);
        configuration.AlarmEditorMinute = Math.Clamp(configuration.AlarmEditorMinute, 0, 59);

        int hour24;

        if (TimeFormatHelper.UsesTwelveHourClock(configuration.TimeFormat))
        {
            var selectedHour12 = Math.Clamp(configuration.AlarmEditorHour, 1, 12);
            hour24 = selectedHour12 % 12;
            if (configuration.AlarmEditorIsPm)
                hour24 += 12;
        }
        else
        {
            hour24 = Math.Clamp(configuration.AlarmEditorHour, 0, 23);
        }

        var dt = new DateTime(year, month, configuration.AlarmEditorDay, hour24, configuration.AlarmEditorMinute, 0);
        return dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    public static string BuildEditorDateTimeText(Configuration configuration)
    {
        return BuildEditorDateTimeText(configuration, configuration.SelectedTimeZoneId);
    }

    public static bool ScheduleSnooze(AlarmEntry alarm, DateTime baseUtc)
    {
        if (!alarm.SnoozeEnabled || alarm.SnoozeCanceled || alarm.SnoozeTriggered)
            return false;

        var snoozeMinutes = Math.Clamp(alarm.SnoozeMinutes <= 0 ? 5 : alarm.SnoozeMinutes, 1, 120);
        alarm.SnoozedUntilUtc = baseUtc.AddMinutes(snoozeMinutes);
        return true;
    }

    public static bool CancelSnooze(Configuration configuration, Guid alarmId)
    {
        var alarm = configuration.Alarms.FirstOrDefault(a => a.Id == alarmId);
        if (alarm == null || alarm.SnoozedUntilUtc <= DateTime.MinValue || alarm.SnoozeTriggered)
            return false;

        alarm.SnoozedUntilUtc = DateTime.MinValue;
        alarm.SnoozeCanceled = true;
        return true;
    }

    public static bool TryGetPendingTriggerUtc(AlarmEntry alarm, out DateTime triggerUtc)
    {
        triggerUtc = DateTime.MinValue;

        if (alarm.SnoozedUntilUtc > DateTime.MinValue && !alarm.SnoozeTriggered)
        {
            triggerUtc = DateTime.SpecifyKind(alarm.SnoozedUntilUtc, DateTimeKind.Utc);
            return true;
        }

        if (alarm.RepeatMode == AlarmRepeatMode.None)
            return TimeZoneHelper.TryParseInZone(alarm.DateTimeText, alarm.GetEffectiveTimeZoneId(), out triggerUtc);

        return TryGetNextRecurringUtc(alarm, DateTime.UtcNow, out triggerUtc);
    }

    public static bool TryGetNextRecurringUtc(AlarmEntry alarm, DateTime fromUtc, out DateTime triggerUtc)
    {
        triggerUtc = DateTime.MinValue;
        if (!TimeZoneHelper.TryParseInZone(alarm.DateTimeText, alarm.GetEffectiveTimeZoneId(), out var baseUtc))
            return false;

        if (alarm.RepeatMode == AlarmRepeatMode.None)
        {
            triggerUtc = baseUtc;
            return true;
        }

        var zone = TimeZoneHelper.GetTimeZone(alarm.GetEffectiveTimeZoneId());
        var baseLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(baseUtc, DateTimeKind.Utc), zone);
        var fromLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), zone);
        var candidate = new DateTime(fromLocal.Year, fromLocal.Month, fromLocal.Day, baseLocal.Hour, baseLocal.Minute, 0);

        if (candidate < baseLocal.Date.AddHours(baseLocal.Hour).AddMinutes(baseLocal.Minute))
            candidate = baseLocal.Date.AddHours(baseLocal.Hour).AddMinutes(baseLocal.Minute);

        while (!RecurringDayMatches(candidate.DayOfWeek, alarm.RepeatMode, baseLocal.DayOfWeek) || TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified), zone) < fromUtc.AddSeconds(-60))
        {
            candidate = candidate.AddDays(1);
        }

        triggerUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified), zone);
        return true;
    }

    public static bool MoveRecurringForward(AlarmEntry alarm, DateTime fromUtc)
    {
        if (alarm.RepeatMode == AlarmRepeatMode.None)
            return false;

        if (!TryGetNextRecurringUtc(alarm, fromUtc.AddSeconds(61), out var nextUtc))
            return false;

        var local = TimeZoneHelper.ConvertFromUtc(nextUtc, alarm.GetEffectiveTimeZoneId());
        alarm.DateTimeText = local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        alarm.HasTriggered = false;
        alarm.SnoozedUntilUtc = DateTime.MinValue;
        alarm.SnoozeCanceled = false;
        alarm.SnoozeTriggered = false;
        return true;
    }

    public static string GetRepeatLabel(AlarmRepeatMode mode)
    {
        return mode switch
        {
            AlarmRepeatMode.Daily => "Daily",
            AlarmRepeatMode.Weekly => "Weekly",
            AlarmRepeatMode.Weekdays => "Weekdays",
            AlarmRepeatMode.Weekends => "Weekends",
            _ => "Once"
        };
    }

    private static bool RecurringDayMatches(DayOfWeek day, AlarmRepeatMode mode, DayOfWeek firstDay)
    {
        return mode switch
        {
            AlarmRepeatMode.Daily => true,
            AlarmRepeatMode.Weekly => day == firstDay,
            AlarmRepeatMode.Weekdays => day is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
            AlarmRepeatMode.Weekends => day is DayOfWeek.Saturday or DayOfWeek.Sunday,
            _ => true
        };
    }

    public static void Remove(Configuration configuration, Guid alarmId)
    {
        configuration.Alarms.RemoveAll(a => a.Id == alarmId);
    }
}
