using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace ESTClock;

public enum ClockTimeZone
{
    EST = 0,
    Pacific = 1,
    Universal = 2,
    BST = 5,
    JST = 6,
    MST = 7,
    ACST = 8
}

public enum ClockTimeFormat
{
    TwelveHour = 0,
    TwentyFourHour = 1
}

public enum ColonAnimationMode
{
    Blink = 0,
    AlwaysVisible = 1,
    Hidden = 2,
    SlowBlink = 3,
    FastBlink = 4
}

public enum ClockDisplayStyle
{
    Classic = 0,
    Minimal = 1,
    StrongShadow = 2,
    SoftGlass = 3,
    RetroPanel = 4
}

public enum ClockLayoutMode
{
    Horizontal = 0,
    Vertical = 1
}

public enum ClockPreset
{
    Classic = 0,
    Minimal = 1,
    GoldHud = 2,
    RetroPanel = 3
}

public enum LocalTimePlacement
{
    InsideMainPanel = 0,
    OutsideAboveMainPanel = 1
}

[Serializable]
public sealed class ClockProfile
{
    public string Name = "Default";
    public bool ShowBorder = true;
    public bool ShowIcon = true;
    public bool ShowShadowText = true;
    public bool ShowIconBorder = true;

    public float ClockTextScale = 2.0f;

    public Vector4 ClockTextColor = new(1, 1, 1, 1);
    public Vector4 ClockShadowColor = new(0, 0, 0, 0.8f);

    public Vector4 IconTextColor = new(0, 0, 0, 1);
    public Vector4 IconBackgroundColor = new(0.90f, 0.86f, 0.80f, 0.96f);
    public Vector4 IconBorderColor = new(0.98f, 0.96f, 0.92f, 1.0f);
    public float IconBorderOpacity = 1.0f;

    public float ClockBackgroundOpacity = 0.82f;
    public Vector4 ClockBackgroundColor = new(0.29f, 0.17f, 0.12f, 1.0f);

    public Vector4 BorderColor = new(0.47f, 0.31f, 0.22f, 0.95f);
    public float BorderOpacity = 0.95f;

    public ClockDisplayStyle DisplayStyle = ClockDisplayStyle.Classic;
    public ClockLayoutMode LayoutMode = ClockLayoutMode.Horizontal;

    public bool ShowLocalTime = false;
    public ClockTimeFormat LocalTimeFormat = ClockTimeFormat.TwelveHour;
    public LocalTimePlacement LocalTimePlacement = LocalTimePlacement.InsideMainPanel;
    public ClockDisplayStyle LocalTimeDisplayStyle = ClockDisplayStyle.Classic;
    public bool LocalTimeShowBorder = false;
    public bool LocalTimeShowShadowText = true;
    public bool LocalTimeShowIcon = false;
    public bool LocalTimeShowIconBorder = true;
    public float LocalTimeTextScale = 1.2f;
    public Vector4 LocalTimeTextColor = new(1, 1, 1, 1);
    public Vector4 LocalTimeShadowColor = new(0, 0, 0, 0.8f);
    public Vector4 LocalTimeBackgroundColor = new(0.29f, 0.17f, 0.12f, 1.0f);
    public float LocalTimeBackgroundOpacity = 0.45f;
    public Vector4 LocalTimeBorderColor = new(0.47f, 0.31f, 0.22f, 0.95f);
    public Vector4 LocalTimeIconTextColor = new(0, 0, 0, 1);
    public Vector4 LocalTimeIconBackgroundColor = new(0.90f, 0.86f, 0.80f, 0.96f);
    public Vector4 LocalTimeIconBorderColor = new(0.98f, 0.96f, 0.92f, 1.0f);
    public float LocalTimeBorderOpacity = 0.95f;
    public float LocalTimeIconBorderOpacity = 1.0f;
    public float LocalTimeVerticalOffset = 0.0f;
    public float LocalTimeHorizontalOffset = 0.0f;

    public ClockProfile Clone()
    {
        return new ClockProfile
        {
            Name = Name,
            ShowBorder = ShowBorder,
            ShowIcon = ShowIcon,
            ShowShadowText = ShowShadowText,
            ShowIconBorder = ShowIconBorder,
            ClockTextScale = ClockTextScale,
            ClockTextColor = ClockTextColor,
            ClockShadowColor = ClockShadowColor,
            IconTextColor = IconTextColor,
            IconBackgroundColor = IconBackgroundColor,
            IconBorderColor = IconBorderColor,
            IconBorderOpacity = IconBorderOpacity,
            ClockBackgroundOpacity = ClockBackgroundOpacity,
            ClockBackgroundColor = ClockBackgroundColor,
            BorderColor = BorderColor,
            BorderOpacity = BorderOpacity,
            DisplayStyle = DisplayStyle,
            LayoutMode = LayoutMode,
            ShowLocalTime = ShowLocalTime,
            LocalTimeFormat = LocalTimeFormat,
            LocalTimePlacement = LocalTimePlacement,
            LocalTimeDisplayStyle = LocalTimeDisplayStyle,
            LocalTimeShowBorder = LocalTimeShowBorder,
            LocalTimeShowShadowText = LocalTimeShowShadowText,
            LocalTimeShowIcon = LocalTimeShowIcon,
            LocalTimeShowIconBorder = LocalTimeShowIconBorder,
            LocalTimeTextScale = LocalTimeTextScale,
            LocalTimeTextColor = LocalTimeTextColor,
            LocalTimeShadowColor = LocalTimeShadowColor,
            LocalTimeBackgroundColor = LocalTimeBackgroundColor,
            LocalTimeBackgroundOpacity = LocalTimeBackgroundOpacity,
            LocalTimeBorderColor = LocalTimeBorderColor,
            LocalTimeIconTextColor = LocalTimeIconTextColor,
            LocalTimeIconBackgroundColor = LocalTimeIconBackgroundColor,
            LocalTimeIconBorderColor = LocalTimeIconBorderColor,
            LocalTimeBorderOpacity = LocalTimeBorderOpacity,
            LocalTimeIconBorderOpacity = LocalTimeIconBorderOpacity,
            LocalTimeVerticalOffset = LocalTimeVerticalOffset,
            LocalTimeHorizontalOffset = LocalTimeHorizontalOffset
        };
    }
}

[Serializable]
public sealed class AlarmEntry
{
    public Guid Id = Guid.NewGuid();
    public string DateTimeText = "";
    public string Message = "";
    public ClockTimeZone TimeZone = ClockTimeZone.EST;
    public bool Enabled = true;
    public bool HasTriggered = false;

    public string BuildListLine(ClockTimeFormat displayFormat)
    {
        if (!TimeZoneHelper.TryParseInZone(DateTimeText, TimeZone, out var utc))
            return $"Invalid alarm - {TimeZone.ToShortText()}";

        var local = TimeZoneHelper.ConvertFromUtc(utc, TimeZone);
        var dateText = local.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var suffix = local.ToString("tt", CultureInfo.InvariantCulture)
            .ToLowerInvariant()
            .Replace("am", "a.m.")
            .Replace("pm", "p.m.");

        string timeText = displayFormat == ClockTimeFormat.TwentyFourHour
            ? $"{local:HH:mm} {suffix}"
            : $"{local:hh:mm} {suffix}";

        var messageText = string.IsNullOrWhiteSpace(Message) ? "Alarm" : Message.Trim();
        return $"{dateText} - {TimeZone.ToShortText()} - {timeText} | {messageText}";
    }

    public string BuildTriggerMessage(ClockTimeFormat displayFormat)
    {
        if (!TimeZoneHelper.TryParseInZone(DateTimeText, TimeZone, out var utc))
            return "✓ (ERR) --:-- → Invalid alarm";

        var local = TimeZoneHelper.ConvertFromUtc(utc, TimeZone);
        var suffix = local.ToString("tt", CultureInfo.InvariantCulture)
            .ToLowerInvariant()
            .Replace("am", "a.m.")
            .Replace("pm", "p.m.");

        string timeText = displayFormat == ClockTimeFormat.TwentyFourHour
            ? $"{local:HH:mm} {suffix}"
            : $"{local:hh:mm} {suffix}";

        var custom = string.IsNullOrWhiteSpace(Message) ? "Alarm" : Message.Trim();
        return $"✓ ({TimeZone.ToShortText()}) {timeText} → {custom}";
    }
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 14;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public bool IsConfigWindowMovable = true;
    public bool AutoStart = false;
    public bool HideDuringCutscenes = false;

    public ClockTimeZone SelectedTimeZone = ClockTimeZone.EST;
    public ClockTimeFormat TimeFormat = ClockTimeFormat.TwelveHour;
    public ColonAnimationMode ColonAnimation = ColonAnimationMode.Blink;

    public List<ClockProfile> Profiles = new();
    public int ActiveProfileIndex = 0;

    public int AlarmEditorDay = 1;
    public int AlarmEditorHour = 1;
    public int AlarmEditorMinute = 0;
    public bool AlarmEditorIsPm = false;
    public string AlarmEditorMessage = "";
    public string AlarmEditorLastLocalDateText = "";

    public List<AlarmEntry> Alarms = new();

    public bool MaintenanceReminderEnabled = false;
    public bool MaintenanceRemind24Hours = true;
    public bool MaintenanceRemind1Hour = true;
    public bool MaintenanceRemind15Minutes = true;
    public string LastDetectedMaintenanceMessage = "";
    public string DetectedMaintenanceDateTimeText = "";
    public bool HasDetectedMaintenanceTime = false;
    public DateTime LastMaintenanceDetectionTimestampUtc = DateTime.MinValue;

    public ClockPreset PreviewPresetSelection = ClockPreset.Classic;

    // legacy fields
    public bool CustomAlarmEnabled = false;
    public bool CustomAlarmKeepAfterTrigger = false;
    public bool CustomAlarmSoundEnabled = true;
    public int CustomAlarmSoundEffect = 1;
    public string CustomAlarmDateTimeText = "";
    public string CustomAlarmMessage = "Custom reminder";
    public int CustomAlarmDay = 1;
    public int CustomAlarmHour = 1;
    public int CustomAlarmMinute = 0;

    public bool ClockTransparent = true;
    public float ClockTextScale = 2.0f;
    public bool ShowBorder = true;
    public bool ShowIcon = true;
    public bool ShowShadowText = true;
    public bool ShowIconBorder = true;
    public Vector4 ClockTextColor = new(1, 1, 1, 1);
    public Vector4 ClockShadowColor = new(0, 0, 0, 0.8f);
    public Vector4 IconTextColor = new(0, 0, 0, 1);
    public Vector4 IconBackgroundColor = new(0.90f, 0.86f, 0.80f, 0.96f);
    public Vector4 IconBorderColor = new(0.98f, 0.96f, 0.92f, 1.0f);
    public float IconBorderOpacity = 1.0f;
    public float ClockBackgroundOpacity = 0.82f;
    public Vector4 ClockBackgroundColor = new(0.29f, 0.17f, 0.12f, 1.0f);
    public Vector4 BorderColor = new(0.47f, 0.31f, 0.22f, 0.95f);
    public float BorderOpacity = 0.95f;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void EnsureInitialized()
    {
        bool needsLocalTimeMigration = Version < 14;
        SanitizeTimeZone();

        if (Profiles == null)
            Profiles = new List<ClockProfile>();

        if (Profiles.Count == 0)
        {
            Profiles.Add(new ClockProfile
            {
                Name = "Default",
                ShowBorder = ShowBorder,
                ShowIcon = ShowIcon,
                ShowShadowText = ShowShadowText,
                ShowIconBorder = ShowIconBorder,
                ClockTextScale = ClockTextScale,
                ClockTextColor = ClockTextColor,
                ClockShadowColor = ClockShadowColor,
                IconTextColor = IconTextColor,
                IconBackgroundColor = IconBackgroundColor,
                IconBorderColor = IconBorderColor,
                IconBorderOpacity = IconBorderOpacity,
                ClockBackgroundOpacity = ClockBackgroundOpacity,
                ClockBackgroundColor = ClockBackgroundColor,
                BorderColor = BorderColor,
                BorderOpacity = BorderOpacity,
                DisplayStyle = ClockDisplayStyle.Classic,
                LayoutMode = ClockLayoutMode.Horizontal
            });
        }

        if (Alarms == null)
            Alarms = new List<AlarmEntry>();

        ActiveProfileIndex = Math.Clamp(ActiveProfileIndex, 0, Profiles.Count - 1);

        foreach (var profile in Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Name))
                profile.Name = "Profile";

            if (string.Equals(profile.Name, "Retro", StringComparison.OrdinalIgnoreCase))
                profile.Name = "Retro Panel";

            if (needsLocalTimeMigration)
                EnsureLocalTimeDefaults(profile);
        }

        Version = 14;

        if (!string.IsNullOrWhiteSpace(CustomAlarmDateTimeText))
        {
            bool alreadyMigrated = Alarms.Exists(a =>
                a.DateTimeText == CustomAlarmDateTimeText &&
                a.Message == (CustomAlarmMessage ?? ""));

            if (!alreadyMigrated)
            {
                Alarms.Add(new AlarmEntry
                {
                    DateTimeText = CustomAlarmDateTimeText,
                    Message = string.IsNullOrWhiteSpace(CustomAlarmMessage) ? "Alarm" : CustomAlarmMessage,
                    TimeZone = SelectedTimeZone,
                    Enabled = CustomAlarmEnabled,
                    HasTriggered = false
                });
            }

            CustomAlarmDateTimeText = "";
        }

        RefreshAlarmEditorDateForLocalDay(SelectedTimeZone);

        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, SelectedTimeZone);
        AlarmEditorDay = Math.Clamp(
            AlarmEditorDay <= 0 ? zoneNow.Day : AlarmEditorDay,
            1,
            DateTime.DaysInMonth(zoneNow.Year, zoneNow.Month));

        if (TimeFormat == ClockTimeFormat.TwelveHour)
        {
            if (AlarmEditorHour <= 0 || AlarmEditorHour > 12)
            {
                AlarmEditorIsPm = zoneNow.Hour >= 12;
                var hour12 = zoneNow.Hour % 12;
                AlarmEditorHour = hour12 == 0 ? 12 : hour12;
            }
        }
        else
        {
            AlarmEditorHour = Math.Clamp(AlarmEditorHour, 0, 23);
        }

        AlarmEditorMinute = Math.Clamp(AlarmEditorMinute, 0, 59);
    }

    private void SanitizeTimeZone()
    {
        var rawValue = (int)SelectedTimeZone;

        // Compatibilidade com versões antigas:
        // 0 = EST
        // 1 = PST/Pacific
        // 2 = UTC/Universal
        // 3/4 eram valores antigos usados em builds anteriores para UTC/GMT
        SelectedTimeZone = rawValue switch
        {
            0 => ClockTimeZone.EST,
            1 => ClockTimeZone.Pacific,
            2 => ClockTimeZone.Universal,
            3 => ClockTimeZone.Universal,
            4 => ClockTimeZone.Universal,
            5 => ClockTimeZone.BST,
            6 => ClockTimeZone.JST,
            7 => ClockTimeZone.MST,
            8 => ClockTimeZone.ACST,
            _ => ClockTimeZone.EST
        };
    }

    private void EnsureLocalTimeDefaults(ClockProfile profile)
    {
        if (profile.LocalTimeTextScale <= 0f)
        {
            profile.LocalTimeTextScale = Math.Max(0.8f, profile.ClockTextScale * 0.6f);
            profile.LocalTimeFormat = TimeFormat;
            profile.LocalTimePlacement = LocalTimePlacement.InsideMainPanel;
            profile.LocalTimeDisplayStyle = profile.DisplayStyle;
            profile.LocalTimeShowBorder = false;
            profile.LocalTimeShowShadowText = profile.ShowShadowText;
            profile.LocalTimeShowIcon = false;
            profile.LocalTimeShowIconBorder = profile.ShowIconBorder;
        }

        if (profile.LocalTimeTextColor.W <= 0f)
            profile.LocalTimeTextColor = profile.ClockTextColor;

        if (profile.LocalTimeShadowColor.W <= 0f)
            profile.LocalTimeShadowColor = profile.ClockShadowColor;

        if (profile.LocalTimeBackgroundColor.W <= 0f)
            profile.LocalTimeBackgroundColor = profile.ClockBackgroundColor;

        if (profile.LocalTimeBorderColor.W <= 0f)
            profile.LocalTimeBorderColor = profile.BorderColor;

        if (profile.LocalTimeBackgroundOpacity <= 0f)
            profile.LocalTimeBackgroundOpacity = Math.Clamp(profile.ClockBackgroundOpacity * 0.55f, 0.15f, 1.0f);

        if (profile.LocalTimeBorderOpacity <= 0f)
            profile.LocalTimeBorderOpacity = profile.BorderOpacity;

        if (profile.LocalTimeIconTextColor.W <= 0f)
            profile.LocalTimeIconTextColor = profile.IconTextColor;

        if (profile.LocalTimeIconBackgroundColor.W <= 0f)
            profile.LocalTimeIconBackgroundColor = profile.IconBackgroundColor;

        if (profile.LocalTimeIconBorderColor.W <= 0f)
            profile.LocalTimeIconBorderColor = profile.IconBorderColor;

        if (profile.LocalTimeIconBorderOpacity <= 0f)
            profile.LocalTimeIconBorderOpacity = profile.IconBorderOpacity;
    }

    public ClockProfile GetActiveProfile()
    {
        EnsureInitialized();
        return Profiles[Math.Clamp(ActiveProfileIndex, 0, Profiles.Count - 1)];
    }

    public void AddProfile(string name)
    {
        EnsureInitialized();

        var clone = GetActiveProfile().Clone();
        clone.Name = string.IsNullOrWhiteSpace(name) ? $"Profile {Profiles.Count + 1}" : name.Trim();
        Profiles.Add(clone);
        ActiveProfileIndex = Profiles.Count - 1;
    }

    public void DeleteActiveProfile()
    {
        EnsureInitialized();

        if (Profiles.Count <= 1)
            return;

        Profiles.RemoveAt(ActiveProfileIndex);
        ActiveProfileIndex = Math.Clamp(ActiveProfileIndex, 0, Profiles.Count - 1);
    }

    public void ApplyPresetToActiveProfile(ClockPreset preset)
    {
        var profile = GetActiveProfile();
        var replacement = CreatePresetProfile(profile.Name, preset);

        profile.ShowBorder = replacement.ShowBorder;
        profile.ShowIcon = replacement.ShowIcon;
        profile.ShowShadowText = replacement.ShowShadowText;
        profile.ShowIconBorder = replacement.ShowIconBorder;
        profile.ClockTextScale = replacement.ClockTextScale;
        profile.ClockTextColor = replacement.ClockTextColor;
        profile.ClockShadowColor = replacement.ClockShadowColor;
        profile.IconTextColor = replacement.IconTextColor;
        profile.IconBackgroundColor = replacement.IconBackgroundColor;
        profile.IconBorderColor = replacement.IconBorderColor;
        profile.IconBorderOpacity = replacement.IconBorderOpacity;
        profile.ClockBackgroundOpacity = replacement.ClockBackgroundOpacity;
        profile.ClockBackgroundColor = replacement.ClockBackgroundColor;
        profile.BorderColor = replacement.BorderColor;
        profile.BorderOpacity = replacement.BorderOpacity;
        profile.DisplayStyle = replacement.DisplayStyle;
        profile.LayoutMode = replacement.LayoutMode;
    }

    public void RefreshAlarmEditorDateForLocalDay(ClockTimeZone editorTimeZone)
    {
        var localDateText = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (string.Equals(AlarmEditorLastLocalDateText, localDateText, StringComparison.Ordinal))
            return;

        AlarmEditorLastLocalDateText = localDateText;

        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, editorTimeZone);
        AlarmEditorDay = Math.Clamp(
            zoneNow.Day,
            1,
            DateTime.DaysInMonth(zoneNow.Year, zoneNow.Month));
    }

    public void AddAlarmFromEditor(ClockTimeZone alarmTimeZone)
    {
        var dateTimeText = BuildAlarmEditorDateTimeText(alarmTimeZone);
        if (string.IsNullOrWhiteSpace(dateTimeText))
            return;

        Alarms.Add(new AlarmEntry
        {
            DateTimeText = dateTimeText,
            Message = string.IsNullOrWhiteSpace(AlarmEditorMessage) ? "Alarm" : AlarmEditorMessage.Trim(),
            TimeZone = alarmTimeZone,
            Enabled = true,
            HasTriggered = false
        });
    }

    public bool UpdateAlarmFromEditor(Guid alarmId, ClockTimeZone alarmTimeZone)
    {
        var alarm = Alarms.FirstOrDefault(a => a.Id == alarmId);
        if (alarm == null)
            return false;

        var dateTimeText = BuildAlarmEditorDateTimeText(alarmTimeZone);
        if (string.IsNullOrWhiteSpace(dateTimeText))
            return false;

        alarm.DateTimeText = dateTimeText;
        alarm.Message = string.IsNullOrWhiteSpace(AlarmEditorMessage) ? "Alarm" : AlarmEditorMessage.Trim();
        alarm.TimeZone = alarmTimeZone;
        alarm.Enabled = true;
        alarm.HasTriggered = false;
        return true;
    }

    public bool ReenableAlarmForToday(Guid alarmId)
    {
        var alarm = Alarms.FirstOrDefault(a => a.Id == alarmId);
        if (alarm == null)
            return false;

        if (!TimeZoneHelper.TryParseInZone(alarm.DateTimeText, alarm.TimeZone, out var alarmUtc))
            return false;

        var alarmLocal = TimeZoneHelper.ConvertFromUtc(alarmUtc, alarm.TimeZone);
        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, alarm.TimeZone);
        var todayAlarm = new DateTime(zoneNow.Year, zoneNow.Month, zoneNow.Day, alarmLocal.Hour, alarmLocal.Minute, 0);

        alarm.DateTimeText = todayAlarm.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        alarm.Enabled = true;
        alarm.HasTriggered = false;
        return true;
    }

    public string BuildAlarmEditorDateTimeText(ClockTimeZone editorTimeZone)
    {
        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, editorTimeZone);
        var year = zoneNow.Year;
        var month = zoneNow.Month;
        var maxDay = DateTime.DaysInMonth(year, month);

        AlarmEditorDay = Math.Clamp(AlarmEditorDay, 1, maxDay);
        AlarmEditorMinute = Math.Clamp(AlarmEditorMinute, 0, 59);

        int hour24;

        if (TimeFormat == ClockTimeFormat.TwelveHour)
        {
            var selectedHour12 = Math.Clamp(AlarmEditorHour, 1, 12);
            hour24 = selectedHour12 % 12;
            if (AlarmEditorIsPm)
                hour24 += 12;
        }
        else
        {
            hour24 = Math.Clamp(AlarmEditorHour, 0, 23);
        }

        var dt = new DateTime(year, month, AlarmEditorDay, hour24, AlarmEditorMinute, 0);
        return dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    public string BuildAlarmEditorDateTimeText()
    {
        return BuildAlarmEditorDateTimeText(SelectedTimeZone);
    }

    public void RemoveAlarm(Guid alarmId)
    {
        Alarms.RemoveAll(a => a.Id == alarmId);
    }

    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
    }

    private static ClockProfile CreatePresetProfile(string name, ClockPreset preset)
    {
        return preset switch
        {
            ClockPreset.Minimal => new ClockProfile
            {
                Name = name,
                ShowBorder = false,
                ShowIcon = false,
                ShowShadowText = false,
                ShowIconBorder = false,
                ClockTextScale = 2.0f,
                ClockTextColor = new Vector4(1f, 1f, 1f, 1f),
                ClockShadowColor = new Vector4(0f, 0f, 0f, 0f),
                IconTextColor = new Vector4(1f, 1f, 1f, 1f),
                IconBackgroundColor = new Vector4(0f, 0f, 0f, 0f),
                IconBorderColor = new Vector4(0f, 0f, 0f, 0f),
                IconBorderOpacity = 0f,
                ClockBackgroundOpacity = 0.15f,
                ClockBackgroundColor = new Vector4(0.05f, 0.05f, 0.05f, 1f),
                BorderColor = new Vector4(0f, 0f, 0f, 0f),
                BorderOpacity = 0f,
                DisplayStyle = ClockDisplayStyle.Minimal,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            ClockPreset.GoldHud => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.15f,
                ClockTextColor = new Vector4(1.00f, 0.88f, 0.52f, 1f),
                ClockShadowColor = new Vector4(0.12f, 0.06f, 0.01f, 0.95f),
                IconTextColor = new Vector4(0.17f, 0.09f, 0.02f, 1f),
                IconBackgroundColor = new Vector4(0.96f, 0.83f, 0.46f, 0.97f),
                IconBorderColor = new Vector4(1.00f, 0.93f, 0.70f, 1f),
                IconBorderOpacity = 1f,
                ClockBackgroundOpacity = 0.87f,
                ClockBackgroundColor = new Vector4(0.19f, 0.11f, 0.04f, 1f),
                BorderColor = new Vector4(0.92f, 0.72f, 0.33f, 1f),
                BorderOpacity = 1f,
                DisplayStyle = ClockDisplayStyle.StrongShadow,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            ClockPreset.RetroPanel => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.0f,
                ClockTextColor = new Vector4(0.78f, 1.00f, 0.76f, 1f),
                ClockShadowColor = new Vector4(0.04f, 0.11f, 0.04f, 1f),
                IconTextColor = new Vector4(0.05f, 0.15f, 0.05f, 1f),
                IconBackgroundColor = new Vector4(0.54f, 0.84f, 0.56f, 0.95f),
                IconBorderColor = new Vector4(0.72f, 1.00f, 0.72f, 1f),
                IconBorderOpacity = 1f,
                ClockBackgroundOpacity = 0.92f,
                ClockBackgroundColor = new Vector4(0.07f, 0.17f, 0.08f, 1f),
                BorderColor = new Vector4(0.58f, 0.94f, 0.58f, 1f),
                BorderOpacity = 1f,
                DisplayStyle = ClockDisplayStyle.RetroPanel,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            _ => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.0f,
                ClockTextColor = new Vector4(1f, 1f, 1f, 1f),
                ClockShadowColor = new Vector4(0f, 0f, 0f, 0.8f),
                IconTextColor = new Vector4(0f, 0f, 0f, 1f),
                IconBackgroundColor = new Vector4(0.90f, 0.86f, 0.80f, 0.96f),
                IconBorderColor = new Vector4(0.98f, 0.96f, 0.92f, 1.0f),
                IconBorderOpacity = 1.0f,
                ClockBackgroundOpacity = 0.82f,
                ClockBackgroundColor = new Vector4(0.29f, 0.17f, 0.12f, 1.0f),
                BorderColor = new Vector4(0.47f, 0.31f, 0.22f, 0.95f),
                BorderOpacity = 0.95f,
                DisplayStyle = ClockDisplayStyle.Classic,
                LayoutMode = ClockLayoutMode.Horizontal
            }
        };
    }
}

public static class TimeZoneHelper
{
    public static TimeZoneInfo GetTimeZone(ClockTimeZone zone)
    {
        return zone switch
        {
            ClockTimeZone.Pacific => TimeZoneInfo.CreateCustomTimeZone("PST", TimeSpan.FromHours(-7), "PST", "PST"),
            ClockTimeZone.Universal => TimeZoneInfo.Utc,
            ClockTimeZone.BST => TimeZoneInfo.CreateCustomTimeZone("BST", TimeSpan.FromHours(1), "BST", "BST"),
            ClockTimeZone.JST => TimeZoneInfo.CreateCustomTimeZone("JST", TimeSpan.FromHours(9), "JST", "JST"),
            ClockTimeZone.MST => GetMountainTimeZone(),
            ClockTimeZone.ACST => TimeZoneInfo.CreateCustomTimeZone("ACST", TimeSpan.FromHours(9.5), "ACST", "ACST"),
            _ => GetEasternTimeZone()
        };
    }

    public static string ToShortText(this ClockTimeZone zone)
    {
        return zone switch
        {
            ClockTimeZone.Pacific => "PST",
            ClockTimeZone.Universal => "UTC",
            ClockTimeZone.BST => "BST",
            ClockTimeZone.JST => "JST",
            ClockTimeZone.MST => "MST",
            ClockTimeZone.ACST => "ACST",
            _ => "EST"
        };
    }

    public static DateTime ConvertFromUtc(DateTime utc, ClockTimeZone zone)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), GetTimeZone(zone));
    }

    public static bool TryParseInZone(string input, ClockTimeZone zone, out DateTime utcTime)
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

        var tz = GetTimeZone(zone);
        var unspecified = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        utcTime = TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
        return true;
    }

    private static TimeZoneInfo GetEasternTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
    }

    private static TimeZoneInfo GetMountainTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("America/Denver"); }
    }
}