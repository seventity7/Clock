using Dalamud.Configuration;
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
    RetroPanel = 4
}

public enum ClockLayoutMode
{
    Horizontal = 0,
    Vertical = 1
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
    RaidMinimal = 10
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
public partial class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 20;

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

    public ClockTimeZone SelectedTimeZone = ClockTimeZone.EST;
    public string SelectedTimeZoneId = "";
    public List<string> FavoriteTimeZoneIds = new();
    public ClockTimeFormat TimeFormat = ClockTimeFormat.TwelveHour;
    public ColonAnimationMode ColonAnimation = ColonAnimationMode.Blink;
    public string UiLanguageCultureName = "en-US";

    public List<ClockProfile> Profiles = new();
    public int ActiveProfileIndex = 0;


    public ClockPreset PreviewPresetSelection = ClockPreset.Classic;


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
        MigrateTimeZones();

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
        }

        if (string.IsNullOrWhiteSpace(UiLanguageCultureName))
            UiLanguageCultureName = "en-US";

        Version = 20;
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

        NextAlarmOverlayDisplayStyle = replacement.DisplayStyle;
        NextAlarmOverlayShowShadowText = replacement.ShowShadowText;
        NextAlarmOverlayTextColor = replacement.ClockTextColor;
        NextAlarmOverlayShadowColor = replacement.ClockShadowColor;
        MaintenanceOverlayDisplayStyle = replacement.DisplayStyle;
        MaintenanceOverlayShowShadowText = replacement.ShowShadowText;
        MaintenanceOverlayTextColor = replacement.ClockTextColor;
        MaintenanceOverlayShadowColor = replacement.ClockShadowColor;
    }

    public void CopyPublicStateFrom(Configuration source)
    {
        Version = source.Version;

        var fields = typeof(Configuration).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        foreach (var field in fields)
            field.SetValue(this, field.GetValue(source));
    }

    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
    }


}
