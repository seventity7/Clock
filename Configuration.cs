using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Clock;

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
    TwentyFourHour = 1,
    TwelveHourSeconds = 2,
    TwentyFourHourSeconds = 3,
    WeekdayTwentyFourHour = 4,
    DateTwentyFourHour = 5
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
    RetroPanel = 4,
    Digital = 5,
    Tech = 6,
    Cartoon = 7,
    Countdown = 8
}

public enum ClockLayoutMode
{
    Horizontal = 0,
    Vertical = 1
}

public enum ClockTimeTextFont
{
    Default = 0,
    Digital = 1,
    Technology = 2,
    Ka1 = 3,
    Countdown = 4
}

public enum AlarmRepeatMode
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Weekdays = 3,
    Weekends = 4
}

public enum ClockPreset
{
    Classic = 0,
    Minimal = 1,
    GoldHud = 2,
    RetroPanel = 3,
    CrystalBlue = 4,
    DalamudDark = 5,
    CleanWhite = 6,
    NeonPurple = 7,
    CasinoGold = 8,
    CompactTransparent = 9,
    RaidMinimal = 10,
    Digital = 11,
    Tech = 12,
    Cartoon = 13,
    Countdown = 14
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
    public ClockTimeTextFont TimeTextFont = ClockTimeTextFont.Default;

    public ClockDisplayStyle NextAlarmOverlayDisplayStyle = ClockDisplayStyle.Classic;
    public bool NextAlarmOverlayShowShadowText = true;
    public Vector4 NextAlarmOverlayTextColor = new(1f, 1f, 1f, 1f);
    public Vector4 NextAlarmOverlayShadowColor = new(0f, 0f, 0f, 0.8f);
    public ClockDisplayStyle MaintenanceOverlayDisplayStyle = ClockDisplayStyle.Classic;
    public bool MaintenanceOverlayShowShadowText = true;
    public Vector4 MaintenanceOverlayTextColor = new(1f, 1f, 1f, 1f);
    public Vector4 MaintenanceOverlayShadowColor = new(0f, 0f, 0f, 0.8f);

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
            TimeTextFont = TimeTextFont,
            NextAlarmOverlayDisplayStyle = NextAlarmOverlayDisplayStyle,
            NextAlarmOverlayShowShadowText = NextAlarmOverlayShowShadowText,
            NextAlarmOverlayTextColor = NextAlarmOverlayTextColor,
            NextAlarmOverlayShadowColor = NextAlarmOverlayShadowColor,
            MaintenanceOverlayDisplayStyle = MaintenanceOverlayDisplayStyle,
            MaintenanceOverlayShowShadowText = MaintenanceOverlayShowShadowText,
            MaintenanceOverlayTextColor = MaintenanceOverlayTextColor,
            MaintenanceOverlayShadowColor = MaintenanceOverlayShadowColor,
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

    public void CopyFrom(ClockProfile source)
    {
        Name = source.Name;
        ShowBorder = source.ShowBorder;
        ShowIcon = source.ShowIcon;
        ShowShadowText = source.ShowShadowText;
        ShowIconBorder = source.ShowIconBorder;
        ClockTextScale = source.ClockTextScale;
        ClockTextColor = source.ClockTextColor;
        ClockShadowColor = source.ClockShadowColor;
        IconTextColor = source.IconTextColor;
        IconBackgroundColor = source.IconBackgroundColor;
        IconBorderColor = source.IconBorderColor;
        IconBorderOpacity = source.IconBorderOpacity;
        ClockBackgroundOpacity = source.ClockBackgroundOpacity;
        ClockBackgroundColor = source.ClockBackgroundColor;
        BorderColor = source.BorderColor;
        BorderOpacity = source.BorderOpacity;
        DisplayStyle = source.DisplayStyle;
        LayoutMode = source.LayoutMode;
        TimeTextFont = source.TimeTextFont;
        NextAlarmOverlayDisplayStyle = source.NextAlarmOverlayDisplayStyle;
        NextAlarmOverlayShowShadowText = source.NextAlarmOverlayShowShadowText;
        NextAlarmOverlayTextColor = source.NextAlarmOverlayTextColor;
        NextAlarmOverlayShadowColor = source.NextAlarmOverlayShadowColor;
        MaintenanceOverlayDisplayStyle = source.MaintenanceOverlayDisplayStyle;
        MaintenanceOverlayShowShadowText = source.MaintenanceOverlayShowShadowText;
        MaintenanceOverlayTextColor = source.MaintenanceOverlayTextColor;
        MaintenanceOverlayShadowColor = source.MaintenanceOverlayShadowColor;
        ShowLocalTime = source.ShowLocalTime;
        LocalTimeFormat = source.LocalTimeFormat;
        LocalTimePlacement = source.LocalTimePlacement;
        LocalTimeDisplayStyle = source.LocalTimeDisplayStyle;
        LocalTimeShowBorder = source.LocalTimeShowBorder;
        LocalTimeShowShadowText = source.LocalTimeShowShadowText;
        LocalTimeShowIcon = source.LocalTimeShowIcon;
        LocalTimeShowIconBorder = source.LocalTimeShowIconBorder;
        LocalTimeTextScale = source.LocalTimeTextScale;
        LocalTimeTextColor = source.LocalTimeTextColor;
        LocalTimeShadowColor = source.LocalTimeShadowColor;
        LocalTimeBackgroundColor = source.LocalTimeBackgroundColor;
        LocalTimeBackgroundOpacity = source.LocalTimeBackgroundOpacity;
        LocalTimeBorderColor = source.LocalTimeBorderColor;
        LocalTimeIconTextColor = source.LocalTimeIconTextColor;
        LocalTimeIconBackgroundColor = source.LocalTimeIconBackgroundColor;
        LocalTimeIconBorderColor = source.LocalTimeIconBorderColor;
        LocalTimeBorderOpacity = source.LocalTimeBorderOpacity;
        LocalTimeIconBorderOpacity = source.LocalTimeIconBorderOpacity;
        LocalTimeVerticalOffset = source.LocalTimeVerticalOffset;
        LocalTimeHorizontalOffset = source.LocalTimeHorizontalOffset;
    }
}

[Serializable]
public partial class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 28;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public bool IsConfigWindowMovable = true;
    public bool AutoStart = false;
    public bool HideDuringCutscenes = false;
    public bool ShowCustomTimestampInChat = false;
    public bool ChatTimestampUseCustomColor = true;
    public bool ChatTimestampShowAmPm = true;
    public Vector4 ChatTimestampColor = new(0.72f, 0.42f, 1.00f, 1.00f);
    public string ChatTimestampTimeZoneId = "";
    public bool ChatTimeHoverEnabled = false;  // Chat time hover is opt-in and the first-enable warning is persisted separately so clicking "No" doesn't enable the option
    public bool ChatTimeHoverShowAlarmSetupOption = true;
    public bool ChatTimeHoverExperimentalWarningAccepted = false;
    public string ChatTimeHoverTimeZoneId = "";
    public float ChatTimeHoverTooltipDurationSeconds = 3f;
    public bool AlarmAnimationsEnabled = true;
    public int AlarmsWindowKeybind = 0;
    public VirtualKey[] AlarmsWindowHotkey = [];
    public bool OpenAlarmsOverlayOnAlarmTrigger = false;
    public bool ClockAlarmsTopButtonIntroSeen = false;

    public ClockTimeZone SelectedTimeZone = ClockTimeZone.EST;
    public string SelectedTimeZoneId = "";
    public List<string> FavoriteTimeZoneIds = new();
    public ClockTimeFormat TimeFormat = ClockTimeFormat.TwelveHour;
    public ColonAnimationMode ColonAnimation = ColonAnimationMode.Blink;
    public string UiLanguageCultureName = "en-US";

    public List<ClockProfile> Profiles = new();
    public int ActiveProfileIndex = 0;


    public ClockPreset PreviewPresetSelection = ClockPreset.Digital;


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
        bool needsOverlayProfileMigration = Version < 22;
        bool needsAlarmIntroMigration = Version < 28;
        MigrateTimeZones();

        if (Profiles == null)
            Profiles = new List<ClockProfile>();

        if (Profiles.Count == 0)
        {
            Profiles.Add(ClockProfileFactory.CreatePresetProfile("Default", ClockPreset.Digital));
            PreviewPresetSelection = ClockPreset.Digital;
        }

        if (FavoriteTimeZoneIds == null)
            FavoriteTimeZoneIds = new List<string>();

        EnsureConfigurationFeatureState();
        SanitizeChatTimestampOptions();

        ActiveProfileIndex = Math.Clamp(ActiveProfileIndex, 0, Profiles.Count - 1);

        foreach (var profile in Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Name))
                profile.Name = "Profile";

            if (string.Equals(profile.Name, "Retro", StringComparison.OrdinalIgnoreCase))
                profile.Name = "Retro Panel";

            if (needsLocalTimeMigration)
                EnsureLocalTimeDefaults(profile);

            if (needsOverlayProfileMigration)
                EnsureOverlayTextDefaults(profile);
        }

        if (AlarmsWindowHotkey == null)
            AlarmsWindowHotkey = [];

        if (AlarmsWindowHotkey.Length == 0 && AlarmsWindowKeybind != 0)
        {
            AlarmsWindowKeybind = 0;
            AlarmsWindowHotkey = [];
        }

        // Versioned reset is used only to re-show the onboarding modal once after UI changes that reviewers/users need to see.
        if (needsAlarmIntroMigration)
            ClockAlarmsTopButtonIntroSeen = false;

        if (string.IsNullOrWhiteSpace(UiLanguageCultureName))
            UiLanguageCultureName = "en-US";

        Version = 28;
    }

    private void MigrateTimeZones()
    {
        if (string.IsNullOrWhiteSpace(SelectedTimeZoneId))
            SelectedTimeZoneId = TimeZoneHelper.ToTimeZoneId(SelectedTimeZone);

        SelectedTimeZoneId = TimeZoneHelper.NormalizeTimeZoneId(SelectedTimeZoneId);

    }


    private void NormalizeFavoriteTimeZones()
    {
        var normalized = new List<string>();

        foreach (var timeZoneId in FavoriteTimeZoneIds)
        {
            var normalizedId = TimeZoneHelper.NormalizeTimeZoneId(timeZoneId);
            if (normalized.Exists(id => string.Equals(id, normalizedId, StringComparison.OrdinalIgnoreCase)))
                continue;

            normalized.Add(normalizedId);
        }

        FavoriteTimeZoneIds = normalized;
    }

    private void EnsureOverlayTextDefaults(ClockProfile profile)
    {
        profile.NextAlarmOverlayDisplayStyle = NextAlarmOverlayDisplayStyle;
        profile.NextAlarmOverlayShowShadowText = NextAlarmOverlayShowShadowText;
        profile.NextAlarmOverlayTextColor = NextAlarmOverlayTextColor;
        profile.NextAlarmOverlayShadowColor = NextAlarmOverlayShadowColor;
        profile.MaintenanceOverlayDisplayStyle = MaintenanceOverlayDisplayStyle;
        profile.MaintenanceOverlayShowShadowText = MaintenanceOverlayShowShadowText;
        profile.MaintenanceOverlayTextColor = MaintenanceOverlayTextColor;
        profile.MaintenanceOverlayShadowColor = MaintenanceOverlayShadowColor;
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

    public void SanitizeChatTimestampOptions()
    {
        ChatTimestampColor.X = Math.Clamp(ChatTimestampColor.X, 0f, 1f);
        ChatTimestampColor.Y = Math.Clamp(ChatTimestampColor.Y, 0f, 1f);
        ChatTimestampColor.Z = Math.Clamp(ChatTimestampColor.Z, 0f, 1f);
        ChatTimestampColor.W = Math.Clamp(ChatTimestampColor.W, 0f, 1f);


        if (!string.IsNullOrWhiteSpace(ChatTimestampTimeZoneId) && !TimeZoneHelper.TryResolveTimeZone(ChatTimestampTimeZoneId, out ChatTimestampTimeZoneId))
            ChatTimestampTimeZoneId = string.Empty;

        if (!string.IsNullOrWhiteSpace(ChatTimeHoverTimeZoneId) && !TimeZoneHelper.TryResolveTimeZone(ChatTimeHoverTimeZoneId, out ChatTimeHoverTimeZoneId))
            ChatTimeHoverTimeZoneId = string.Empty;

        ChatTimeHoverTooltipDurationSeconds = Math.Clamp(ChatTimeHoverTooltipDurationSeconds, 2f, 5f);
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
        var replacement = ClockProfileFactory.CreatePresetProfile(profile.Name, preset);

        profile.ShowShadowText = replacement.ShowShadowText;
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

        profile.LocalTimeTextColor = replacement.ClockTextColor;
        profile.LocalTimeShadowColor = replacement.ClockShadowColor;
        profile.LocalTimeBackgroundColor = replacement.ClockBackgroundColor;
        profile.LocalTimeBackgroundOpacity = Math.Clamp(replacement.ClockBackgroundOpacity * 0.55f, 0.10f, 1.0f);
        profile.LocalTimeBorderColor = replacement.BorderColor;
        profile.LocalTimeIconTextColor = replacement.IconTextColor;
        profile.LocalTimeIconBackgroundColor = replacement.IconBackgroundColor;
        profile.LocalTimeIconBorderColor = replacement.IconBorderColor;
        profile.LocalTimeBorderOpacity = replacement.BorderOpacity;
        profile.LocalTimeIconBorderOpacity = replacement.IconBorderOpacity;

        profile.NextAlarmOverlayDisplayStyle = replacement.DisplayStyle;
        profile.NextAlarmOverlayShowShadowText = replacement.ShowShadowText;
        profile.NextAlarmOverlayTextColor = replacement.ClockTextColor;
        profile.NextAlarmOverlayShadowColor = replacement.ClockShadowColor;
        profile.MaintenanceOverlayDisplayStyle = replacement.DisplayStyle;
        profile.MaintenanceOverlayShowShadowText = replacement.ShowShadowText;
        profile.MaintenanceOverlayTextColor = replacement.ClockTextColor;
        profile.MaintenanceOverlayShadowColor = replacement.ClockShadowColor;

        if (preset == ClockPreset.Digital)
        {
            profile.DisplayStyle = replacement.DisplayStyle;
            profile.TimeTextFont = ClockTimeTextFont.Digital;
            profile.ClockShadowColor = new Vector4(0f, 0f, 0f, 0f);
            profile.NextAlarmOverlayShadowColor = new Vector4(0f, 0f, 0f, 1f);
            profile.MaintenanceOverlayShadowColor = new Vector4(0f, 0f, 0f, 1f);
            profile.IconBackgroundColor = new Vector4(0.0118f, 0.0118f, 0.0118f, 0f);
            profile.LocalTimeShowBorder = false;
            profile.LocalTimeShadowColor = new Vector4(0f, 0f, 0f, 0.949f);
            profile.LocalTimeIconBackgroundColor = new Vector4(0.0118f, 0.0118f, 0.0118f, 0f);
        }

        else if (preset == ClockPreset.Cartoon)
        {
            profile.DisplayStyle = replacement.DisplayStyle;
            profile.TimeTextFont = ClockTimeTextFont.Ka1;
            profile.ClockTextColor = new Vector4(0.08f, 0.20f, 0.42f, 1f);
            profile.ClockShadowColor = new Vector4(1f, 1f, 1f, 0.70f);
            profile.IconTextColor = new Vector4(0.08f, 0.20f, 0.42f, 1f);
            profile.IconBackgroundColor = new Vector4(1.00f, 0.78f, 0.20f, 1f);
            profile.IconBorderColor = new Vector4(1f, 1f, 1f, 1f);
            profile.IconBorderOpacity = 1f;
            profile.ClockBackgroundOpacity = 1f;
            profile.ClockBackgroundColor = new Vector4(0.045f, 0.047f, 0.050f, 1f);
            profile.BorderColor = new Vector4(0.20f, 0.20f, 0.20f, 1f);
            profile.BorderOpacity = 1f;
            profile.LocalTimeShowBorder = false;
            profile.LocalTimeShadowColor = new Vector4(1f, 1f, 1f, 0.55f);
        }
        else if (preset == ClockPreset.Tech)
        {
            profile.DisplayStyle = replacement.DisplayStyle;
            profile.TimeTextFont = ClockTimeTextFont.Technology;
            profile.ClockTextColor = new Vector4(1.00f, 0.20f, 0.20f, 1f);
            profile.ClockShadowColor = new Vector4(0.0784f, 0.1608f, 0.1608f, 1f);
            profile.IconTextColor = new Vector4(1.00f, 0.20f, 0.20f, 1f);
            profile.IconBackgroundColor = new Vector4(0.102f, 0.180f, 0.180f, 0f);
            profile.IconBorderColor = new Vector4(0.180f, 0.290f, 0.302f, 0f);
            profile.IconBorderOpacity = 0f;
            profile.LocalTimeShowBorder = false;
        }
        else if (preset == ClockPreset.Countdown)
        {
            profile.DisplayStyle = replacement.DisplayStyle;
            profile.TimeTextFont = ClockTimeTextFont.Countdown;
            profile.ClockTextColor = new Vector4(0.96f, 0.96f, 0.94f, 1f);
            profile.ClockShadowColor = new Vector4(0f, 0f, 0f, 0.95f);
            profile.IconTextColor = new Vector4(0.96f, 0.96f, 0.94f, 1f);
            profile.IconBackgroundColor = new Vector4(0.04f, 0.04f, 0.04f, 1f);
            profile.IconBorderColor = new Vector4(0.30f, 0.30f, 0.30f, 1f);
            profile.IconBorderOpacity = 1f;
            profile.ClockBackgroundOpacity = 1f;
            profile.ClockBackgroundColor = new Vector4(0.045f, 0.047f, 0.050f, 1f);
            profile.BorderColor = new Vector4(0.537f, 0.537f, 0.537f, 1f);
            profile.BorderOpacity = 1f;
            profile.LocalTimeShowBorder = false;
            profile.LocalTimeShadowColor = new Vector4(0f, 0f, 0f, 0.95f);
        }
    }

    public void CopyPublicStateFrom(Configuration source)
    {
        Version = source.Version;

        IsConfigWindowMovable = source.IsConfigWindowMovable;
        AutoStart = source.AutoStart;
        HideDuringCutscenes = source.HideDuringCutscenes;
        ShowCustomTimestampInChat = source.ShowCustomTimestampInChat;
        ChatTimestampUseCustomColor = source.ChatTimestampUseCustomColor;
        ChatTimestampShowAmPm = source.ChatTimestampShowAmPm;
        ChatTimestampColor = source.ChatTimestampColor;
        ChatTimestampTimeZoneId = source.ChatTimestampTimeZoneId;
        ChatTimeHoverEnabled = source.ChatTimeHoverEnabled;
        ChatTimeHoverShowAlarmSetupOption = source.ChatTimeHoverShowAlarmSetupOption;
        ChatTimeHoverExperimentalWarningAccepted = source.ChatTimeHoverExperimentalWarningAccepted;
        ChatTimeHoverTimeZoneId = source.ChatTimeHoverTimeZoneId;
        ChatTimeHoverTooltipDurationSeconds = source.ChatTimeHoverTooltipDurationSeconds;
        AlarmAnimationsEnabled = source.AlarmAnimationsEnabled;
        SelectedTimeZone = source.SelectedTimeZone;
        SelectedTimeZoneId = source.SelectedTimeZoneId;
        FavoriteTimeZoneIds = source.FavoriteTimeZoneIds;
        TimeFormat = source.TimeFormat;
        ColonAnimation = source.ColonAnimation;
        UiLanguageCultureName = source.UiLanguageCultureName;
        Profiles = source.Profiles;
        ActiveProfileIndex = source.ActiveProfileIndex;
        PreviewPresetSelection = source.PreviewPresetSelection;
        ClockTransparent = source.ClockTransparent;
        ClockTextScale = source.ClockTextScale;
        ShowBorder = source.ShowBorder;
        ShowIcon = source.ShowIcon;
        ShowShadowText = source.ShowShadowText;
        ShowIconBorder = source.ShowIconBorder;
        ClockTextColor = source.ClockTextColor;
        ClockShadowColor = source.ClockShadowColor;
        IconTextColor = source.IconTextColor;
        IconBackgroundColor = source.IconBackgroundColor;
        IconBorderColor = source.IconBorderColor;
        IconBorderOpacity = source.IconBorderOpacity;
        ClockBackgroundOpacity = source.ClockBackgroundOpacity;
        ClockBackgroundColor = source.ClockBackgroundColor;
        BorderColor = source.BorderColor;
        BorderOpacity = source.BorderOpacity;
        AlarmEditorDay = source.AlarmEditorDay;
        AlarmEditorHour = source.AlarmEditorHour;
        AlarmEditorMinute = source.AlarmEditorMinute;
        AlarmEditorIsPm = source.AlarmEditorIsPm;
        AlarmEditorMessage = source.AlarmEditorMessage;
        AlarmEditorLastLocalDateText = source.AlarmEditorLastLocalDateText;
        AlarmEditorDateOverrideText = source.AlarmEditorDateOverrideText;
        AlarmSoundId = source.AlarmSoundId;
        AlarmSoundRepeats = source.AlarmSoundRepeats;
        AlarmEditorSnoozeEnabled = source.AlarmEditorSnoozeEnabled;
        AlarmEditorSnoozeMinutes = source.AlarmEditorSnoozeMinutes;
        AlarmEditorRepeatMode = source.AlarmEditorRepeatMode;
        ShowNextAlarmOnOverlay = source.ShowNextAlarmOnOverlay;
        NextAlarmOverlayTextScale = source.NextAlarmOverlayTextScale;
        NextAlarmOverlayVerticalOffset = source.NextAlarmOverlayVerticalOffset;
        NextAlarmOverlayDisplayStyle = source.NextAlarmOverlayDisplayStyle;
        NextAlarmOverlayShowShadowText = source.NextAlarmOverlayShowShadowText;
        NextAlarmOverlayTextColor = source.NextAlarmOverlayTextColor;
        NextAlarmOverlayShadowColor = source.NextAlarmOverlayShadowColor;
        ShowResetTimersOnOverlay = source.ShowResetTimersOnOverlay;
        CommandSuggestionEnabled = source.CommandSuggestionEnabled;
        Alarms = source.Alarms;
        MaintenanceReminderEnabled = source.MaintenanceReminderEnabled;
        ShowMaintenanceOnOverlay = source.ShowMaintenanceOnOverlay;
        MaintenanceOverlayTextScale = source.MaintenanceOverlayTextScale;
        MaintenanceOverlayVerticalOffset = source.MaintenanceOverlayVerticalOffset;
        MaintenanceOverlayDisplayStyle = source.MaintenanceOverlayDisplayStyle;
        MaintenanceOverlayShowShadowText = source.MaintenanceOverlayShowShadowText;
        MaintenanceOverlayTextColor = source.MaintenanceOverlayTextColor;
        MaintenanceOverlayShadowColor = source.MaintenanceOverlayShadowColor;
        MaintenanceLanguage = source.MaintenanceLanguage;
        MaintenanceRemind24Hours = source.MaintenanceRemind24Hours;
        MaintenanceRemind1Hour = source.MaintenanceRemind1Hour;
        MaintenanceRemind15Minutes = source.MaintenanceRemind15Minutes;
        LastDetectedMaintenanceMessage = source.LastDetectedMaintenanceMessage;
        DetectedMaintenanceDateTimeText = source.DetectedMaintenanceDateTimeText;
        DetectedMaintenanceTimeZoneText = source.DetectedMaintenanceTimeZoneText;
        DetectedMaintenanceStartUtc = source.DetectedMaintenanceStartUtc;
        LastMaintenanceNewsTitle = source.LastMaintenanceNewsTitle;
        LastMaintenanceNewsUrl = source.LastMaintenanceNewsUrl;
        HasDetectedMaintenanceTime = source.HasDetectedMaintenanceTime;
        LastMaintenanceDetectionTimestampUtc = source.LastMaintenanceDetectionTimestampUtc;
        LastMaintenanceCheckStatus = source.LastMaintenanceCheckStatus;
        CustomAlarmEnabled = source.CustomAlarmEnabled;
        CustomAlarmKeepAfterTrigger = source.CustomAlarmKeepAfterTrigger;
        CustomAlarmSoundEnabled = source.CustomAlarmSoundEnabled;
        CustomAlarmSoundEffect = source.CustomAlarmSoundEffect;
        CustomAlarmDateTimeText = source.CustomAlarmDateTimeText;
        CustomAlarmMessage = source.CustomAlarmMessage;
        CustomAlarmDay = source.CustomAlarmDay;
        CustomAlarmHour = source.CustomAlarmHour;
        CustomAlarmMinute = source.CustomAlarmMinute;
    }

    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
    }


}
