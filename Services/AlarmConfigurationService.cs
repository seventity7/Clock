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

        configuration.AlarmSoundId = Math.Clamp(configuration.AlarmSoundId, 1, 16);

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
                HasTriggered = false
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

        if (configuration.TimeFormat == ClockTimeFormat.TwelveHour)
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
            HasTriggered = false
        });
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
        return true;
    }

    public static string BuildEditorDateTimeText(Configuration configuration, string editorTimeZoneId)
    {
        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, editorTimeZoneId);
        var year = zoneNow.Year;
        var month = zoneNow.Month;
        var maxDay = DateTime.DaysInMonth(year, month);

        configuration.AlarmEditorDay = Math.Clamp(configuration.AlarmEditorDay, 1, maxDay);
        configuration.AlarmEditorMinute = Math.Clamp(configuration.AlarmEditorMinute, 0, 59);

        int hour24;

        if (configuration.TimeFormat == ClockTimeFormat.TwelveHour)
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

    public static void Remove(Configuration configuration, Guid alarmId)
    {
        configuration.Alarms.RemoveAll(a => a.Id == alarmId);
    }
}
