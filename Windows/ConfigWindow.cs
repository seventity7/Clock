using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Clock.Services;

namespace Clock.Windows;

public class ConfigWindow : Window, IDisposable
{
    private enum ConfigTabRequest
    {
        None,
        Alarms,
        Appearance,
        AppearanceBottom,
        AppearanceLocalTimeLayout
    }


    private struct AlarmWheelAnimation
    {
        public int From;
        public int To;
        public double StartedAt;
    }

    private const string HelpUrl = "https://github.com/seventity7/clock";

    private readonly Plugin plugin;
    private readonly Configuration configuration;

    private float textSizeInputValue;
    private string? editingSliderId;
    private float editingSliderValue;
    private bool focusSliderInputNextFrame;

    private ClockPreset presetSelection = ClockPreset.Classic;
    private string newProfileName = "";
    private ConfigTabRequest requestedTab = ConfigTabRequest.None;
    private Guid? editingAlarmId;
    private string? editingAlarmTimeZoneId;
    private readonly FileDialogManager fileDialogManager = new();
    private readonly Dictionary<string, List<ClockProfile>> appearanceUndoStacks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClockProfile> colorEditUndoStart = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClockProfile> sliderEditUndoStart = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AlarmWheelAnimation> alarmWheelAnimations = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> alarmPanelSelectedAlarmIds = new();
    private static readonly Vector4 GoldTextColor = new(1f, 0.82f, 0.42f, 1f);
    private const float AppearanceSliderWidth = 96f;
    private const float LocalAppearanceSliderWidth = 86f;
    private string timeZoneFilter = "";
    private string chatTimestampTimeZoneFilter = "";
    private string chatTimeHoverTimeZoneFilter = "";
    private string countryTimeZoneFilter = "";
    private CountryTimeZoneOption? selectedCountryTimeZone;
    private bool favoriteTimezonesExpanded = true;
    private string configurationPopupMessage = "";
    private bool openConfigurationPopupNextFrame;
    private string languageFilter = "";
    private bool chatAlarmSetupPending;
    private bool chatTimeHoverConfirmVisible;
    private DateTime alarmCalendarVisibleMonth = DateTime.MinValue;
    private float alarmSelectorScrollY;
    private float alarmSelectorLockedScrollY;
    private bool alarmSelectorConsumedWheel;
    private bool alarmSelectorHoveredThisFrame;
    private bool alarmSelectorRestoreScrollNextFrame;
    private bool alarmSelectorLastRectValid;
    private Vector2 alarmSelectorLastMin;
    private Vector2 alarmSelectorLastMax;
    private float alarmSelectorCapturedWheel;
    private float alarmEditorControlStartX;
    private float alarmEditorControlWidth;
    private float alarmEditorPanelWidth;
    private DateTime alarmDateWheelAnchor = DateTime.MinValue;
    private int alarmPanelPage;
    private int alarmPanelPreviousPage;
    private double alarmPanelSlideStartedAt;
    private int soundModeVisualIndex = -1;
    private int soundModeTargetIndex = -1;
    private double soundModeMoveStartedAt;
    private double topAlarmButtonPulseStartedAt = double.MinValue;
    private bool clockWindowOpenTracked;
    private bool openAlarmIntroPopupNextFrame;
    private bool alarmIntroPopupVisible;
    private bool capturingAlarmsWindowKeybind;
    private readonly List<VirtualKey> pendingAlarmsWindowKeybind = new();
    private int alarmsWindowKeybindCaptureDelayFrames;

    public ConfigWindow(Plugin plugin)
        : base("###ConfigWindow")
    {
        this.plugin = plugin;
        this.configuration = plugin.Configuration;

        Flags =
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(490, 580);
        SizeCondition = ImGuiCond.FirstUseEver;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(490, 580),
            MaximumSize = new Vector2(590, 680)
        };

        textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
        presetSelection = configuration.PreviewPresetSelection;
    }

    public void Dispose() { }

    public void OpenToAlarmsTab()
    {
        // Clear the chat-created alarm context when leaving this flow so normal alarm editing falls back to the primary timezone.
        configuration.AlarmEditorDateOverrideText = string.Empty;
        editingAlarmTimeZoneId = null;
        chatAlarmSetupPending = false;
        requestedTab = ConfigTabRequest.None;
        IsOpen = true;
    }

    public void OpenToAlarmsTabFromChat(DateTime targetLocal, string targetTimeZoneId)
    {
        editingAlarmId = null;
        // When opened from chat conversion, the alarm editor temporarily uses the conversion target timezone instead of the primary timezone.
        editingAlarmTimeZoneId = string.IsNullOrWhiteSpace(targetTimeZoneId) ? configuration.SelectedTimeZoneId : targetTimeZoneId;

        // Chat-created alarms carry a full date. Keep it as an override so the day picker does not silently snap back to the current month.
        configuration.AlarmEditorDay = targetLocal.Day;
        configuration.AlarmEditorDateOverrideText = targetLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        configuration.AlarmEditorMinute = targetLocal.Minute;
        configuration.AlarmEditorMessage = T("Set up a description here");

        if (TimeFormatHelper.UsesTwelveHourClock(configuration.TimeFormat))
        {
            configuration.AlarmEditorIsPm = targetLocal.Hour >= 12;
            var hour12 = targetLocal.Hour % 12;
            configuration.AlarmEditorHour = hour12 == 0 ? 12 : hour12;
        }
        else
        {
            configuration.AlarmEditorHour = Math.Clamp(targetLocal.Hour, 0, 23);
        }

        chatAlarmSetupPending = true;
        requestedTab = ConfigTabRequest.None;
        IsOpen = true;
        configuration.Save();
    }

    public override void PreDraw()
    {
    }

    public override void Draw()
    {
        var windowSize = ImGui.GetWindowSize();
        DrawTopButtons(windowSize);

        TrackClockWindowOpen();
        DrawClockTitleHeader();
        DrawFadeSeparator();
        ImGui.Spacing();

        DrawProfileHeader();

        if (ImGui.BeginTabBar("ClockTabs"))
        {
            if (ImGui.BeginTabItem(T("General")))
            {
                if (BeginTabContent("##ClockGeneralTabContent"))
                    DrawGeneralTab();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(T("Profiles")))
            {
                if (BeginTabContent("##ClockProfilesTabContent"))
                    DrawProfilesTab();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            var appearanceTabFlags = requestedTab is ConfigTabRequest.Appearance or ConfigTabRequest.AppearanceBottom or ConfigTabRequest.AppearanceLocalTimeLayout
                ? ImGuiTabItemFlags.SetSelected
                : ImGuiTabItemFlags.None;

            if (ImGui.BeginTabItem(T("Appearance"), appearanceTabFlags))
            {
                var appearanceJump = requestedTab;
                if (requestedTab is ConfigTabRequest.Appearance or ConfigTabRequest.AppearanceBottom or ConfigTabRequest.AppearanceLocalTimeLayout)
                    requestedTab = ConfigTabRequest.None;

                if (BeginTabContent("##ClockAppearanceTabContent"))
                    DrawAppearanceTab(appearanceJump);
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(T("Commands")))
            {
                if (BeginTabContent("##ClockCommandsTabContent"))
                    DrawCommandsTab();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(T("Plugin Config"), ImGuiTabItemFlags.Trailing))
            {
                if (BeginTabContent("##ClockPluginConfigTabContent"))
                    DrawPluginConfigTab();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        DrawConfigurationResultPopup();
        fileDialogManager.Draw();
        DrawAlarmTopButtonIntroPopup();
    }

    public override void PostDraw()
    {
    }

    private void TrackClockWindowOpen()
    {
        if (clockWindowOpenTracked)
            return;

        clockWindowOpenTracked = true;
        topAlarmButtonPulseStartedAt = ImGui.GetTime();

        // This flag is intentionally checked only when /clock transitions open, avoiding repeated modal requests while the window is already drawing.
        if (!configuration.ClockAlarmsTopButtonIntroSeen)
            openAlarmIntroPopupNextFrame = true;
    }

    private void DrawClockTitleHeader()
    {
        const string alarmIcon = "\uf34e";
        var titleColor = new Vector4(1f, 0.88f, 0.55f, 1f);
        var hoverColor = new Vector4(1f, 0.92f, 0.08f, 1f);
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var textHeight = ImGui.GetTextLineHeight();
        var iconSize = Vector2.Zero;

        using (plugin.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
            iconSize = ImGui.CalcTextSize(alarmIcon);

        var iconClickSize = new Vector2(MathF.Max(iconSize.X + 8f, textHeight + 8f), MathF.Max(textHeight, iconSize.Y));
        ImGui.SetCursorScreenPos(start);
        ImGui.InvisibleButton("##ClockOpenAlarmsTopButton", iconClickSize);
        var hovered = ImGui.IsItemHovered();
        var iconCenter = new Vector2(start.X + iconClickSize.X * 0.5f, start.Y + iconClickSize.Y * 0.5f);

        var pulseAge = (float)(ImGui.GetTime() - topAlarmButtonPulseStartedAt);
        if (pulseAge >= 0f && pulseAge <= 10f)
        {
            var wave = (MathF.Sin(pulseAge * 5.2f) + 1f) * 0.5f;
            var pulse = (MathF.Sin(pulseAge * 9.0f) + 1f) * 0.5f;
            var gold = new Vector4(1f, 0.62f + 0.25f * wave, 0.05f + 0.20f * pulse, 0.45f + 0.35f * wave);
            drawList.AddCircle(iconCenter, textHeight * (0.72f + 0.10f * pulse), ImGui.GetColorU32(gold), 32, 1.5f + pulse);
        }

        if (hovered)
            ImGui.SetTooltip(T("Open Alarms"));

        if (ImGui.IsItemClicked())
            plugin.OpenAlarmOverlay();

        using (plugin.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            var iconPos = new Vector2(
                MathF.Round(start.X + (iconClickSize.X - iconSize.X) * 0.5f),
                MathF.Round(start.Y + (iconClickSize.Y - iconSize.Y) * 0.5f));
            drawList.AddText(iconPos, ImGui.GetColorU32(hovered ? hoverColor : titleColor), alarmIcon);
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
        ImGui.TextColored(titleColor, "Clock");
        ImGui.SameLine();
        ImGui.TextDisabled(T("Advanced Settings"));
    }

    private void DrawAlarmTopButtonIntroPopup()
    {
        const string popupId = "ClockAlarmsTopButtonIntroPopup";
        const string alarmIcon = "\uf34e";

        // The intro uses ImGui's modal popup stack instead of hand-drawn overlays so it consistently blocks child windows and tab contents.
        if (openAlarmIntroPopupNextFrame)
        {
            openAlarmIntroPopupNextFrame = false;
            alarmIntroPopupVisible = true;
            ImGui.OpenPopup(popupId);
        }

        if (!alarmIntroPopupVisible)
            return;

        // Keep the bool local: closing the native modal updates popupOpen, while the persisted "seen" state is handled separately.
        var popupOpen = true;
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var popupSize = new Vector2(360f, 92f);
        ImGui.SetNextWindowSize(popupSize, ImGuiCond.Appearing);
        ImGui.SetNextWindowPos(windowPos + (windowSize - popupSize) * 0.5f, ImGuiCond.Appearing);

        if (ImGui.BeginPopupModal(popupId, ref popupOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings))
        {
            if (!configuration.ClockAlarmsTopButtonIntroSeen)
            {
                configuration.ClockAlarmsTopButtonIntroSeen = true;
                configuration.Save();
            }

            ImGui.SetCursorPos(new Vector2(18f, 34f));
            ImGui.TextUnformatted(T("To create a alarm click the button "));
            ImGui.SameLine(0f, 0f);
            using (plugin.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
                ImGui.TextColored(new Vector4(1f, 0.92f, 0.08f, 1f), alarmIcon);
            ImGui.SameLine(0f, 0f);
            ImGui.TextUnformatted(T(" in the top"));

            var label = T("Or click here");
            var buttonWidth = MathF.Max(104f, ImGui.CalcTextSize(label).X + 20f);
            ImGui.SetCursorPos(new Vector2((ImGui.GetWindowSize().X - buttonWidth) * 0.5f, 62f));
            if (ImGui.Button(label, new Vector2(buttonWidth, 24f)))
            {
                plugin.OpenAlarmOverlay();
                alarmIntroPopupVisible = false;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        if (!popupOpen)
            alarmIntroPopupVisible = false;
    }

    private bool BeginTabContent(string id, bool noMouseScroll = false)
    {
        ImGui.Spacing();
        var flags = ImGuiWindowFlags.AlwaysVerticalScrollbar;
        if (noMouseScroll)
            flags |= ImGuiWindowFlags.NoScrollWithMouse;

        return ImGui.BeginChild(id, Vector2.Zero, false, flags);
    }



    private void DrawTopButtons(Vector2 windowSize)
    {
        var savedCursor = ImGui.GetCursorPos();

        var currentLanguageName = ClockLocalizationService.GetCultureDisplayName(configuration.UiLanguageCultureName);
        var languageButtonSize = new Vector2(Math.Clamp(ImGui.CalcTextSize(currentLanguageName).X + 18f, 78f, 148f), 21f);
        var feedbackButtonSize = new Vector2(24, 21);
        var helpButtonSize = new Vector2(58, 21);
        var closeButtonSize = new Vector2(24, 21);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetCursorPos(new Vector2(windowSize.X - languageButtonSize.X - feedbackButtonSize.X - helpButtonSize.X - closeButtonSize.X - (spacing * 3f) - 8f, 4));

        // Feedback button so users dont need to hunt through /xlplugins manually.
        DrawFeedbackButton(feedbackButtonSize);

        ImGui.SameLine();

        if (ImGui.Button($"{currentLanguageName}##ClockLanguageButton", languageButtonSize))
            ImGui.OpenPopup("ClockLanguagePopup");

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(T("Select language"));

        DrawLanguagePopup();

        ImGui.SameLine();

        if (ImGui.Button(T("Help").ToUpperInvariant(), helpButtonSize))
            OpenHelpUrl();

        ImGui.SameLine();

        if (ImGui.Button("X", closeButtonSize))
        {
            // Closing the window also exits the chat-alarm setup flow so the next normal alarm edit is not stuck on the timezone from the conversion.
            chatAlarmSetupPending = false;
            editingAlarmTimeZoneId = null;
            configuration.AlarmEditorDateOverrideText = string.Empty;
            IsOpen = false;
            clockWindowOpenTracked = false;
            ImGui.SetCursorPos(savedCursor);
            return;
        }
        ImGui.SetCursorPos(savedCursor);
    }

    private void DrawFeedbackButton(Vector2 size)
    {
        // This just opens Dalamud's own installed-plugin entry.
        const string icon = "\uf7f5";
        var cursor = ImGui.GetCursorScreenPos();
        var hoveredColor = new Vector4(0.76f, 0.55f, 1f, 1f);
        var iconColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];

        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hoveredColor);
        if (ImGui.Button("##ClockFeedbackPluginInstaller", size))
            // Just open Dalamud's installed-plugin page directly.
            plugin.PluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.InstalledPlugins, "Clock");
        ImGui.PopStyleColor();

        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var iconSize = ImGui.CalcTextSize(icon);
            var iconPos = cursor + (size - iconSize) * 0.5f;
            ImGui.GetWindowDrawList().AddText(iconPos, ImGui.GetColorU32(iconColor), icon);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(T("Send Feedback/Report issues"));
    }

    private void DrawLanguagePopup()
    {
        const string popupId = "ClockLanguagePopup";
        if (!ImGui.BeginPopup(popupId, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            return;

        ImGui.SetNextItemWidth(280f);
        ImGui.InputText("##ClockLanguageFilter", ref languageFilter, 96);
        if (string.IsNullOrWhiteSpace(languageFilter) && !ImGui.IsItemActive())
        {
            var pos = ImGui.GetItemRectMin() + new Vector2(8f, 2f);
            ImGui.GetWindowDrawList().AddText(pos, ImGui.GetColorU32(ImGuiCol.TextDisabled), T("Search languages..."));
        }

        DrawFadeSeparator();

        if (ImGui.BeginChild("##ClockLanguageScrollableList", new Vector2(320f, 260f), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            foreach (var option in ClockLocalizationService.GetLanguageOptions())
            {
                if (!ClockLocalizationService.MatchesFilter(option, languageFilter))
                    continue;

                var selected = string.Equals(configuration.UiLanguageCultureName, option.CultureName, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(option.DisplayName, selected))
                {
                    configuration.UiLanguageCultureName = option.CultureName;
                    configuration.Save();
                    languageFilter = "";
                    ImGui.CloseCurrentPopup();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
        }
        ImGui.EndChild();
        ImGui.EndPopup();
    }


    private string T(string text)
    {
        return ClockLocalizationService.Translate(configuration.UiLanguageCultureName, text);
    }

    private static void OpenHelpUrl()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = HelpUrl,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private void DrawProfileHeader()
    {
        ImGui.Text(T("Active Profile"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(126f);

        if (ImGui.BeginCombo("##ActiveProfileCombo", $"{T("Profile")}: {configuration.GetActiveProfile().Name}"))
        {
            var profileIndices = Enumerable.Range(0, configuration.Profiles.Count)
                .Where(i => IsUserProfile(configuration.Profiles[i].Name))
                .ToList();

            if (profileIndices.Count == 0)
            {
                ImGui.TextDisabled(T("No user profiles"));
            }
            else
            {
                foreach (var profileIndex in profileIndices)
                {
                    bool isSelected = profileIndex == configuration.ActiveProfileIndex;
                    if (ImGui.Selectable(configuration.Profiles[profileIndex].Name, isSelected))
                    {
                        configuration.ActiveProfileIndex = profileIndex;
                        configuration.Save();
                        textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
                    }

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        DrawProfileHeaderLayoutMode(configuration.GetActiveProfile());

        ImGui.Spacing();
    }

    private static bool IsUserProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var lowered = name.Trim().ToLowerInvariant();
        return lowered is not ("default" or "minimal" or "gold hud" or "retro panel" or "retro" or "classic" or "digital" or "tech" or "cartoon");
    }

    private void DrawGeneralTab()
    {
        Section(T("Behavior"), () =>
        {
            var stick = !configuration.IsConfigWindowMovable;
            if (Checkbox(T("Stick clock"), ref stick))
            {
                configuration.IsConfigWindowMovable = !stick;
                configuration.Save();
            }
            Help(T("Locks or unlocks movement/resizing of the clock window."));

            DrawAlarmsWindowKeybindSetting();

            var alarmAnimationsEnabled = configuration.AlarmAnimationsEnabled;
            if (Checkbox(T("Animations"), ref alarmAnimationsEnabled))
            {
                configuration.AlarmAnimationsEnabled = alarmAnimationsEnabled;
                configuration.Save();
            }

            var animationsLineX = ImGui.GetCursorPosX();
            ImGui.SameLine();
            DrawAlarmAnimationsPreviewIcon();
            ImGui.SetCursorPosX(animationsLineX);
            Help(T("Alarm animations with notifications."));

            bool autoStart = configuration.AutoStart;
            if (Checkbox(T("Auto Start"), ref autoStart))
            {
                configuration.AutoStart = autoStart;
                configuration.Save();
            }
            Help(T("Automatically opens the clock after login."));

            bool hideDuringCutscenes = configuration.HideDuringCutscenes;
            if (Checkbox(T("Hide during cutscenes"), ref hideDuringCutscenes))
            {
                configuration.HideDuringCutscenes = hideDuringCutscenes;
                configuration.Save();
            }
            Help(T("Hides only the clock during cutscenes."));

            bool commandSuggestion = configuration.CommandSuggestionEnabled;
            if (Checkbox(T("Command suggestion"), ref commandSuggestion))
            {
                configuration.CommandSuggestionEnabled = commandSuggestion;
                configuration.Save();
            }
            Help(T("Shows /clock command suggestions."));

        }, null, false);

        Section(T("Time Display"), () =>
        {
            var profile = configuration.GetActiveProfile();

            DrawCompactTimezoneCombo();
            DrawCompactFormatCombo();

            DrawFavoriteTimezonesSection();
        }, DrawTimeDisplayHeaderRight);

        Section(T("Chat Timestamp"), () =>
        {
            DrawChatTimestampSettings();
        }, DrawChatTimestampHeaderRight);

        DrawMaintenanceRemindersSection();

        Section(T("Chat Time Conversion"), () =>
        {
            DrawChatTimeHoverSettings();
        }, DrawChatTimeConversionHeaderRight);
    }


    private void DrawMaintenanceRemindersSection()
    {
        Section(T("Maintenance Reminders"), () =>
        {
            bool enabled = configuration.MaintenanceReminderEnabled;
            if (Checkbox(T("Enable Maintenance Reminders"), ref enabled))
            {
                configuration.MaintenanceReminderEnabled = enabled;
                configuration.Save();
            }

            if (!configuration.MaintenanceReminderEnabled)
                return;

            var languageOptions = new[]
            {
                LodestoneMaintenanceLanguage.EnglishUs,
                LodestoneMaintenanceLanguage.EnglishUk,
                LodestoneMaintenanceLanguage.French,
                LodestoneMaintenanceLanguage.German,
                LodestoneMaintenanceLanguage.Japanese
            };
            var languageNames = languageOptions.Select(LodestoneMaintenanceService.GetLanguageName).ToArray();
            var languageIndex = Array.FindIndex(languageOptions, language => language == configuration.MaintenanceLanguage);
            if (languageIndex < 0)
                languageIndex = 0;
            ImGui.SetNextItemWidth(138f);
            if (DrawCombo("Lodestone Language", languageNames, ref languageIndex, false))
            {
                configuration.MaintenanceLanguage = languageOptions[Math.Clamp(languageIndex, 0, languageOptions.Length - 1)];
                configuration.LastMaintenanceCheckStatus = "";
                configuration.Save();
            }

            ImGui.BeginDisabled(plugin.IsMaintenanceRefreshRunning);
            if (ImGui.Button(plugin.IsMaintenanceRefreshRunning ? T("Checking...") : T("Check Now")))
            {
                if (!plugin.RequestMaintenanceRefresh(true))
                {
                    configuration.LastMaintenanceCheckStatus = "Maintenance check is already running.";
                    configuration.Save();
                }
            }
            ImGui.EndDisabled();

            if (!string.IsNullOrWhiteSpace(configuration.LastMaintenanceCheckStatus))
                ImGui.TextWrapped(T(configuration.LastMaintenanceCheckStatus));

            ImGui.Spacing();

            bool remind24 = configuration.MaintenanceRemind24Hours;
            if (Checkbox(T("24 hours before"), ref remind24))
            {
                configuration.MaintenanceRemind24Hours = remind24;
                configuration.Save();
            }

            bool remind1 = configuration.MaintenanceRemind1Hour;
            if (Checkbox(T("1 hour before"), ref remind1))
            {
                configuration.MaintenanceRemind1Hour = remind1;
                configuration.Save();
            }

            bool remind15 = configuration.MaintenanceRemind15Minutes;
            if (Checkbox(T("15 minutes before"), ref remind15))
            {
                configuration.MaintenanceRemind15Minutes = remind15;
                configuration.Save();
            }

            ImGui.Spacing();
            ImGui.TextDisabled(T("Automatic checks run while reminders or maintenance overlay are enabled."));
            ImGui.Spacing();
            ImGui.TextColored(GoldTextColor, T("Latest Lodestone maintenance:"));
            ImGui.TextWrapped(string.IsNullOrWhiteSpace(configuration.LastDetectedMaintenanceMessage)
                ? T("No Lodestone maintenance found yet.")
                : configuration.LastDetectedMaintenanceMessage);

            if (configuration.HasDetectedMaintenanceTime)
            {
                ImGui.Spacing();
                ImGui.TextColored(GoldTextColor, $"{T("Detected maintenance time:")} {configuration.DetectedMaintenanceDateTimeText} {configuration.DetectedMaintenanceTimeZoneText}");
            }

            if (configuration.LastMaintenanceDetectionTimestampUtc > DateTime.MinValue)
            {
                ImGui.Spacing();
                ImGui.TextDisabled(string.Format(T("Last check: {0}"), configuration.LastMaintenanceDetectionTimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
            }
        }, () =>
        {
            if (configuration.MaintenanceReminderEnabled)
                DrawMaintenanceOverlayTitleRight();
        });
    }

    private void DrawAlarmsWindowKeybindSetting()
    {
        if (capturingAlarmsWindowKeybind)
        {
            if (CaptureAlarmsWindowKeybind())
            {
                configuration.AlarmsWindowHotkey = NormalizeAlarmsWindowKeybind(pendingAlarmsWindowKeybind);
                capturingAlarmsWindowKeybind = false;
                pendingAlarmsWindowKeybind.Clear();
                alarmsWindowKeybindCaptureDelayFrames = 0;
                ClearPressedAlarmKeybindKeys();
                configuration.Save();
            }
        }

        var keybindText = capturingAlarmsWindowKeybind
            ? pendingAlarmsWindowKeybind.Count == 0 ? T("Press keys...") : string.Join("+", pendingAlarmsWindowKeybind.Select(Plugin.FormatVirtualKey))
            : plugin.FormatAlarmsWindowKeybind();

        var buttonWidth = Math.Clamp(ImGui.CalcTextSize(keybindText).X + 18f, 76f, 140f);
        if (ImGui.Button($"{keybindText}##AlarmsWindowKeybind", new Vector2(buttonWidth, 0f)))
            BeginAlarmsWindowKeybindCapture();

        ImGui.SameLine();
        ImGui.TextUnformatted(T("Alarms window keybind"));

        if (configuration.AlarmsWindowHotkey is { Length: > 0 })
        {
            ImGui.SameLine();
            if (ImGui.SmallButton($"{T("Clear")}##ClearAlarmsWindowKeybind"))
            {
                configuration.AlarmsWindowHotkey = [];
                capturingAlarmsWindowKeybind = false;
                pendingAlarmsWindowKeybind.Clear();
                alarmsWindowKeybindCaptureDelayFrames = 0;
                configuration.Save();
            }
        }

        Help(T("Set a shortcut for the alarms overlay."));
    }

    private void BeginAlarmsWindowKeybindCapture()
    {
        capturingAlarmsWindowKeybind = true;
        alarmsWindowKeybindCaptureDelayFrames = 1;
        pendingAlarmsWindowKeybind.Clear();
        ClearPressedAlarmKeybindKeys();
    }

    private bool CaptureAlarmsWindowKeybind()
    {
        if (!capturingAlarmsWindowKeybind)
            return false;

        if (alarmsWindowKeybindCaptureDelayFrames > 0)
        {
            ClearPressedAlarmKeybindKeys();
            alarmsWindowKeybindCaptureDelayFrames--;
            return false;
        }

        foreach (var key in plugin.KeyState.GetValidVirtualKeys())
        {
            if (!plugin.KeyState[key])
                continue;

            plugin.KeyState[key] = false;

            if (IsMouseButtonKey(key))
                continue;

            if (key == VirtualKey.ESCAPE)
            {
                capturingAlarmsWindowKeybind = false;
                pendingAlarmsWindowKeybind.Clear();
                alarmsWindowKeybindCaptureDelayFrames = 0;
                return false;
            }

            if (!pendingAlarmsWindowKeybind.Contains(key))
                pendingAlarmsWindowKeybind.Add(key);
        }

        return pendingAlarmsWindowKeybind.Any(key => !IsModifierKey(key));
    }

    private void ClearPressedAlarmKeybindKeys()
    {
        foreach (var key in plugin.KeyState.GetValidVirtualKeys())
        {
            if (plugin.KeyState[key])
                plugin.KeyState[key] = false;
        }
    }

    private VirtualKey[] NormalizeAlarmsWindowKeybind(IEnumerable<VirtualKey> keys)
    {
        var validKeys = plugin.KeyState.GetValidVirtualKeys().ToHashSet();
        return keys
            .Where(key => validKeys.Contains(key) && !IsMouseButtonKey(key))
            .Distinct()
            .OrderBy(key => (int)key)
            .ToArray();
    }

    private static bool IsMouseButtonKey(VirtualKey key)
    {
        var value = (int)key;
        return value is >= 0x01 and <= 0x06;
    }

    private static bool IsModifierKey(VirtualKey key)
    {
        var value = (int)key;
        return value is 0x10 or 0x11 or 0x12 or >= 0xA0 and <= 0xA5;
    }

    private void DrawTimeDisplayHeaderRight()
    {
        var profile = configuration.GetActiveProfile();
        var label = T("Show Local Time");
        var approxWidth = ImGui.CalcTextSize(label).X + 58f;
        var startX = ImGui.GetCursorPosX();
        var rightX = ImGui.GetContentRegionMax().X - approxWidth;
        if (rightX > startX)
            ImGui.SetCursorPosX(rightX);

        bool showLocalTime = profile.ShowLocalTime;
        if (Checkbox($"{label}##HeaderShowLocalTime", ref showLocalTime))
        {
            profile.ShowLocalTime = showLocalTime;
            configuration.Save();
        }

        ImGui.SameLine();
        DrawAppearanceShortcutIcon("ShowLocalTime", ConfigTabRequest.AppearanceLocalTimeLayout);
    }

    private void DrawAlarmAnimationsPreviewIcon()
    {
        const string icon = "";
        var disabledColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
        bool hovered;
        bool clicked;

        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var iconSize = ImGui.CalcTextSize(icon);
            var cursor = ImGui.GetCursorPos();
            var frameHeight = ImGui.GetFrameHeight();
            var drawCursor = new Vector2(cursor.X, cursor.Y + MathF.Max(0f, ((frameHeight - iconSize.Y) * 0.5f) - 1f));
            ImGui.SetCursorPos(drawCursor);

            var pos = ImGui.GetCursorScreenPos();
            clicked = ImGui.InvisibleButton("##ClockAlarmAnimationsPreview", iconSize);
            hovered = ImGui.IsItemHovered();
            ImGui.GetWindowDrawList().AddText(pos, ImGui.GetColorU32(hovered ? GoldTextColor : disabledColor), icon);
            ImGui.SetCursorPos(new Vector2(cursor.X + iconSize.X, cursor.Y + frameHeight));
        }

        if (hovered)
            ImGui.SetTooltip(T("Run preview"));

        if (clicked)
            plugin.RunAlarmAnimationPreview();
    }

    private void DrawChatTimeConversionHeaderRight()
    {
        var lightRed = new Vector4(1f, 0.45f, 0.45f, 1f);
        var red = new Vector4(1f, 0.12f, 0.12f, 1f);
        var label = T("Experimental");
        ImGui.TextColored(lightRed, label);

        if (ImGui.IsItemHovered())
        {
            // This label is intentionally visible near the section title because chat hover conversion touches live chat rendering.
            // The tooltip is here to set expectations before users enable it: it is opt-in, safe to try but may conflict with other chat plugins or have bugs.
            ImGui.GetWindowDrawList().AddText(ImGui.GetItemRectMin(), ImGui.GetColorU32(red), label);
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(T("This feature is completely experimental, it might or might not work well.\nIt will not break your game if you enable it but use it at your own risk.\nPlugin incompatibilities are expected to happen.\nPlease report any issues through the Dalamud Feedback button."));
            ImGui.EndTooltip();
        }
    }

    private void DrawChatTimestampHeaderRight()
    {
        var infoIcon = FontAwesomeIcon.Question.ToIconString();
        var startX = ImGui.GetCursorPosX();
        var contentMaxX = ImGui.GetContentRegionMax().X;
        bool hovered;

        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var iconSize = ImGui.CalcTextSize(infoIcon);
            ImGui.SetCursorPosX(Math.Max(startX, contentMaxX - iconSize.X));

            var iconMin = ImGui.GetCursorScreenPos();
            var iconMax = iconMin + iconSize;
            hovered = ImGui.IsMouseHoveringRect(iconMin, iconMax);
            var disabledColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];

            ImGui.TextColored(hovered ? GoldTextColor : disabledColor, infoIcon);
        }

        if (hovered)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(T("Recommend to turn off any similar feature from other plugins to avoid any issues."));
            ImGui.EndTooltip();
        }
    }


    private void DrawAppearanceShortcutIcon(string id, ConfigTabRequest target)
    {
        const string icon = "\uf53f";
        var disabledColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
        bool hovered;

        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var size = ImGui.CalcTextSize(icon);
            var pos = ImGui.GetCursorScreenPos();
            if (ImGui.InvisibleButton($"##ClockAppearanceShortcut{id}", size))
                requestedTab = target;

            hovered = ImGui.IsItemHovered();
            ImGui.GetWindowDrawList().AddText(pos, ImGui.GetColorU32(hovered ? GoldTextColor : disabledColor), icon);
        }

        if (hovered)
            ImGui.SetTooltip(T("Appearance Settings"));
    }


    private void DrawChatTimeHoverSettings()
    {
        bool enabled = configuration.ChatTimeHoverEnabled;
        if (Checkbox(T("Enable chat time-conversion"), ref enabled))
        {
            // This feature touches live chat rendering, so the first-enable prompt makes the opt-in explicit before enabling possible plugin interactions.
            // The experimental warning is only accepted after a "Yes".
            if (enabled && !configuration.ChatTimeHoverExperimentalWarningAccepted)
            {
                chatTimeHoverConfirmVisible = true;
                ImGui.OpenPopup($"{T("Experimental")}###ClockChatTimeHoverExperimentalConfirm");
            }
            else
            {
                configuration.ChatTimeHoverEnabled = enabled;
                if (!enabled)
                    chatTimeHoverConfirmVisible = false;

                configuration.SanitizeChatTimestampOptions();
                configuration.Save();
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!configuration.ChatTimeHoverEnabled);
        if (ImGui.Button(T("Test")))
            plugin.PrintChatTimeHoverTestMessage();
        ImGui.EndDisabled();

        Help(T("Converts chat times on hover."));

        if (!configuration.ChatTimeHoverEnabled)
        {
            DrawChatTimeHoverFirstEnablePrompt();
            return;
        }

        ImGui.Indent();

        // While the first-enable prompt is open, dependent controls stay locked so the user cannot use it.
        var hoverInputsLocked = chatTimeHoverConfirmVisible;
        if (hoverInputsLocked)
            ImGui.BeginDisabled();

        var showAlarm = configuration.ChatTimeHoverShowAlarmSetupOption;
        if (Checkbox(T("Show alarm setup option"), ref showAlarm))
        {
            configuration.ChatTimeHoverShowAlarmSetupOption = showAlarm;
            configuration.Save();
        }

        ImGui.SameLine();
        // Make the user aware that chat-created alarms use the conversion target timezone, which can differ from the normal primary timezone.
        DrawChatTimeHoverAlarmSetupInfoIcon();

        Help(T("Shows alarm setup only for future times."));

        DrawChatTimeHoverTimezoneCombo();
        Help(T("Timezone used for chat time conversions."));

        var tooltipDuration = configuration.ChatTimeHoverTooltipDurationSeconds;
        ImGui.SetNextItemWidth(180f);
        if (ImGui.SliderFloat(T("Tooltip duration"), ref tooltipDuration, 2f, 5f, "%.1fs"))
        {
            configuration.ChatTimeHoverTooltipDurationSeconds = tooltipDuration;
            configuration.SanitizeChatTimestampOptions();
            configuration.Save();
        }
        Help(T("How long the clicked conversion tooltip stays visible."));

        if (hoverInputsLocked)
            ImGui.EndDisabled();

        ImGui.Unindent();
        DrawChatTimeHoverFirstEnablePrompt();
    }

    private void DrawChatTimeHoverAlarmSetupInfoIcon()
    {
        const string icon = "\uf128";
        var hovered = false;
        var enabled = configuration.ChatTimeHoverEnabled;
        var disabledColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];

        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            ImGui.TextColored(disabledColor, icon);
            hovered = ImGui.IsItemHovered();

            if (enabled && hovered)
                ImGui.GetWindowDrawList().AddText(ImGui.GetItemRectMin(), ImGui.GetColorU32(GoldTextColor), icon);
        }

        if (enabled && hovered)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(T("When creating a alarm from the tooltip option,\nit will be set up for the timezone selected below"));
            ImGui.EndTooltip();
        }
    }

    private void DrawChatTimeHoverFirstEnablePrompt()
    {
        if (!chatTimeHoverConfirmVisible)
            return;

        // Center the modal over this config window; using a real modal keeps the section layout stable underneath.
        var center = ImGui.GetWindowPos() + ImGui.GetWindowSize() * 0.5f;
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(410f, 0f), ImGuiCond.Appearing);

        if (!ImGui.BeginPopupModal($"{T("Experimental")}###ClockChatTimeHoverExperimentalConfirm", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.OpenPopup($"{T("Experimental")}###ClockChatTimeHoverExperimentalConfirm");
            return;
        }

        var text = T("This feature is completely experimental. Plugin incompatibilities and bugs are expected to happen. This alert will not appear again once activated. Do you wish to activate this option?");
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 380f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.Spacing();
        DrawFadeSeparator();
        ImGui.Spacing();

        var yesSize = new Vector2(86f, 24f);
        var noSize = new Vector2(86f, 24f);
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (width - yesSize.X - noSize.X - gap) * 0.5f));

        if (ImGui.Button(T("Yes"), yesSize))
        {
            configuration.ChatTimeHoverEnabled = true;
            configuration.ChatTimeHoverExperimentalWarningAccepted = true;
            chatTimeHoverConfirmVisible = false;
            configuration.SanitizeChatTimestampOptions();
            configuration.Save();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button(T("No"), noSize))
        {
            configuration.ChatTimeHoverEnabled = false;
            chatTimeHoverConfirmVisible = false;
            configuration.Save();
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void DrawChatTimeHoverTimezoneCombo()
    {
        ImGui.Text(T("Convert chat times to"));
        ImGui.SameLine();

        var configuredId = configuration.ChatTimeHoverTimeZoneId;
        var currentLabel = string.IsNullOrWhiteSpace(configuredId)
            ? T("Primary Timezone")
            : TimeZoneHelper.GetComboLabel(configuredId);
        var popupId = "ChatTimeHoverTimezonePopup";

        if (ImGui.Button($"{currentLabel}  ▼##ChatTimeHoverTimezoneDropdownButton", new Vector2(260f, 0f)))
            ImGui.OpenPopup(popupId);

        var buttonMin = ImGui.GetItemRectMin();
        var buttonMax = ImGui.GetItemRectMax();
        var typedTimeZoneId = string.Empty;
        var hasTypedTimeZone = !string.IsNullOrWhiteSpace(chatTimeHoverTimeZoneFilter)
            && TimeZoneHelper.TryResolveTimeZone(chatTimeHoverTimeZoneFilter, out typedTimeZoneId);

        ImGui.SetNextWindowPos(new Vector2(buttonMin.X, buttonMax.Y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(420f, hasTypedTimeZone ? 360f : 320f), ImGuiCond.Always);

        if (ImGui.BeginPopup(popupId, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputText("##ChatTimeHoverTimezoneFilter", ref chatTimeHoverTimeZoneFilter, 96);
            DrawFadeSeparator();

            if (ImGui.Selectable(T("Primary Timezone"), string.IsNullOrWhiteSpace(configuration.ChatTimeHoverTimeZoneId)))
            {
                configuration.ChatTimeHoverTimeZoneId = string.Empty;
                configuration.Save();
                chatTimeHoverTimeZoneFilter = "";
                ImGui.CloseCurrentPopup();
            }

            if (hasTypedTimeZone)
            {
                bool typedSelected = string.Equals(configuration.ChatTimeHoverTimeZoneId, typedTimeZoneId, StringComparison.OrdinalIgnoreCase);
                if (DrawTimeZoneSelectable(string.Format(CultureInfo.InvariantCulture, T("Use typed ID: {0}"), TimeZoneHelper.GetComboLabel(typedTimeZoneId)), typedSelected, typedTimeZoneId))
                {
                    configuration.ChatTimeHoverTimeZoneId = typedTimeZoneId;
                    configuration.Save();
                    chatTimeHoverTimeZoneFilter = "";
                    ImGui.CloseCurrentPopup();
                }

                DrawFadeSeparator();
            }

            if (ImGui.BeginChild("##ChatTimeHoverTimezoneScrollableList", new Vector2(0f, 244f), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                foreach (var timeZone in TimeZoneHelper.GetSystemTimeZones().OrderBy(timeZone => timeZone.BaseUtcOffset).ThenBy(timeZone => timeZone.Id, StringComparer.OrdinalIgnoreCase))
                {
                    if (!TimeZoneHelper.MatchesFilter(timeZone, chatTimeHoverTimeZoneFilter))
                        continue;

                    bool selected = string.Equals(configuration.ChatTimeHoverTimeZoneId, timeZone.Id, StringComparison.OrdinalIgnoreCase);
                    if (DrawTimeZoneSelectable(TimeZoneHelper.GetComboLabel(timeZone), selected, timeZone.Id))
                    {
                        configuration.ChatTimeHoverTimeZoneId = TimeZoneHelper.NormalizeTimeZoneId(timeZone.Id);
                        configuration.Save();
                        chatTimeHoverTimeZoneFilter = "";
                        ImGui.CloseCurrentPopup();
                    }

                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndChild();
            ImGui.EndPopup();
        }
    }

    private void DrawChatTimestampSettings()
    {
        bool showCustomTimestamp = configuration.ShowCustomTimestampInChat;
        if (Checkbox(T("Show custom timestamp"), ref showCustomTimestamp))
        {
            configuration.ShowCustomTimestampInChat = showCustomTimestamp;
            configuration.SanitizeChatTimestampOptions();
            configuration.Save();
            plugin.RefreshChatTimestampSettings();
        }

        if (!configuration.ShowCustomTimestampInChat)
            return;

        bool showAmPm = configuration.ChatTimestampShowAmPm;
        if (Checkbox(T("Show AM/PM"), ref showAmPm))
        {
            configuration.ChatTimestampShowAmPm = showAmPm;
            configuration.Save();
            plugin.RefreshChatTimestampSettings();
        }

        ImGui.Indent();

        bool useCustomColor = configuration.ChatTimestampUseCustomColor;
        if (Checkbox(T("Custom color"), ref useCustomColor))
        {
            configuration.ChatTimestampUseCustomColor = useCustomColor;
            configuration.SanitizeChatTimestampOptions();
            configuration.Save();
            plugin.RefreshChatTimestampSettings();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(46f);
        var timestampColor = configuration.ChatTimestampColor;
        if (ImGui.ColorEdit4("##ChatTimestampColor", ref timestampColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.NoTooltip))
        {
            configuration.ChatTimestampColor = timestampColor;
            configuration.ChatTimestampUseCustomColor = true;
            configuration.SanitizeChatTimestampOptions();
            configuration.Save();
            plugin.RefreshChatTimestampSettings();
        }

        DrawTimestampTimezoneCombo();
        Help(T("Enable \"Add time stamp to messages.\" in char config to work"));

        ImGui.Unindent();

    }

    private void DrawTimestampTimezoneCombo()
    {
        ImGui.Text(T("Timestamp timezone"));
        ImGui.SameLine();

        var currentLabel = string.IsNullOrWhiteSpace(configuration.ChatTimestampTimeZoneId)
            ? T("Local Time")
            : TimeZoneHelper.GetComboLabel(configuration.ChatTimestampTimeZoneId);
        var popupId = "ChatTimestampTimezonePopup";

        if (ImGui.Button($"{currentLabel}  ▼##ChatTimestampTimezoneDropdownButton", new Vector2(260f, 0f)))
            ImGui.OpenPopup(popupId);

        var buttonMin = ImGui.GetItemRectMin();
        var buttonMax = ImGui.GetItemRectMax();
        var typedTimeZoneId = string.Empty;
        var hasTypedTimeZone = !string.IsNullOrWhiteSpace(chatTimestampTimeZoneFilter)
            && TimeZoneHelper.TryResolveTimeZone(chatTimestampTimeZoneFilter, out typedTimeZoneId);

        ImGui.SetNextWindowPos(new Vector2(buttonMin.X, buttonMax.Y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(420f, hasTypedTimeZone ? 360f : 320f), ImGuiCond.Always);

        if (ImGui.BeginPopup(popupId, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputText("##ChatTimestampTimezoneFilter", ref chatTimestampTimeZoneFilter, 96);
            DrawFadeSeparator();

            bool localSelected = string.IsNullOrWhiteSpace(configuration.ChatTimestampTimeZoneId);
            if (ImGui.Selectable(T("Local Time"), localSelected))
            {
                configuration.ChatTimestampTimeZoneId = string.Empty;
                configuration.Save();
                plugin.RefreshChatTimestampSettings();
                chatTimestampTimeZoneFilter = "";
                ImGui.CloseCurrentPopup();
            }

            if (localSelected)
                ImGui.SetItemDefaultFocus();

            DrawFadeSeparator();

            if (hasTypedTimeZone)
            {
                bool typedSelected = string.Equals(configuration.ChatTimestampTimeZoneId, typedTimeZoneId, StringComparison.OrdinalIgnoreCase);
                if (DrawTimeZoneSelectable(string.Format(CultureInfo.InvariantCulture, T("Use typed ID: {0}"), TimeZoneHelper.GetComboLabel(typedTimeZoneId)), typedSelected, typedTimeZoneId))
                {
                    configuration.ChatTimestampTimeZoneId = TimeZoneHelper.NormalizeTimeZoneId(typedTimeZoneId);
                    configuration.Save();
                    plugin.RefreshChatTimestampSettings();
                    chatTimestampTimeZoneFilter = "";
                    ImGui.CloseCurrentPopup();
                }

                DrawFadeSeparator();
            }

            if (ImGui.BeginChild("##ChatTimestampTimezoneScrollableList", new Vector2(0f, 244f), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                foreach (var timeZone in GetOrderedTimeZonesForPrimaryCombo())
                {
                    if (!TimeZoneHelper.MatchesFilter(timeZone, chatTimestampTimeZoneFilter))
                        continue;

                    bool selected = string.Equals(configuration.ChatTimestampTimeZoneId, timeZone.Id, StringComparison.OrdinalIgnoreCase);
                    if (DrawTimeZoneSelectable(TimeZoneHelper.GetComboLabel(timeZone), selected, timeZone.Id))
                    {
                        configuration.ChatTimestampTimeZoneId = TimeZoneHelper.NormalizeTimeZoneId(timeZone.Id);
                        configuration.Save();
                        plugin.RefreshChatTimestampSettings();
                        chatTimestampTimeZoneFilter = "";
                        ImGui.CloseCurrentPopup();
                    }

                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndChild();
            ImGui.EndPopup();
        }
    }

    private void DrawCompactTimezoneCombo()
    {
        ImGui.Text(T("Primary Timezone"));
        ImGui.SameLine();

        var currentLabel = TimeZoneHelper.GetComboLabel(configuration.SelectedTimeZoneId);
        var popupId = "PrimaryTimezonePopup";

        if (ImGui.Button($"{currentLabel}  ▼##PrimaryTimezoneDropdownButton", new Vector2(260f, 0f)))
            ImGui.OpenPopup(popupId);

        var buttonMin = ImGui.GetItemRectMin();
        var buttonMax = ImGui.GetItemRectMax();
        var typedTimeZoneId = string.Empty;
        var hasTypedTimeZone = !string.IsNullOrWhiteSpace(timeZoneFilter)
            && TimeZoneHelper.TryResolveTimeZone(timeZoneFilter, out typedTimeZoneId);

        ImGui.SetNextWindowPos(new Vector2(buttonMin.X, buttonMax.Y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(420f, hasTypedTimeZone ? 336f : 296f), ImGuiCond.Always);

        if (ImGui.BeginPopup(popupId, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputText("##PrimaryTimezoneFilter", ref timeZoneFilter, 96);
            DrawFadeSeparator();

            if (hasTypedTimeZone)
            {
                bool typedSelected = string.Equals(configuration.SelectedTimeZoneId, typedTimeZoneId, StringComparison.OrdinalIgnoreCase);
                if (DrawTimeZoneSelectable(string.Format(CultureInfo.InvariantCulture, T("Use typed ID: {0}"), TimeZoneHelper.GetComboLabel(typedTimeZoneId)), typedSelected, typedTimeZoneId))
                {
                    configuration.SelectedTimeZoneId = typedTimeZoneId;
                    configuration.Save();
                    timeZoneFilter = "";
                    ImGui.CloseCurrentPopup();
                }

                DrawFadeSeparator();
            }

            if (ImGui.BeginChild("##PrimaryTimezoneScrollableList", new Vector2(0f, 244f), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                foreach (var timeZone in GetOrderedTimeZonesForPrimaryCombo())
                {
                    if (!TimeZoneHelper.MatchesFilter(timeZone, timeZoneFilter))
                        continue;

                    bool selected = string.Equals(configuration.SelectedTimeZoneId, timeZone.Id, StringComparison.OrdinalIgnoreCase);
                    if (DrawTimeZoneSelectable(TimeZoneHelper.GetComboLabel(timeZone), selected, timeZone.Id))
                    {
                        configuration.SelectedTimeZoneId = TimeZoneHelper.NormalizeTimeZoneId(timeZone.Id);
                        configuration.Save();
                        timeZoneFilter = "";
                        ImGui.CloseCurrentPopup();
                    }

                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndChild();
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        DrawFavoriteTimezoneButton();

        DrawCountryTimezoneSelector();
    }

    private TimeZoneInfo[] GetOrderedTimeZonesForPrimaryCombo()
    {
        var favoriteIds = configuration.FavoriteTimeZoneIds
            .Select(TimeZoneHelper.NormalizeTimeZoneId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return TimeZoneHelper.GetSystemTimeZones()
            .OrderByDescending(timeZone => favoriteIds.Contains(TimeZoneHelper.NormalizeTimeZoneId(timeZone.Id), StringComparer.OrdinalIgnoreCase))
            .ThenBy(timeZone => timeZone.BaseUtcOffset)
            .ThenBy(timeZone => timeZone.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool DrawTimeZoneSelectable(string label, bool selected, string timeZoneId)
    {
        var highlighted = TimeZoneHelper.ShouldHighlightTimeZone(timeZoneId);
        if (highlighted)
            ImGui.PushStyleColor(ImGuiCol.Text, GoldTextColor);

        var clicked = ImGui.Selectable(label, selected);

        if (highlighted && ImGui.IsItemHovered())
            ImGui.SetTooltip(T("Commonly Used"));

        if (highlighted)
            ImGui.PopStyleColor();

        return clicked;
    }

    private void DrawFavoriteTimezoneButton()
    {
        var normalizedId = TimeZoneHelper.NormalizeTimeZoneId(configuration.SelectedTimeZoneId);
        var isFavorite = configuration.FavoriteTimeZoneIds.Any(id =>
            string.Equals(TimeZoneHelper.NormalizeTimeZoneId(id), normalizedId, StringComparison.OrdinalIgnoreCase));

        if (isFavorite)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, GoldTextColor);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.36f, 0.26f, 0.08f, 0.95f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.48f, 0.34f, 0.10f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.56f, 0.40f, 0.12f, 1f));
        }

        if (ImGui.Button("★##FavoriteTimezone", new Vector2(30f, 0f)))
        {
            if (isFavorite)
            {
                configuration.FavoriteTimeZoneIds.RemoveAll(id =>
                    string.Equals(TimeZoneHelper.NormalizeTimeZoneId(id), normalizedId, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                configuration.FavoriteTimeZoneIds.Add(normalizedId);
            }

            configuration.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(isFavorite ? T("Remove from favorites") : T("Add to favorites"));

        if (isFavorite)
            ImGui.PopStyleColor(4);
    }

    private void DrawFavoriteTimezonesSection()
    {
        ImGui.Spacing();

        var arrow = favoriteTimezonesExpanded ? "" : "";
        var headerStart = ImGui.GetCursorScreenPos();
        var headerHeight = ImGui.GetTextLineHeightWithSpacing();
        if (ImGui.InvisibleButton("##FavoriteTimezonesHeader", new Vector2(ImGui.GetContentRegionAvail().X, headerHeight)))
            favoriteTimezonesExpanded = !favoriteTimezonesExpanded;

        var headerHovered = ImGui.IsItemHovered();
        var drawList = ImGui.GetWindowDrawList();
        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
            drawList.AddText(headerStart, ImGui.GetColorU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]), arrow);

        var iconWidth = ImGui.CalcTextSize(arrow).X + 16f;
        drawList.AddText(new Vector2(headerStart.X + iconWidth, headerStart.Y), ImGui.GetColorU32(headerHovered ? ImGui.GetStyle().Colors[(int)ImGuiCol.Text] : ImGui.GetStyle().Colors[(int)ImGuiCol.Text]), T("Favorite Timezones"));

        if (!favoriteTimezonesExpanded)
            return;

        ImGui.TextDisabled(T("Favorite timezones can be selected to change the current timezone"));

        if (configuration.FavoriteTimeZoneIds.Count == 0)
        {
            ImGui.TextDisabled(T("No favorite timezones added yet."));
            return;
        }

        for (var i = 0; i < configuration.FavoriteTimeZoneIds.Count; i++)
        {
            var timeZoneId = TimeZoneHelper.NormalizeTimeZoneId(configuration.FavoriteTimeZoneIds[i]);
            configuration.FavoriteTimeZoneIds[i] = timeZoneId;

            var selected = string.Equals(configuration.SelectedTimeZoneId, timeZoneId, StringComparison.OrdinalIgnoreCase);
            if (ImGui.Selectable($"{TimeZoneHelper.GetComboLabel(timeZoneId)}##FavoriteTimezone{i}", selected))
            {
                configuration.SelectedTimeZoneId = timeZoneId;
                configuration.Save();
            }
        }
    }

    private void DrawCountryTimezoneSelector()
    {
        ImGui.Spacing();
        ImGui.TextWrapped(T("Didn't find what you are looking for? Try by the country instead:"));

        var selectedCountryLabel = selectedCountryTimeZone?.Label ?? T("Select Country");
        var popupId = "CountryTimezonePopup";

        if (ImGui.Button($"{selectedCountryLabel}  ▼##CountryTimezoneDropdownButton", new Vector2(205f, 0f)))
            ImGui.OpenPopup(popupId);

        var buttonMin = ImGui.GetItemRectMin();
        var buttonMax = ImGui.GetItemRectMax();

        ImGui.SetNextWindowPos(new Vector2(buttonMin.X, buttonMax.Y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(420f, 266f), ImGuiCond.Always);

        if (ImGui.BeginPopup(popupId, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputText("##CountryTimezoneFilter", ref countryTimeZoneFilter, 96);
            DrawFadeSeparator();

            if (ImGui.BeginChild("##CountryTimezoneScrollableList", new Vector2(0f, 214f), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                foreach (var option in TimeZoneHelper.GetCountryTimeZoneOptions())
                {
                    if (!TimeZoneHelper.MatchesCountryFilter(option, countryTimeZoneFilter))
                        continue;

                    var selected = selectedCountryTimeZone != null
                        && string.Equals(selectedCountryTimeZone.CountryName, option.CountryName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(selectedCountryTimeZone.TimeZoneId, option.TimeZoneId, StringComparison.OrdinalIgnoreCase);

                    if (ImGui.Selectable(option.Label, selected))
                    {
                        selectedCountryTimeZone = option;
                        countryTimeZoneFilter = "";
                        ImGui.CloseCurrentPopup();
                    }

                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndChild();
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(selectedCountryTimeZone == null);
        if (ImGui.Button("✓##ApplyCountryTimezone", new Vector2(28f, 0f)) && selectedCountryTimeZone != null)
        {
            configuration.SelectedTimeZoneId = TimeZoneHelper.NormalizeTimeZoneId(selectedCountryTimeZone.TimeZoneId);
            configuration.Save();
            timeZoneFilter = "";
        }
        ImGui.EndDisabled();

        ImGui.TextDisabled(T("Search by country and hit \"✓\" to automaticaly find timezone."));
    }

    private void DrawProfileHeaderLayoutMode(ClockProfile profile)
    {
        var layoutNames = new[] { "Horizontal", "Vertical" };
        var layoutIndex = (int)profile.LayoutMode;
        var label = T("Layout Mode");
        const float comboWidth = 104f;

        var style = ImGui.GetStyle();
        var width = ImGui.CalcTextSize(label).X + style.ItemInnerSpacing.X + comboWidth;
        var rightX = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - width;

        ImGui.SameLine();
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), rightX));
        ImGui.Text(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(comboWidth);
        if (DrawCombo("##GeneralLayoutMode", layoutNames, ref layoutIndex))
        {
            profile.LayoutMode = (ClockLayoutMode)layoutIndex;
            configuration.Save();
        }
    }

    private void DrawCompactFormatCombo()
    {
        var formatNames = TimeFormatHelper.Names;
        var formatIndex = Math.Clamp((int)configuration.TimeFormat, 0, formatNames.Length - 1);

        ImGui.Text(T("Time Format"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(118f);

        if (ImGui.BeginCombo("##TimeFormat", T(formatNames[formatIndex])))
        {
            for (int i = 0; i < formatNames.Length; i++)
            {
                bool selected = i == formatIndex;
                if (ImGui.Selectable(T(formatNames[i]), selected))
                {
                    configuration.TimeFormat = (ClockTimeFormat)i;
                    configuration.Save();
                    plugin.RefreshChatTimestampSettings();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }

    private void DrawCompactColonCombo()
    {
        var colonNames = new[] { "Default", "Always", "Hidden", "Slow", "Fast" };
        var colonIndex = (int)configuration.ColonAnimation;

        ImGui.Text(T("Colon Animation"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(88f);

        if (ImGui.BeginCombo("##ColonAnimation", T(colonNames[Math.Clamp(colonIndex, 0, colonNames.Length - 1)])))
        {
            for (int i = 0; i < colonNames.Length; i++)
            {
                bool selected = i == colonIndex;
                if (ImGui.Selectable(T(colonNames[i]), selected))
                {
                    configuration.ColonAnimation = (ColonAnimationMode)i;
                    configuration.Save();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }



    private void DrawProfilesTab()
    {
        Section(T("Profiles"), () =>
        {
            var userProfiles = configuration.Profiles
                .Where(p => IsUserProfile(p.Name))
                .Select(p => p.Name)
                .ToArray();

            int savedProfileIndex = Array.FindIndex(userProfiles, n => n == configuration.GetActiveProfile().Name);
            if (savedProfileIndex < 0)
                savedProfileIndex = 0;

            if (userProfiles.Length > 0)
            {
                ImGui.SetNextItemWidth(130f);
                if (DrawCombo("Saved Profiles", userProfiles, ref savedProfileIndex, false))
                {
                    var chosenName = userProfiles[Math.Clamp(savedProfileIndex, 0, userProfiles.Length - 1)];
                    var realIndex = configuration.Profiles.FindIndex(p => p.Name == chosenName);
                    if (realIndex >= 0)
                    {
                        configuration.ActiveProfileIndex = realIndex;
                        configuration.Save();
                        textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
                    }
                }
            }
            else
            {
                ImGui.TextDisabled(T("Saved Profiles"));
                ImGui.SameLine();
                ImGui.TextDisabled(T("No user profiles"));
            }

            ImGui.SameLine(0f, 18f);
            var rename = configuration.GetActiveProfile().Name;
            ImGui.SetNextItemWidth(142f);
            if (ImGui.InputText(T("Rename profile"), ref rename, 64))
            {
                configuration.GetActiveProfile().Name = rename;
                configuration.Save();
            }

            if (string.IsNullOrWhiteSpace(newProfileName))
                newProfileName = $"Profile {configuration.Profiles.Count + 1}";

            ImGui.SetNextItemWidth(114f);
            ImGui.InputText(T("New Profile"), ref newProfileName, 64);

            if (ImGui.Button(T("Create From Current")))
            {
                configuration.AddProfile(newProfileName);
                configuration.Save();
                textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
            }

            ImGui.SameLine();

            if (ImGui.Button(T("Delete Active Profile")))
            {
                configuration.DeleteActiveProfile();
                configuration.Save();
                textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
            }
        });
    }

    private void DrawAppearanceTab(ConfigTabRequest scrollTarget = ConfigTabRequest.None)
    {
        var profile = configuration.GetActiveProfile();

        DrawAppearanceGroupTitle("Layout & Style", "LayoutStyle", "Undo last change", true);
        DrawMainTimeLayoutSettings(profile);

        DrawAppearanceSeparator();

        DrawAppearanceCategory("Main Time", "", "MainTime", () =>
        {
            DrawAppearanceGroupTitle("Visibility", "MainTime");
            DrawMainTimeVisibilitySettings(profile);
            DrawAppearanceSeparator();

            DrawAppearanceGroupTitle("Text");
            DrawMainTimeTextSettings(profile);
            DrawAppearanceSeparator();

            DrawAppearanceGroupTitle("Timezone Badge");
            DrawMainTimeIconSettings(profile);
        });

        if (scrollTarget == ConfigTabRequest.AppearanceLocalTimeLayout)
            ImGui.SetScrollHereY(0.25f);

        DrawAppearanceCategory("Local Time", "", "LocalTime", () =>
        {
            DrawAppearanceGroupTitle("Layout", "LocalTime");
            DrawLocalTimeLayoutSettings(profile);
            DrawAppearanceSeparator();

            DrawAppearanceGroupTitle("Text");
            DrawLocalTimeTextSettings(profile);
            DrawAppearanceSeparator();

            DrawAppearanceGroupTitle("Timezone Badge");
            DrawLocalTimeIconSettings(profile);
        });

        DrawAppearanceCategory("Extras", "", "Extras", () =>
        {
            DrawAppearanceGroupTitle("Alarm Overlay", "Extras");
            DrawOverlayTextAppearanceSettings(true);
            DrawAppearanceSeparator();

            DrawAppearanceGroupTitle("Maintenance Overlay");
            DrawOverlayTextAppearanceSettings(false);
        });

        if (scrollTarget == ConfigTabRequest.AppearanceBottom)
            ImGui.SetScrollHereY(1.0f);
    }

    private void DrawAppearanceCategory(string title, string icon, string undoKey, Action drawContent)
    {
        ImGui.Spacing();
        var headerId = title.Replace(" ", string.Empty, StringComparison.Ordinal);
        var open = ImGui.CollapsingHeader($"      {T(title)}##ClockAppearance{headerId}Group");
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();

        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize() * 0.88f;
            var iconSize = ImGui.CalcTextSize(icon) * 0.88f;
            var iconPos = new Vector2(min.X + 24f, min.Y + MathF.Floor((max.Y - min.Y - iconSize.Y) * 0.5f));
            ImGui.GetWindowDrawList().AddText(font, fontSize, iconPos, ImGui.GetColorU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]), icon);
        }

        if (!open)
            return;

        ImGui.Indent(8f);
        drawContent();
        ImGui.Unindent(8f);
    }

    private void DrawAppearanceGroupTitle(string title, string? undoKey = null, string tooltip = "Undo change", bool inlineUndo = false)
    {
        ImGui.Spacing();
        ImGui.TextColored(GoldTextColor, T(title));

        if (undoKey != null)
            DrawAppearanceUndoIconOnRow(undoKey, tooltip, ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
    }

    private void DrawAppearanceUndoIconOnRow(string undoKey, string tooltip, Vector2 rowMin, Vector2 rowMax)
    {
        const string icon = "";
        var hasUndo = appearanceUndoStacks.TryGetValue(undoKey, out var stack) && stack.Count > 0;
        var color = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
        var hoverColor = new Vector4(1f, 0.45f, 0.45f, 1f);
        var hovered = false;

        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var size = ImGui.CalcTextSize(icon);
            var right = ImGui.GetWindowPos().X + ImGui.GetContentRegionMax().X - 8f;
            var x = MathF.Max(rowMin.X, right - size.X);
            var y = rowMin.Y + MathF.Floor((rowMax.Y - rowMin.Y - size.Y) * 0.5f);
            var pos = new Vector2(x, y);
            hovered = ImGui.IsMouseHoveringRect(pos, pos + size, false);
            ImGui.GetWindowDrawList().AddText(pos, ImGui.GetColorU32(hovered && hasUndo ? hoverColor : color), icon);

            if (hovered && hasUndo && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                UndoAppearanceChange(undoKey);
        }

        if (hovered)
            ImGui.SetTooltip(T(tooltip));
    }

    private void SaveAppearanceChange(string undoKey, ClockProfile before)
    {
        if (!appearanceUndoStacks.TryGetValue(undoKey, out var stack))
        {
            stack = new List<ClockProfile>();
            appearanceUndoStacks[undoKey] = stack;
        }

        stack.Add(before.Clone());
        if (stack.Count > 50)
            stack.RemoveAt(0);

        configuration.Save();
    }

    private void UndoAppearanceChange(string undoKey)
    {
        if (!appearanceUndoStacks.TryGetValue(undoKey, out var stack) || stack.Count == 0)
            return;

        var profile = configuration.GetActiveProfile();
        var previous = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        profile.CopyFrom(previous);
        textSizeInputValue = profile.ClockTextScale;
        configuration.Save();
    }

    private void DrawMainTimeLayoutSettings(ClockProfile profile)
    {
        var undoBefore = profile.Clone();
        var styleNames = new[] { "Classic", "Minimal", "Strong Shadow", "Soft Glass", "Retro Panel", "Digital", "Tech", "Cartoon", "Countdown" };
        var styleIndex = (int)profile.DisplayStyle;
        ImGui.SetNextItemWidth(114f);
        if (DrawCombo("Display Style", styleNames, ref styleIndex))
        {
            profile.DisplayStyle = (ClockDisplayStyle)styleIndex;
            SaveAppearanceChange("LayoutStyle", undoBefore);
        }

        ImGui.SameLine();
        var presetValues = new[]
        {
            ClockPreset.Countdown,
            ClockPreset.Cartoon,
            ClockPreset.Tech,
            ClockPreset.Digital,
            ClockPreset.Classic,
            ClockPreset.Minimal,
            ClockPreset.GoldHud,
            ClockPreset.RetroPanel,
            ClockPreset.CrystalBlue,
            ClockPreset.DalamudDark,
            ClockPreset.CleanWhite,
            ClockPreset.NeonPurple,
            ClockPreset.CasinoGold,
            ClockPreset.CompactTransparent,
            ClockPreset.RaidMinimal
        };
        var presetNames = new[] { "Countdown", "Cartoon", "Tech", "Digital", "Classic", "Minimal", "Gold HUD", "Retro Panel", "Crystal Blue", "Dalamud Dark", "Clean White", "Neon Purple", "Casino Gold", "Compact Transparent", "Raid Minimal" };
        var presetIndex = Array.IndexOf(presetValues, presetSelection);
        if (presetIndex < 0)
            presetIndex = 0;
        ImGui.SetNextItemWidth(108f);
        if (DrawCombo("Preset", presetNames, ref presetIndex))
        {
            presetSelection = presetValues[Math.Clamp(presetIndex, 0, presetValues.Length - 1)];
            configuration.PreviewPresetSelection = presetSelection;
            SaveAppearanceChange("LayoutStyle", undoBefore);
        }


        DrawTimeTextFontCombo(profile, "LayoutStyle", undoBefore);
        ImGui.SameLine(0f, 18f);
        DrawCompactColonCombo();

        if (ImGui.Button(T("Apply Preset To Active Profile")))
        {
            configuration.ApplyPresetToActiveProfile(presetSelection);
            SaveAppearanceChange("LayoutStyle", undoBefore);
            textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
        }
    }

    private void DrawMainTimeVisibilitySettings(ClockProfile profile)
    {
        var undoBefore = profile.Clone();
        var startX = ImGui.GetCursorPosX();
        var secondColumnX = startX + 150f;

        bool showBorder = profile.ShowBorder;
        if (Checkbox($"{T("Border")}##MainTimeBorder", ref showBorder))
        {
            profile.ShowBorder = showBorder;
            SaveAppearanceChange("MainTime", undoBefore);
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(secondColumnX);
        bool showShadowText = profile.ShowShadowText;
        if (Checkbox($"{T("Shadow Text")}##MainTimeShadowText", ref showShadowText))
        {
            profile.ShowShadowText = showShadowText;
            SaveAppearanceChange("MainTime", undoBefore);
        }

        bool showIcon = profile.ShowIcon;
        if (Checkbox($"{T("Badge")}##MainTimeShowIcon", ref showIcon))
        {
            profile.ShowIcon = showIcon;
            SaveAppearanceChange("MainTime", undoBefore);
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(secondColumnX);
        bool showIconBorder = profile.ShowIconBorder;
        if (Checkbox($"{T("Badge Border")}##MainTimeIconBorder", ref showIconBorder))
        {
            profile.ShowIconBorder = showIconBorder;
            SaveAppearanceChange("MainTime", undoBefore);
        }
    }

    private void DrawMainTimeTextSettings(ClockProfile profile)
    {
        var undoBefore = profile.Clone();

        DrawCompactColorPair("Text Color", ref profile.ClockTextColor, "##TextColor", "Shadow Color", ref profile.ClockShadowColor, "##ShadowColor", "MainTime", undoBefore);

        DrawTextSizeControl("MainTime", undoBefore);

        if (profile.TimeTextFont == ClockTimeTextFont.Digital && !plugin.IsDigitalClockFontReady())
            ImGui.TextDisabled(T("Digital font is loading; check /xllog if needed."));
        if (profile.TimeTextFont == ClockTimeTextFont.Technology && !plugin.IsTechnologyClockFontReady())
            ImGui.TextDisabled(T("Tech font is loading; check /xllog if needed."));
        if (profile.TimeTextFont == ClockTimeTextFont.Ka1 && !plugin.IsKa1ClockFontReady())
            ImGui.TextDisabled(T("Cartoon font is loading; check /xllog if needed."));
        if (profile.TimeTextFont == ClockTimeTextFont.Countdown && !plugin.IsCountdownClockFontReady())
            ImGui.TextDisabled(T("Countdown font is loading; check /xllog if needed."));

        DrawAppearanceSeparator();
        DrawMainTimeBackgroundSettings(profile);
    }

    private void DrawMainTimeBackgroundSettings(ClockProfile profile)
    {
        var undoBefore = profile.Clone();

        DrawCompactColorPair("Background Color", ref profile.ClockBackgroundColor, "##BgColor", "Border Color", ref profile.BorderColor, "##BorderColor", "MainTime", undoBefore);

        ImGui.SetNextItemWidth(AppearanceSliderWidth);
        float opacity = profile.ClockBackgroundOpacity;
        if (DrawEditableSliderFloat("Background Opacity", ref opacity, 0.0f, 1.0f, "%.2f", "MainBackgroundOpacity", "MainTime", undoBefore))
        {
            profile.ClockBackgroundOpacity = opacity;
            configuration.Save();
        }

        ImGui.SameLine(0f, 18f);
        ImGui.SetNextItemWidth(AppearanceSliderWidth);
        float borderOpacity = profile.BorderOpacity;
        if (DrawEditableSliderFloat("Border Opacity", ref borderOpacity, 0.0f, 1.0f, "%.2f", "MainBorderOpacity", "MainTime", undoBefore))
        {
            profile.BorderOpacity = borderOpacity;
            configuration.Save();
        }
    }

    private void DrawMainTimeIconSettings(ClockProfile profile)
    {
        var undoBefore = profile.Clone();

        DrawCompactColorInline("Text Color", ref profile.IconTextColor, "##IconTextColor", "MainTime", undoBefore);
        ImGui.SameLine(0f, 18f);
        DrawCompactColorInline("Background Color", ref profile.IconBackgroundColor, "##IconBgColor", "MainTime", undoBefore);
        ImGui.SameLine(0f, 18f);
        DrawCompactColorInline("Border Color", ref profile.IconBorderColor, "##IconBorderColor", "MainTime", undoBefore);

        ImGui.SetNextItemWidth(AppearanceSliderWidth);
        float iconBorderOpacity = profile.IconBorderOpacity;
        if (DrawEditableSliderFloat("Border Opacity", ref iconBorderOpacity, 0.0f, 1.0f, "%.2f", "MainIconBorderOpacity", "MainTime", undoBefore))
        {
            profile.IconBorderOpacity = iconBorderOpacity;
            configuration.Save();
        }
    }

    private void DrawLocalTimeLayoutSettings(ClockProfile profile)
    {
        var undoBefore = profile.Clone();
        var placementNames = new[] { "Inside main display", "Outside main display" };
        var placementIndex = (int)profile.LocalTimePlacement;
        ImGui.SetNextItemWidth(160f);
        if (DrawCombo("Placement", placementNames, ref placementIndex))
        {
            profile.LocalTimePlacement = (LocalTimePlacement)placementIndex;
            SaveAppearanceChange("LocalTime", undoBefore);
        }

        ImGui.SameLine(0f, 16f);
        var formatNames = TimeFormatHelper.Names;
        var localFormatIndex = Math.Clamp((int)profile.LocalTimeFormat, 0, formatNames.Length - 1);
        ImGui.SetNextItemWidth(108f);
        if (DrawCombo("Time Format", formatNames, ref localFormatIndex))
        {
            profile.LocalTimeFormat = (ClockTimeFormat)localFormatIndex;
            SaveAppearanceChange("LocalTime", undoBefore);
        }

        ImGui.SetNextItemWidth(LocalAppearanceSliderWidth);
        float localVerticalOffset = profile.LocalTimeVerticalOffset;
        if (DrawEditableSliderFloat("Vertical Offset", ref localVerticalOffset, -40.0f, 40.0f, "%.1f", "LocalVerticalOffset", "LocalTime", undoBefore))
        {
            profile.LocalTimeVerticalOffset = localVerticalOffset;
            configuration.Save();
        }

        ImGui.SameLine(0f, 18f);
        ImGui.SetNextItemWidth(LocalAppearanceSliderWidth);
        float localHorizontalOffset = profile.LocalTimeHorizontalOffset;
        if (DrawEditableSliderFloat("Horizontal Offset", ref localHorizontalOffset, -40.0f, 40.0f, "%.1f", "LocalHorizontalOffset", "LocalTime", undoBefore))
        {
            profile.LocalTimeHorizontalOffset = localHorizontalOffset;
            configuration.Save();
        }

        ImGui.TextDisabled(T("Moves local time without changing the main clock."));
    }

    private void DrawLocalTimeTextSettings(ClockProfile profile)
    {
        var undoBefore = profile.Clone();
        var localStyleNames = new[] { "Classic", "Minimal", "Strong Shadow", "Soft Glass", "Retro Panel", "Digital", "Tech", "Cartoon", "Countdown" };
        var localStyleIndex = (int)profile.LocalTimeDisplayStyle;
        ImGui.SetNextItemWidth(130f);
        if (DrawCombo("Display Style", localStyleNames, ref localStyleIndex))
        {
            profile.LocalTimeDisplayStyle = (ClockDisplayStyle)localStyleIndex;
            SaveAppearanceChange("LocalTime", undoBefore);
        }

        ImGui.SameLine(0f, 18f);
        bool localShadow = profile.LocalTimeShowShadowText;
        if (Checkbox($"{T("Shadow Text")}##LocalTimeShadowText", ref localShadow))
        {
            profile.LocalTimeShowShadowText = localShadow;
            SaveAppearanceChange("LocalTime", undoBefore);
        }

        DrawCompactColorInline("Text Color", ref profile.LocalTimeTextColor, "##LocalTextColor", "LocalTime", undoBefore);
        ImGui.SameLine(0f, 18f);
        DrawCompactColorInline("Shadow Color", ref profile.LocalTimeShadowColor, "##LocalShadowColor", "LocalTime", undoBefore);
        ImGui.SameLine(0f, 18f);
        DrawLocalTextSizeControl("LocalTime", undoBefore, LocalAppearanceSliderWidth);

        DrawAppearanceSeparator();
        DrawLocalTimeBackgroundSettings(profile);
    }

    private void DrawLocalTimeBackgroundSettings(ClockProfile profile)
    {
        var undoBefore = profile.Clone();
        bool localBorder = profile.LocalTimeShowBorder;
        if (Checkbox($"{T("Border")}##LocalTimeBorder", ref localBorder))
        {
            profile.LocalTimeShowBorder = localBorder;
            SaveAppearanceChange("LocalTime", undoBefore);
        }

        if (!profile.LocalTimeShowBorder)
            return;

        ImGui.SameLine(0f, 18f);
        DrawCompactColorInline("Background Color", ref profile.LocalTimeBackgroundColor, "##LocalBgColor", "LocalTime", undoBefore);
        ImGui.SameLine(0f, 18f);
        DrawCompactColorInline("Border Color", ref profile.LocalTimeBorderColor, "##LocalBorderColor", "LocalTime", undoBefore);

        ImGui.SetNextItemWidth(LocalAppearanceSliderWidth);
        float localOpacity = profile.LocalTimeBackgroundOpacity;
        if (DrawEditableSliderFloat("Background Opacity", ref localOpacity, 0.0f, 1.0f, "%.2f", "LocalBackgroundOpacity", "LocalTime", undoBefore))
        {
            profile.LocalTimeBackgroundOpacity = localOpacity;
            configuration.Save();
        }

        ImGui.SameLine(0f, 18f);
        ImGui.SetNextItemWidth(LocalAppearanceSliderWidth);
        float localBorderOpacity = profile.LocalTimeBorderOpacity;
        if (DrawEditableSliderFloat("Border Opacity", ref localBorderOpacity, 0.0f, 1.0f, "%.2f", "LocalBorderOpacity", "LocalTime", undoBefore))
        {
            profile.LocalTimeBorderOpacity = localBorderOpacity;
            configuration.Save();
        }

        ImGui.TextDisabled(T("Uses separate local-time styling."));
    }

    private void DrawLocalTimeIconSettings(ClockProfile profile)
    {
        var undoBefore = profile.Clone();
        bool localIcon = profile.LocalTimeShowIcon;
        if (Checkbox($"{T("Show")}##LocalTimeShowIcon", ref localIcon))
        {
            profile.LocalTimeShowIcon = localIcon;
            SaveAppearanceChange("LocalTime", undoBefore);
        }

        if (profile.LocalTimeShowIcon)
        {
            ImGui.SameLine(0f, 18f);
            DrawCompactColorInline("Background Color", ref profile.LocalTimeIconBackgroundColor, "##LocalIconBgColor", "LocalTime", undoBefore);
            ImGui.SameLine(0f, 18f);
            DrawCompactColorInline("Text Color", ref profile.LocalTimeIconTextColor, "##LocalIconTextColor", "LocalTime", undoBefore);
        }

        bool localIconBorder = profile.LocalTimeShowIconBorder;
        if (Checkbox($"{T("Badge Border")}##LocalTimeIconBorder", ref localIconBorder))
        {
            profile.LocalTimeShowIconBorder = localIconBorder;
            SaveAppearanceChange("LocalTime", undoBefore);
        }

        if (!profile.LocalTimeShowIconBorder)
            return;

        ImGui.SameLine(0f, 18f);
        DrawCompactColorInline("Border Color", ref profile.LocalTimeIconBorderColor, "##LocalIconBorderColor", "LocalTime", undoBefore);
        ImGui.SameLine(0f, 18f);
        ImGui.SetNextItemWidth(LocalAppearanceSliderWidth);
        float localIconBorderOpacity = profile.LocalTimeIconBorderOpacity;
        if (DrawEditableSliderFloat("Border Opacity", ref localIconBorderOpacity, 0.0f, 1.0f, "%.2f", "LocalIconBorderOpacity", "LocalTime", undoBefore))
        {
            profile.LocalTimeIconBorderOpacity = localIconBorderOpacity;
            configuration.Save();
        }
    }

    private void DrawTimeTextFontCombo(ClockProfile profile, string? undoKey = null, ClockProfile? undoBefore = null)
    {
        var fontValues = new[] { ClockTimeTextFont.Default, ClockTimeTextFont.Digital, ClockTimeTextFont.Technology, ClockTimeTextFont.Ka1, ClockTimeTextFont.Countdown };
        var fontNames = new[] { "Default", "Digital", "Tech", "Cartoon", "Countdown" };
        var fontIndex = Array.IndexOf(fontValues, profile.TimeTextFont);
        if (fontIndex < 0)
            fontIndex = 0;

        ImGui.SetNextItemWidth(112f);
        if (ImGui.BeginCombo(T("Clock Font"), T(fontNames[fontIndex])))
        {
            for (int i = 0; i < fontNames.Length; i++)
            {
                var fontValue = fontValues[i];
                bool selected = i == fontIndex;

                bool changed;
                using (plugin.PushClockTimeFont(fontValue))
                    changed = ImGui.Selectable(T(fontNames[i]), selected);

                if (changed)
                {
                    profile.TimeTextFont = fontValue;
                    if (undoKey != null && undoBefore != null)
                        SaveAppearanceChange(undoKey, undoBefore);
                    else
                        configuration.Save();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }

    private void DrawOverlayTextAppearanceSettings(bool nextAlarm)
    {
        var profile = configuration.GetActiveProfile();
        var undoBefore = profile.Clone();
        var styleNames = new[] { "Classic", "Minimal", "Strong Shadow", "Soft Glass", "Retro Panel", "Digital", "Tech", "Cartoon", "Countdown" };
        var style = nextAlarm ? profile.NextAlarmOverlayDisplayStyle : profile.MaintenanceOverlayDisplayStyle;
        var styleIndex = Math.Clamp((int)style, 0, styleNames.Length - 1);

        ImGui.SetNextItemWidth(114f);
        if (ImGui.BeginCombo($"{T("Display Style")}##{(nextAlarm ? "NextAlarmOverlayStyle" : "MaintenanceOverlayStyle")}", T(styleNames[styleIndex])))
        {
            for (var i = 0; i < styleNames.Length; i++)
            {
                var selected = i == styleIndex;
                if (ImGui.Selectable(T(styleNames[i]), selected))
                {
                    if (nextAlarm)
                        profile.NextAlarmOverlayDisplayStyle = (ClockDisplayStyle)i;
                    else
                        profile.MaintenanceOverlayDisplayStyle = (ClockDisplayStyle)i;

                    SaveAppearanceChange("Extras", undoBefore);
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        var shadow = nextAlarm ? profile.NextAlarmOverlayShowShadowText : profile.MaintenanceOverlayShowShadowText;
        if (Checkbox($"{T("Shadow Text")}##{(nextAlarm ? "NextAlarmOverlayShadowToggle" : "MaintenanceOverlayShadowToggle")}", ref shadow))
        {
            if (nextAlarm)
                profile.NextAlarmOverlayShowShadowText = shadow;
            else
                profile.MaintenanceOverlayShowShadowText = shadow;

            SaveAppearanceChange("Extras", undoBefore);
        }

        ImGui.SameLine(0f, 18f);
        if (nextAlarm)
        {
            DrawCompactColorInline("Shadow Color", ref profile.NextAlarmOverlayShadowColor, "##NextAlarmOverlayShadowColor", "Extras", undoBefore);
            ImGui.SameLine(0f, 18f);
            DrawCompactColorInline("Text Color", ref profile.NextAlarmOverlayTextColor, "##NextAlarmOverlayTextColor", "Extras", undoBefore);
        }
        else
        {
            DrawCompactColorInline("Shadow Color", ref profile.MaintenanceOverlayShadowColor, "##MaintenanceOverlayShadowColor", "Extras", undoBefore);
            ImGui.SameLine(0f, 18f);
            DrawCompactColorInline("Text Color", ref profile.MaintenanceOverlayTextColor, "##MaintenanceOverlayTextColor", "Extras", undoBefore);
        }
    }

    private void DrawLocalTextSizeControl(string? undoKey = null, ClockProfile? undoBefore = null, float width = AppearanceSliderWidth)
    {
        var profile = configuration.GetActiveProfile();

        ImGui.SetNextItemWidth(width);
        float scale = profile.LocalTimeTextScale;
        if (DrawEditableSliderFloat("Text Size", ref scale, 0.35f, 5.0f, "%.2f", "LocalTextSize", undoKey, undoBefore))
        {
            profile.LocalTimeTextScale = scale;
            configuration.Save();
        }
    }



    private void DrawCompactColorPair(string firstLabel, ref Vector4 firstColor, string firstId, string secondLabel, ref Vector4 secondColor, string secondId, string undoKey, ClockProfile undoBefore)
    {
        DrawCompactColorInline(firstLabel, ref firstColor, firstId, undoKey, undoBefore);
        ImGui.SameLine(0f, 18f);
        DrawCompactColorInline(secondLabel, ref secondColor, secondId, undoKey, undoBefore);
    }

    private void DrawCompactColorInline(string label, ref Vector4 color, string id, string? undoKey = null, ClockProfile? undoBefore = null)
    {
        ImGui.SetNextItemWidth(28f);
        var changed = ImGui.ColorEdit4(id, ref color, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.AlphaBar);

        if (undoKey != null && undoBefore != null && ImGui.IsItemActivated())
            colorEditUndoStart[id] = undoBefore.Clone();

        if (changed)
            configuration.Save();

        if (undoKey != null && ImGui.IsItemDeactivatedAfterEdit())
        {
            if (colorEditUndoStart.TryGetValue(id, out var beforeEdit))
            {
                SaveAppearanceChange(undoKey, beforeEdit);
                colorEditUndoStart.Remove(id);
            }
        }

        ImGui.SameLine(0f, 4f);
        ImGui.TextUnformatted(T(label));
    }


    private bool DrawCreateAlarmActionRow(string editorZone, bool isEditingAlarm, bool alarmInPast, bool overlayMode = false)
    {
        var completed = false;
        if (alarmEditorControlStartX > 0f)
            ImGui.SetCursorPosX(alarmEditorControlStartX);

        var pulseAddAlarm = chatAlarmSetupPending && !isEditingAlarm && !alarmInPast;

        if (overlayMode)
        {
            var totalWidth = alarmEditorControlWidth > 0f ? alarmEditorControlWidth : 260f;
            var iconSize = ImGui.GetFrameHeight() + 14f;
            var count = isEditingAlarm ? 2 : 1;
            var rowWidth = count * iconSize + MathF.Max(0, count - 1) * ImGui.GetStyle().ItemSpacing.X;
            ImGui.SetCursorPosX(alarmEditorControlStartX + MathF.Max(0f, (totalWidth - rowWidth) * 0.5f));
        }

        ImGui.BeginDisabled(isEditingAlarm || alarmInPast);
        if (DrawCreateAlarmIconButton("AddAlarmIcon", "\uf0c7", pulseAddAlarm ? GoldTextColor : Vector4.One, T("Add Alarm"), isEditingAlarm || alarmInPast))
        {
            AlarmConfigurationService.AddFromEditor(configuration, editorZone);
            ResetAlarmEditorQuickOptions();
            chatAlarmSetupPending = false;
            editingAlarmTimeZoneId = null;
            configuration.Save();
            completed = true;
        }
        ImGui.EndDisabled();
        if (alarmInPast && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(T("Can't create a Alarm for the past!"));

        if (!overlayMode)
        {
            ImGui.SameLine();
            DrawAlarmSnoozePickerButton();

            ImGui.SameLine();
            DrawAlarmSoundPickerButton();
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!isEditingAlarm);
        if (DrawCreateAlarmIconButton("EditAlarmIcon", "\uf31c", isEditingAlarm ? new Vector4(0.82f, 0.62f, 1.0f, 1f) : Vector4.One, T("Edit Alarm"), !isEditingAlarm) && editingAlarmId.HasValue)
        {
            if (AlarmConfigurationService.UpdateFromEditor(configuration, editingAlarmId.Value, editorZone))
            {
                configuration.AlarmEditorMessage = string.Empty;
                configuration.Save();
                ClearAlarmEditingState();
                completed = true;
            }
        }
        ImGui.EndDisabled();

        if (overlayMode && isEditingAlarm && editingAlarmId.HasValue)
        {
            ImGui.SameLine();
            if (DrawCreateAlarmIconButton("DeleteEditingAlarmIcon", "\uf2ed", new Vector4(1.0f, 0.45f, 0.45f, 1f), T("Delete")))
            {
                AlarmConfigurationService.Remove(configuration, editingAlarmId.Value);
                ClearAlarmEditingState();
                configuration.Save();
                completed = true;
            }
        }

        if (!overlayMode)
        {
            ImGui.SameLine();
            DrawAlarmRepeatPickerButton();
        }

        ImGui.Spacing();
        return completed;
    }

    private float GetAlarmPanelWidth()
    {
        if (alarmEditorControlWidth > 0f && alarmEditorPanelWidth > 0f)
            return alarmEditorPanelWidth + (alarmEditorControlWidth - alarmEditorPanelWidth) * 0.5f + 54f;

        return alarmEditorPanelWidth > 0f ? alarmEditorPanelWidth : 330f;
    }

    private void SetAlarmPanelPage(int page)
    {
        page = Math.Clamp(page, 0, 3);
        if (alarmPanelPage == page)
            return;

        alarmPanelPreviousPage = alarmPanelPage;
        alarmPanelPage = page;
        alarmPanelSlideStartedAt = ImGui.GetTime();
    }

    private void DrawAlarmPanelAlarmOptionsPanel()
    {
        if (alarmEditorControlStartX > 0f)
            ImGui.SetCursorPosX(alarmEditorControlStartX);

        var width = GetAlarmPanelWidth();
        var start = ImGui.GetCursorScreenPos();
        var rowHeight = 42f;
        var panelHeight = 176f;
        var drawList = ImGui.GetWindowDrawList();
        var panelBg = new Vector4(0.82f, 0.82f, 0.82f, 0.08f);
        var line = new Vector4(1f, 1f, 1f, 0.08f);
        drawList.AddRectFilled(start, start + new Vector2(width, panelHeight), ImGui.GetColorU32(panelBg), 10f);

        drawList.PushClipRect(start, start + new Vector2(width, panelHeight), true);

        var animAge = Math.Clamp((float)((ImGui.GetTime() - alarmPanelSlideStartedAt) / 0.24), 0f, 1f);
        var anim = 1f - MathF.Pow(1f - animAge, 3f);
        var sliding = alarmPanelPreviousPage != alarmPanelPage && animAge < 1f;
        if (sliding)
        {
            var dir = alarmPanelPage > alarmPanelPreviousPage ? 1f : -1f;
            DrawAlarmPanelPage(alarmPanelPreviousPage, start + new Vector2(-dir * width * anim, 0f), width, panelHeight, rowHeight, line);
            DrawAlarmPanelPage(alarmPanelPage, start + new Vector2(dir * width * (1f - anim), 0f), width, panelHeight, rowHeight, line);
        }
        else
        {
            alarmPanelPreviousPage = alarmPanelPage;
            DrawAlarmPanelPage(alarmPanelPage, start, width, panelHeight, rowHeight, line);
        }

        drawList.PopClipRect();
        ImGui.SetCursorScreenPos(start + new Vector2(0f, panelHeight + ImGui.GetStyle().ItemSpacing.Y));
    }

    private void DrawAlarmPanelPage(int page, Vector2 start, float width, float panelHeight, float rowHeight, Vector4 line)
    {
        var drawList = ImGui.GetWindowDrawList();

        switch (page)
        {
            case 1:
                DrawAlarmPanelPanelBackTitle(start, width, rowHeight, T("Repeat"));
                DrawRepeatChoiceGrid(start + new Vector2(0f, rowHeight + 12f), width);
                break;
            case 2:
                DrawAlarmPanelPanelBackTitle(start, width, rowHeight, T("Game Sounds"));
                DrawSoundPanelHeader(start + new Vector2(0f, rowHeight + 7f), width);
                DrawSoundChoiceGrid(start + new Vector2(0f, rowHeight + 65f), width, 21f);
                break;
            case 3:
                DrawAlarmPanelPanelBackTitle(start, width, rowHeight, T("Setup Snooze"));
                DrawSnoozeChoiceGrid(start + new Vector2(0f, rowHeight + 12f), width);
                break;
            default:
                DrawAlarmPanelOptionRow(start, width, rowHeight, "Repeat", AlarmConfigurationService.GetRepeatLabel(configuration.AlarmEditorRepeatMode), "\uf1da", 0, () => SetAlarmPanelPage(1), line);
                DrawAlarmPanelLabelRow(start, width, rowHeight, 1, line);
                DrawAlarmPanelOptionRow(start, width, rowHeight, "Sound", configuration.AlarmSoundId == 0 ? T("None") : configuration.AlarmSoundId.ToString(CultureInfo.InvariantCulture), "\uf028", 2, () => SetAlarmPanelPage(2), line);
                DrawAlarmPanelOptionRow(start, width, rowHeight, "Setup Snooze", configuration.AlarmEditorSnoozeEnabled ? $"{configuration.AlarmEditorSnoozeMinutes} {T("Minutes")}" : T("None"), "\uf236", 3, () => SetAlarmPanelPage(3), line);
                break;
        }

        void DrawAlarmPanelPanelBackTitle(Vector2 panelStart, float panelWidth, float rowH, string title)
        {
            var rowMin = panelStart;
            var rowMax = rowMin + new Vector2(panelWidth, rowH);
            ImGui.SetCursorScreenPos(rowMin);
            ImGui.InvisibleButton($"##AlarmPanelPanelBack{page}", new Vector2(44f, rowH));
            var hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked())
                SetAlarmPanelPage(0);
            using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
                drawList.AddText(rowMin + new Vector2(16f, 12f), ImGui.GetColorU32(hovered ? GoldTextColor : new Vector4(1f, 1f, 1f, 0.78f)), "\uf104");
            var titleSize = ImGui.CalcTextSize(title);
            drawList.AddText(new Vector2(rowMin.X + (panelWidth - titleSize.X) * 0.5f, rowMin.Y + 11f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.94f)), title);
            DrawAlarmPanelFadeDivider(drawList, new Vector2(rowMin.X + 42f, rowMax.Y), new Vector2(rowMax.X, rowMax.Y), line.W, 1f);
        }

        void DrawRepeatChoiceGrid(Vector2 gridStart, float panelWidth)
        {
            var options = new[]
            {
                (Label: T("Once"), Selected: configuration.AlarmEditorRepeatMode == AlarmRepeatMode.None, Click: new Action(() => { configuration.AlarmEditorRepeatMode = AlarmRepeatMode.None; configuration.Save(); })),
                (Label: T("Daily"), Selected: configuration.AlarmEditorRepeatMode == AlarmRepeatMode.Daily, Click: new Action(() => { configuration.AlarmEditorRepeatMode = AlarmRepeatMode.Daily; configuration.Save(); })),
                (Label: T("Weekly"), Selected: configuration.AlarmEditorRepeatMode == AlarmRepeatMode.Weekly, Click: new Action(() => { configuration.AlarmEditorRepeatMode = AlarmRepeatMode.Weekly; configuration.Save(); })),
                (Label: T("Weekdays"), Selected: configuration.AlarmEditorRepeatMode == AlarmRepeatMode.Weekdays, Click: new Action(() => { configuration.AlarmEditorRepeatMode = AlarmRepeatMode.Weekdays; configuration.Save(); })),
                (Label: T("Weekends"), Selected: configuration.AlarmEditorRepeatMode == AlarmRepeatMode.Weekends, Click: new Action(() => { configuration.AlarmEditorRepeatMode = AlarmRepeatMode.Weekends; configuration.Save(); }))
            };

            DrawOptionPillGrid("Repeat", gridStart, panelWidth, options, 3);
        }

        void DrawSnoozeChoiceGrid(Vector2 gridStart, float panelWidth)
        {
            var options = new[]
            {
                (Label: T("None"), Selected: !configuration.AlarmEditorSnoozeEnabled, Click: new Action(() => { configuration.AlarmEditorSnoozeEnabled = false; configuration.Save(); })),
                (Label: string.Format(CultureInfo.InvariantCulture, T("{0} Minutes"), 5), Selected: configuration.AlarmEditorSnoozeEnabled && configuration.AlarmEditorSnoozeMinutes == 5, Click: new Action(() => { configuration.AlarmEditorSnoozeEnabled = true; configuration.AlarmEditorSnoozeMinutes = 5; configuration.Save(); })),
                (Label: string.Format(CultureInfo.InvariantCulture, T("{0} Minutes"), 10), Selected: configuration.AlarmEditorSnoozeEnabled && configuration.AlarmEditorSnoozeMinutes == 10, Click: new Action(() => { configuration.AlarmEditorSnoozeEnabled = true; configuration.AlarmEditorSnoozeMinutes = 10; configuration.Save(); })),
                (Label: string.Format(CultureInfo.InvariantCulture, T("{0} Minutes"), 15), Selected: configuration.AlarmEditorSnoozeEnabled && configuration.AlarmEditorSnoozeMinutes == 15, Click: new Action(() => { configuration.AlarmEditorSnoozeEnabled = true; configuration.AlarmEditorSnoozeMinutes = 15; configuration.Save(); })),
                (Label: string.Format(CultureInfo.InvariantCulture, T("{0} Minutes"), 30), Selected: configuration.AlarmEditorSnoozeEnabled && configuration.AlarmEditorSnoozeMinutes == 30, Click: new Action(() => { configuration.AlarmEditorSnoozeEnabled = true; configuration.AlarmEditorSnoozeMinutes = 30; configuration.Save(); }))
            };

            DrawOptionPillGrid("Snooze", gridStart, panelWidth, options, 2);
        }

        void DrawOptionPillGrid(string idPrefix, Vector2 gridStart, float panelWidth, (string Label, bool Selected, Action Click)[] options, int cols)
        {
            var pad = 12f;
            var gap = 7f;
            var rowGap = 8f;
            var cellHeight = 26f;
            var cellWidth = (panelWidth - pad * 2f - gap * (cols - 1)) / cols;

            for (var i = 0; i < options.Length; i++)
            {
                var col = i % cols;
                var row = i / cols;
                var cellMin = gridStart + new Vector2(pad + (cellWidth + gap) * col, (cellHeight + rowGap) * row);
                var cellMax = cellMin + new Vector2(cellWidth, cellHeight);
                var option = options[i];

                ImGui.SetCursorScreenPos(cellMin);
                ImGui.InvisibleButton($"##AlarmPanel{idPrefix}Choice{page}_{i}", new Vector2(cellWidth, cellHeight));
                var hovered = ImGui.IsItemHovered();
                if (ImGui.IsItemClicked())
                    option.Click();

                var fill = option.Selected
                    ? new Vector4(1f, 0.68f, 0.12f, 0.95f)
                    : hovered ? new Vector4(1f, 1f, 1f, 0.095f) : new Vector4(1f, 1f, 1f, 0.045f);
                var border = option.Selected
                    ? new Vector4(1f, 0.78f, 0.30f, 0.90f)
                    : new Vector4(1f, 1f, 1f, 0.07f);

                drawList.AddRectFilled(cellMin, cellMax, ImGui.GetColorU32(fill), 7f);
                drawList.AddRect(cellMin, cellMax, ImGui.GetColorU32(border), 7f, ImDrawFlags.None, 1f);

                var labelSize = ImGui.CalcTextSize(option.Label);
                var color = option.Selected ? new Vector4(0f, 0f, 0f, 0.96f) : new Vector4(1f, 1f, 1f, 0.90f);
                drawList.AddText(new Vector2(cellMin.X + (cellWidth - labelSize.X) * 0.5f, cellMin.Y + (cellHeight - labelSize.Y) * 0.5f), ImGui.GetColorU32(color), option.Label);
            }
        }

        void DrawSoundPanelHeader(Vector2 headerStart, float panelWidth)
        {
            var modeHeight = 26f;
            var modeMin = headerStart + new Vector2(16f, 0f);
            var modeWidth = panelWidth - 32f;
            var modeMax = modeMin + new Vector2(modeWidth, modeHeight);
            var targetIndex = configuration.AlarmSoundId == 0 ? 0 : configuration.AlarmSoundRepeats ? 2 : 1;

            if (soundModeVisualIndex < 0)
                soundModeVisualIndex = targetIndex;
            if (soundModeTargetIndex != targetIndex)
            {
                soundModeVisualIndex = soundModeTargetIndex < 0 ? targetIndex : soundModeVisualIndex;
                soundModeTargetIndex = targetIndex;
                soundModeMoveStartedAt = ImGui.GetTime();
            }

            var moveAge = Math.Clamp((float)((ImGui.GetTime() - soundModeMoveStartedAt) / 0.18), 0f, 1f);
            var moveT = 1f - MathF.Pow(1f - moveAge, 3f);
            var pieceWidth = modeWidth / 3f;
            var visualX = modeMin.X + pieceWidth * (soundModeVisualIndex + (soundModeTargetIndex - soundModeVisualIndex) * moveT) + 3f;
            if (moveAge >= 1f)
                soundModeVisualIndex = targetIndex;

            drawList.AddRectFilled(modeMin, modeMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.050f)), 11f);
            drawList.AddRect(modeMin, modeMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.070f)), 11f, ImDrawFlags.None, 1f);

            var pillMin = new Vector2(visualX, modeMin.Y + 3f);
            var pillSize = new Vector2(pieceWidth - 6f, modeHeight - 6f);
            drawList.AddRectFilled(pillMin, pillMin + pillSize, ImGui.GetColorU32(GoldTextColor), 8f);

            DrawSoundModePill(modeMin, modeWidth, modeHeight, 0, T("None"), targetIndex == 0, () => { configuration.AlarmSoundId = 0; configuration.Save(); });

            if (configuration.OpenAlarmsOverlayOnAlarmTrigger)
            {
                DrawSoundModePill(modeMin, modeWidth, modeHeight, 1, T("Once"), targetIndex == 1, () => { if (configuration.AlarmSoundId == 0) configuration.AlarmSoundId = 9; configuration.AlarmSoundRepeats = false; configuration.Save(); plugin.PlaySelectedAlarmSoundOnly(); });
                DrawSoundModePill(modeMin, modeWidth, modeHeight, 2, T("Repeat"), targetIndex == 2, () => { if (configuration.AlarmSoundId == 0) configuration.AlarmSoundId = 9; configuration.AlarmSoundRepeats = true; configuration.Save(); plugin.PlaySelectedAlarmSoundOnly(); });
            }
            else
            {
                DrawSoundModePill(modeMin, modeWidth, modeHeight, 1, T("Sound"), targetIndex != 0, () => { if (configuration.AlarmSoundId == 0) configuration.AlarmSoundId = 9; configuration.Save(); plugin.PlaySelectedAlarmSoundOnly(); });
                DrawSoundModePill(modeMin, modeWidth, modeHeight, 2, T("Off"), targetIndex == 0, () => { configuration.AlarmSoundId = 0; configuration.Save(); });
            }

            var status = configuration.AlarmSoundId == 0
                ? T("No sound selected")
                : $"{T("Selected Sound")}: {configuration.AlarmSoundId}";
            var statusSize = ImGui.CalcTextSize(status);
            drawList.AddText(new Vector2(headerStart.X + (panelWidth - statusSize.X) * 0.5f, headerStart.Y + 36f), ImGui.GetColorU32(new Vector4(0.82f, 0.82f, 0.86f, 0.82f)), status);
        }

        void DrawSoundModePill(Vector2 modeMin, float modeWidth, float modeHeight, int col, string label, bool selected, Action click)
        {
            var pieceWidth = modeWidth / 3f;
            var min = modeMin + new Vector2(pieceWidth * col + 3f, 3f);
            var size = new Vector2(pieceWidth - 6f, modeHeight - 6f);
            var max = min + size;
            ImGui.SetCursorScreenPos(min);
            ImGui.InvisibleButton($"##AlarmPanelSoundMode{page}_{col}_{label}", size);
            var hovered = ImGui.IsItemHovered();
            if (!selected && hovered)
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.045f)), 8f);

            if (ImGui.IsItemClicked())
                click();

            var color = selected ? new Vector4(0f, 0f, 0f, 0.95f) : new Vector4(1f, 1f, 1f, 0.88f);
            var labelSize = ImGui.CalcTextSize(label);
            drawList.AddText(new Vector2(min.X + (size.X - labelSize.X) * 0.5f, min.Y + (size.Y - labelSize.Y) * 0.5f), ImGui.GetColorU32(color), label);
        }

        void DrawSoundChoiceGrid(Vector2 gridStart, float gridWidth, float rowH)
        {
            var pad = 10f;
            var gap = 5f;
            var cols = 6;
            var cellWidth = (gridWidth - pad * 2f - gap * (cols - 1)) / cols;
            var cellHeight = 18f;
            var selectedId = configuration.AlarmSoundId;

            for (var i = 0; i < 16; i++)
            {
                var id = i + 1;
                var col = i % cols;
                var row = i / cols;
                var cellMin = gridStart + new Vector2(pad + (cellWidth + gap) * col, rowH * row);
                var cellMax = cellMin + new Vector2(cellWidth, cellHeight);
                var selected = selectedId == id;

                ImGui.SetCursorScreenPos(cellMin);
                ImGui.InvisibleButton($"##AlarmPanelSoundChoice{id}", new Vector2(cellWidth, cellHeight));
                var hovered = ImGui.IsItemHovered();
                if (ImGui.IsItemClicked())
                {
                    configuration.AlarmSoundId = id;
                    configuration.Save();
                    plugin.PlaySelectedAlarmSoundOnly();
                }

                var fill = selected
                    ? new Vector4(1f, 0.68f, 0.12f, 0.95f)
                    : hovered ? new Vector4(1f, 1f, 1f, 0.095f) : new Vector4(1f, 1f, 1f, 0.045f);
                var border = selected
                    ? new Vector4(1f, 0.78f, 0.30f, 0.90f)
                    : new Vector4(1f, 1f, 1f, 0.07f);

                drawList.AddRectFilled(cellMin, cellMax, ImGui.GetColorU32(fill), 6f);
                drawList.AddRect(cellMin, cellMax, ImGui.GetColorU32(border), 6f, ImDrawFlags.None, 1f);

                var label = id.ToString(CultureInfo.InvariantCulture);
                var labelSize = ImGui.CalcTextSize(label);
                var color = selected ? new Vector4(0f, 0f, 0f, 0.96f) : new Vector4(1f, 1f, 1f, 0.90f);
                drawList.AddText(new Vector2(cellMin.X + (cellWidth - labelSize.X) * 0.5f, cellMin.Y + (cellHeight - labelSize.Y) * 0.5f), ImGui.GetColorU32(color), label);
            }
        }

    }

    private void DrawAlarmPanelLabelRow(Vector2 start, float width, float rowHeight, int row, Vector4 line)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rowMin = start + new Vector2(0f, row * rowHeight);
        var rowMax = rowMin + new Vector2(width, rowHeight);

        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
            drawList.AddText(rowMin + new Vector2(14f, 12f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.88f)), "\uf02b");
        drawList.AddText(rowMin + new Vector2(42f, 11f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.94f)), T("Label"));

        var inputMin = new Vector2(rowMin.X + width * 0.42f, rowMin.Y + 6f);
        var inputWidth = width * 0.52f;
        ImGui.SetCursorScreenPos(inputMin);
        ImGui.PushItemWidth(inputWidth);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(1f, 1f, 1f, 0.04f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(1f, 1f, 1f, 0.06f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 0f));
        if (ImGui.InputText("##AlarmPanelAlarmMessage", ref configuration.AlarmEditorMessage, 128))
            configuration.Save();
        var inputActive = ImGui.IsItemActive();
        ImGui.PopStyleColor(4);
        ImGui.PopItemWidth();

        var hasText = !string.IsNullOrEmpty(configuration.AlarmEditorMessage);
        var visibleText = hasText ? configuration.AlarmEditorMessage : T("Alarm Message");
        var visibleColor = hasText
            ? new Vector4(1f, 1f, 1f, 0.98f)
            : new Vector4(0.62f, 0.62f, 0.66f, 0.86f);
        var visibleSize = ImGui.CalcTextSize(visibleText);
        var textX = inputMin.X + MathF.Max(0f, (inputWidth - visibleSize.X) * 0.5f);
        var textY = rowMin.Y + 11f;
        drawList.AddText(new Vector2(textX, textY), ImGui.GetColorU32(visibleColor), visibleText);
        if (inputActive && ((int)(ImGui.GetTime() * 2.0) & 1) == 0)
        {
            var caretX = hasText ? textX + visibleSize.X + 1f : textX;
            drawList.AddLine(new Vector2(caretX, textY - 1f), new Vector2(caretX, textY + ImGui.GetTextLineHeight() + 1f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)), 1f);
        }
        drawList.AddLine(new Vector2(rowMin.X + 42f, rowMax.Y), new Vector2(rowMax.X, rowMax.Y), ImGui.GetColorU32(line), 1f);
    }

    private void DrawAlarmPanelOptionRow(Vector2 start, float width, float rowHeight, string labelKey, string value, string icon, int row, Action click, Vector4 line)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rowMin = start + new Vector2(0f, row * rowHeight);
        var rowMax = rowMin + new Vector2(width, rowHeight);
        ImGui.SetCursorScreenPos(rowMin);
        ImGui.InvisibleButton($"##AlarmPanelOption{labelKey}", new Vector2(width, rowHeight));
        var hovered = ImGui.IsItemHovered();
        if (hovered)
            drawList.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.035f)), row == 0 ? 10f : 0f);
        if (ImGui.IsItemClicked())
            click();

        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
            drawList.AddText(rowMin + new Vector2(14f, 12f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.92f)), icon);

        drawList.AddText(rowMin + new Vector2(42f, 11f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.94f)), T(labelKey));
        var rightText = T(value);
        var rightSize = ImGui.CalcTextSize(rightText);
        drawList.AddText(new Vector2(rowMax.X - rightSize.X - 30f, rowMin.Y + 11f), ImGui.GetColorU32(new Vector4(0.72f, 0.72f, 0.78f, 0.96f)), rightText);
        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
            drawList.AddText(new Vector2(rowMax.X - 21f, rowMin.Y + 12f), ImGui.GetColorU32(new Vector4(0.70f, 0.70f, 0.76f, 0.88f)), "\uf105");
        if (row < 3)
            DrawAlarmPanelFadeDivider(drawList, new Vector2(rowMin.X + 42f, rowMax.Y), new Vector2(rowMax.X, rowMax.Y), line.W, 1f);
    }

    private bool DrawAlarmPanelSaveAlarmButton()
    {
        if (alarmEditorControlStartX > 0f)
            ImGui.SetCursorPosX(alarmEditorControlStartX);

        var width = GetAlarmPanelWidth();
        var start = ImGui.GetCursorScreenPos();
        var height = 26f;
        var drawList = ImGui.GetWindowDrawList();
        var bg = new Vector4(0.82f, 0.82f, 0.82f, 0.08f);
        var blocked = IsAlarmOverlaySaveBlocked;
        var gold = blocked ? new Vector4(1f, 0.42f, 0.42f, 1f) : GoldTextColor;
        ImGui.InvisibleButton("##AlarmPanelSaveAlarmButton", new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        if (hovered)
            bg.W = 0.12f;
        drawList.AddRectFilled(start, start + new Vector2(width, height), ImGui.GetColorU32(bg), 10f);
        var text = T("Save Alarm");
        var textSize = ImGui.CalcTextSize(text);
        drawList.AddText(new Vector2(start.X + (width - textSize.X) * 0.5f, start.Y + (height - textSize.Y) * 0.5f), ImGui.GetColorU32(gold), text);
        if (hovered && blocked)
            ImGui.SetTooltip(T("Can't create a alarm for the past!"));
        if (ImGui.IsItemClicked() && !blocked)
        {
            if (CommitAlarmOverlayEditorFromHeader())
                return true;
        }
        return false;
    }

    private bool DrawAlarmPanelDeleteAlarmButton()
    {
        if (!editingAlarmId.HasValue)
            return false;

        if (alarmEditorControlStartX > 0f)
            ImGui.SetCursorPosX(alarmEditorControlStartX);

        var width = GetAlarmPanelWidth();
        var start = ImGui.GetCursorScreenPos();
        var height = 26f;
        var drawList = ImGui.GetWindowDrawList();
        var bg = new Vector4(0.82f, 0.82f, 0.82f, 0.08f);
        var red = new Vector4(1f, 0.34f, 0.34f, 1f);
        ImGui.InvisibleButton("##AlarmPanelDeleteAlarmButton", new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        if (hovered)
            bg.W = 0.12f;
        drawList.AddRectFilled(start, start + new Vector2(width, height), ImGui.GetColorU32(bg), 10f);
        var text = T("Delete Alarm");
        var textSize = ImGui.CalcTextSize(text);
        drawList.AddText(new Vector2(start.X + (width - textSize.X) * 0.5f, start.Y + (height - textSize.Y) * 0.5f), ImGui.GetColorU32(red), text);
        if (ImGui.IsItemClicked() && editingAlarmId.HasValue)
        {
            AlarmConfigurationService.Remove(configuration, editingAlarmId.Value);
            ClearAlarmEditingState();
            configuration.Save();
            alarmPanelPage = 0;
            return true;
        }
        return false;
    }



    public bool DrawAlarmOverlayCreateContent(bool fullPage = false)
    {
        if (fullPage)
        {
            var rows = 5f;
            var estimatedHeight = 126f + ImGui.GetTextLineHeight() * 2.1f + ImGui.GetStyle().ItemSpacing.Y * 3f + rows * 42f + 40f + (editingAlarmId.HasValue ? 34f : 0f);
            var yOffset = MathF.Max(0f, (ImGui.GetContentRegionAvail().Y - estimatedHeight) * 0.5f);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0f, yOffset - ImGui.GetTextLineHeight() * 3f));

            var title = editingAlarmId.HasValue ? T("Edit Alarm") : T("Add Alarm");
            var drawList = ImGui.GetWindowDrawList();
            var start = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var titleDividerY = start.Y + 39f;
            using (plugin.PushAlarmPanelAlarmTitleFont())
            {
                var titleSize = ImGui.CalcTextSize(title);
                var titleY = titleDividerY - titleSize.Y - 7f;
                drawList.AddText(new Vector2(MathF.Round(start.X + (width - titleSize.X) * 0.5f), MathF.Round(titleY)), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.98f)), title);
            }
            DrawAlarmPanelFadeDivider(drawList, new Vector2(start.X + 14f, titleDividerY), new Vector2(start.X + width - 14f, titleDividerY), 0.12f, 1.05f);
            ImGui.Dummy(new Vector2(1f, 42f));
        }

        DrawAlarmSelectors(fullPage);
        ImGui.Spacing();

        if (fullPage)
        {
            DrawAlarmPanelAlarmOptionsPanel();
            if (DrawAlarmPanelSaveAlarmButton())
                return true;
            if (editingAlarmId.HasValue && DrawAlarmPanelDeleteAlarmButton())
                return true;
            ImGui.Spacing();
            return false;
        }

        DrawAlarmMessageInput();
        ImGui.Spacing();

        var isEditingAlarm = editingAlarmId.HasValue;
        var editorZone = GetAlarmEditorTimeZone();
        var alarmInPast = !isEditingAlarm && IsAlarmEditorInPast(editorZone);
        return DrawCreateAlarmActionRow(editorZone, isEditingAlarm, alarmInPast, false);
    }

    public bool CommitAlarmOverlayEditorFromHeader()
    {
        var isEditingAlarm = editingAlarmId.HasValue;
        var editorZone = GetAlarmEditorTimeZone();
        var alarmInPast = !isEditingAlarm && IsAlarmEditorInPast(editorZone);
        if (alarmInPast)
            return false;

        if (isEditingAlarm && editingAlarmId.HasValue)
        {
            if (!AlarmConfigurationService.UpdateFromEditor(configuration, editingAlarmId.Value, editorZone))
                return false;

            configuration.AlarmEditorMessage = string.Empty;
            configuration.Save();
            ClearAlarmEditingState();
            alarmPanelPage = 0;
            return true;
        }

        AlarmConfigurationService.AddFromEditor(configuration, editorZone);
        ResetAlarmEditorQuickOptions();
        chatAlarmSetupPending = false;
        editingAlarmTimeZoneId = null;
        configuration.AlarmEditorMessage = string.Empty;
        configuration.Save();
        alarmPanelPage = 0;
        return true;
    }

    public bool IsEditingAlarm => editingAlarmId.HasValue;

    public bool IsCapturingAlarmsWindowKeybind => capturingAlarmsWindowKeybind;

    public bool IsAlarmOverlaySaveBlocked => IsAlarmEditorInPast(GetAlarmEditorTimeZone());

    public bool HasSelectedAlarmPanelAlarms => alarmPanelSelectedAlarmIds.Count > 0;

    public void DeleteSelectedAlarmPanelAlarms()
    {
        if (alarmPanelSelectedAlarmIds.Count == 0)
            return;

        configuration.Alarms.RemoveAll(a => alarmPanelSelectedAlarmIds.Contains(a.Id));
        alarmPanelSelectedAlarmIds.Clear();
        configuration.Save();
    }

    public void BeginNewAlarmFromOverlay()
    {
        alarmPanelSelectedAlarmIds.Clear();
        ClearAlarmEditingState();
        configuration.AlarmEditorMessage = string.Empty;
        alarmPanelPage = 0;
    }

    public void CancelAlarmOverlayEditor()
    {
        ClearAlarmEditingState();
        alarmPanelPage = 0;
    }

    public bool DrawAlarmPanelAlarmHistoryOverlay()
    {
        var alarms = configuration.Alarms
            .OrderBy(a => GetAlarmSortUtc(a))
            .ThenBy(a => a.Id)
            .ToList();

        if (alarms.Count == 0)
        {
            var drawList = ImGui.GetWindowDrawList();
            var start = ImGui.GetCursorScreenPos();
            var title = T("Alarms");
            var sub = T("No Alarms");
            var width = ImGui.GetContentRegionAvail().X;
            var y = start.Y + 20f;
            using (plugin.PushAlarmPanelAlarmTitleFont())
            {
                var titleSize = ImGui.CalcTextSize(title);
                drawList.AddText(new Vector2(start.X + (width - titleSize.X) * 0.5f, y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.98f)), title);
            }
            var subSize = ImGui.CalcTextSize(sub);
            drawList.AddText(new Vector2(start.X + (width - subSize.X) * 0.5f, y + 52f), ImGui.GetColorU32(new Vector4(0.62f, 0.62f, 0.66f, 0.92f)), sub);
            ImGui.Dummy(new Vector2(width, 150f));
            return false;
        }

        var described = alarms.Where(a => !string.IsNullOrWhiteSpace(a.Message)).ToList();
        var others = alarms.Where(a => string.IsNullOrWhiteSpace(a.Message)).ToList();
        var editRequested = false;
        var rowIndex = 0;

        foreach (var group in described.GroupBy(a => a.Message?.Trim() ?? string.Empty))
        {
            DrawAlarmPanelAlarmSectionLabel(group.Key, true);
            foreach (var alarm in group)
                editRequested |= DrawAlarmPanelAlarmRow(alarm, rowIndex++);
        }

        if (others.Count > 0)
        {
            DrawAlarmPanelAlarmSectionLabel(T("Others"), false);
            foreach (var alarm in others)
                editRequested |= DrawAlarmPanelAlarmRow(alarm, rowIndex++);
        }

        return editRequested;
    }

    private void DrawAlarmPanelFadeDivider(ImDrawListPtr drawList, Vector2 from, Vector2 to, float alpha = 0.09f, float thickness = 1f)
    {
        var width = to.X - from.X;
        if (width <= 2f)
            return;

        const int parts = 16;
        for (var i = 0; i < parts; i++)
        {
            var t0 = i / (float)parts;
            var t1 = (i + 1) / (float)parts;
            var mid = (t0 + t1) * 0.5f;
            var fade = MathF.Sin(mid * MathF.PI);
            var x0 = from.X + width * t0;
            var x1 = from.X + width * t1;
            drawList.AddLine(new Vector2(x0, from.Y), new Vector2(x1, to.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha * fade)), thickness);
        }
    }

    private void DrawAlarmPanelAlarmSectionLabel(string label, bool showMessageIcon)
    {
        ImGui.Dummy(new Vector2(1f, 14f));
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var x = start.X + 2f;
        if (showMessageIcon)
        {
            using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
            {
                var icon = "\uf236";
                drawList.AddText(new Vector2(x, start.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.84f)), icon);
                x += ImGui.CalcTextSize(icon).X + 8f;
            }
        }
        drawList.AddText(new Vector2(x, start.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.94f)), label);
        var lineY = start.Y + ImGui.GetTextLineHeight() + 8f;
        DrawAlarmPanelFadeDivider(drawList, new Vector2(start.X, lineY), new Vector2(start.X + width, lineY), 0.12f, 1.05f);
        ImGui.Dummy(new Vector2(width, ImGui.GetTextLineHeight() + 12f));
    }

    private bool DrawAlarmPanelAlarmRow(AlarmEntry alarm, int rowIndex)
    {
        var drawList = ImGui.GetWindowDrawList();
        var contentWidth = ImGui.GetContentRegionAvail().X;
        var rowHeight = 96f;
        var start = ImGui.GetCursorScreenPos();
        var end = start + new Vector2(contentWidth, rowHeight);
        var switchSize = new Vector2(54f, 30f);
        var switchPos = new Vector2(end.X - switchSize.X - 12f, start.Y + 27f);

        if ((rowIndex & 1) == 1)
            drawList.AddRectFilled(start, end, ImGui.GetColorU32(new Vector4(0.70f, 0.70f, 0.74f, 0.05f)), 8f);

        ImGui.InvisibleButton($"##AlarmPanelAlarmRow{alarm.Id}", new Vector2(contentWidth, rowHeight));
        var rowHovered = ImGui.IsItemHovered();
        var rowClicked = ImGui.IsItemClicked();
        var switchHovered = ImGui.IsMouseHoveringRect(switchPos, switchPos + switchSize, true);
        var switchClicked = switchHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);

        var selected = alarmPanelSelectedAlarmIds.Contains(alarm.Id);
        if (selected)
            drawList.AddRectFilled(start, end, ImGui.GetColorU32(new Vector4(1f, 0.68f, 0.12f, 0.10f)), 8f);
        else if (rowHovered && !switchHovered)
            drawList.AddRectFilled(start, end, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.032f)), 8f);

        var hasPendingSnooze = alarm.SnoozedUntilUtc > DateTime.MinValue && !alarm.SnoozeTriggered && !alarm.SnoozeCanceled;
        if (switchClicked)
        {
            alarm.Enabled = !alarm.Enabled;
            if (alarm.Enabled)
                alarm.HasTriggered = false;
            configuration.Save();
        }

        var editRequested = false;
        if (rowClicked && !switchHovered)
        {
            if (ImGui.GetIO().KeyCtrl)
            {
                if (!alarmPanelSelectedAlarmIds.Add(alarm.Id))
                    alarmPanelSelectedAlarmIds.Remove(alarm.Id);
            }
            else
            {
                alarmPanelSelectedAlarmIds.Clear();
                LoadAlarmIntoEditor(alarm);
                editRequested = true;
            }
        }

        var active = alarm.Enabled && (!alarm.HasTriggered || hasPendingSnooze);
        var timeColor = active
            ? ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.98f))
            : ImGui.GetColorU32(new Vector4(0.58f, 0.58f, 0.62f, 0.86f));
        var detailColor = active
            ? ImGui.GetColorU32(new Vector4(0.82f, 0.82f, 0.86f, 0.94f))
            : ImGui.GetColorU32(new Vector4(0.55f, 0.55f, 0.59f, 0.82f));

        var timeParts = BuildAlarmPanelAlarmTimeParts(alarm);
        var timeBase = new Vector2(MathF.Round(start.X + 4f), MathF.Round(start.Y + 12f));
        float approxTimeWidth;
        using (plugin.PushAlarmPanelAlarmFont())
        {
            drawList.AddText(timeBase, timeColor, timeParts.Time);
            approxTimeWidth = ImGui.CalcTextSize(timeParts.Time).X;
        }
        var suffixX = MathF.Round(timeBase.X + approxTimeWidth + 3f);
        if (!string.IsNullOrEmpty(timeParts.Suffix))
        {
            var suffixPos = new Vector2(suffixX, MathF.Round(timeBase.Y + 24f));
            drawList.AddText(suffixPos, timeColor, timeParts.Suffix);
            suffixX += ImGui.CalcTextSize(timeParts.Suffix).X + 6f;
        }

        var timeZoneText = TimeZoneHelper.ToShortText(alarm.GetEffectiveTimeZoneId());
        if (!string.IsNullOrWhiteSpace(timeZoneText))
            drawList.AddText(new Vector2(suffixX, MathF.Round(timeBase.Y + 24f)), timeColor, timeZoneText);

        var dateText = BuildAlarmPanelAlarmDateLabel(alarm);
        var meta = BuildAlarmPanelAlarmMeta(alarm);
        var detailText = string.IsNullOrWhiteSpace(meta) ? dateText : $"{dateText} · {meta}";
        if (!string.IsNullOrWhiteSpace(detailText))
            drawList.AddText(new Vector2(start.X + 7f, start.Y + 67f), detailColor, detailText);

        DrawAlarmPanelSwitchVisual(switchPos, switchSize, active, true, switchHovered);

        DrawAlarmPanelFadeDivider(drawList, new Vector2(start.X, end.Y - 1f), new Vector2(end.X, end.Y - 1f), 0.10f, 1.05f);
        return editRequested;
    }

    private DateTime GetAlarmSortUtc(AlarmEntry alarm)
    {
        return TimeZoneHelper.TryParseInZone(alarm.DateTimeText, alarm.GetEffectiveTimeZoneId(), out var utc)
            ? utc
            : DateTime.MaxValue;
    }

    private bool TryGetAlarmPanelAlarmDisplayUtc(AlarmEntry alarm, out DateTime utc)
    {
        if (alarm.SnoozedUntilUtc > DateTime.MinValue && !alarm.SnoozeTriggered && !alarm.SnoozeCanceled)
        {
            utc = DateTime.SpecifyKind(alarm.SnoozedUntilUtc, DateTimeKind.Utc);
            return true;
        }

        return TimeZoneHelper.TryParseInZone(alarm.DateTimeText, alarm.GetEffectiveTimeZoneId(), out utc);
    }

    private (string Time, string Suffix) BuildAlarmPanelAlarmTimeParts(AlarmEntry alarm)
    {
        if (!TryGetAlarmPanelAlarmDisplayUtc(alarm, out var utc))
            return ("--:--", string.Empty);

        var local = TimeZoneHelper.ConvertFromUtc(utc, alarm.GetEffectiveTimeZoneId());
        if (TimeFormatHelper.UsesTwelveHourClock(configuration.TimeFormat))
        {
            var hour = local.Hour % 12;
            if (hour == 0)
                hour = 12;
            return ($"{hour}:{local.Minute:00}", local.Hour >= 12 ? "PM" : "AM");
        }

        return ($"{local.Hour:00}:{local.Minute:00}", string.Empty);
    }

    private string BuildAlarmPanelAlarmMeta(AlarmEntry alarm)
    {
        var parts = new List<string>();
        if (alarm.RepeatMode != AlarmRepeatMode.None)
            parts.Add(T(AlarmConfigurationService.GetRepeatLabel(alarm.RepeatMode)));
        if (alarm.SnoozeEnabled)
            parts.Add(string.Format(CultureInfo.InvariantCulture, T("Snooze {0}m"), alarm.SnoozeMinutes));
        return string.Join(" · ", parts);
    }

    private string BuildAlarmPanelAlarmDateLabel(AlarmEntry alarm)
    {
        if (!TryGetAlarmPanelAlarmDisplayUtc(alarm, out var utc))
            return string.Empty;

        var local = TimeZoneHelper.ConvertFromUtc(utc, alarm.GetEffectiveTimeZoneId()).Date;
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local).Date;
        var days = (local - today).Days;

        if (days == 0)
            return T("Today");
        if (days == 1)
            return T("Tomorrow");
        if (days > 1 && days < 7)
            return T(local.DayOfWeek.ToString());

        return local.ToString("dd MMM", CultureInfo.CurrentCulture);
    }

    private void DrawAlarmPanelSwitchVisual(Vector2 pos, Vector2 size, bool on, bool enabled, bool hovered)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bg = on
            ? new Vector4(1f, 0.68f, 0.12f, enabled ? 1f : 0.36f)
            : new Vector4(0.18f, 0.18f, 0.21f, enabled ? 1f : 0.46f);
        if (hovered && enabled)
            bg.W = 0.94f;

        drawList.AddRectFilled(pos, pos + size, ImGui.GetColorU32(bg), size.Y * 0.5f);
        var knobRadius = size.Y * 0.42f;
        var knobX = on ? pos.X + size.X - size.Y * 0.5f : pos.X + size.Y * 0.5f;
        var knobCenter = new Vector2(knobX, pos.Y + size.Y * 0.5f);
        drawList.AddCircleFilled(knobCenter, knobRadius, ImGui.GetColorU32(new Vector4(0.96f, 0.96f, 0.96f, enabled ? 1f : 0.70f)), 32);
    }





    private void DrawAlarmSoundPickerButton()
    {
        if (configuration.AlarmSoundId < 0 || configuration.AlarmSoundId > Plugin.MaxAlarmSoundEffectId)
            configuration.AlarmSoundId = 9;

        var hasSound = configuration.AlarmSoundId > 0;
        var color = hasSound ? GoldTextColor : Vector4.One;
        if (DrawCreateAlarmIconButton("ClockAlarmSoundPicker", "\uf028", color, T("Sound")))
            ImGui.OpenPopup("ClockAlarmSoundIdPopup");

        DrawAlarmSoundPickerPopupOnly();
    }

    private void ResetAlarmEditorQuickOptions()
    {
        configuration.AlarmEditorMessage = string.Empty;
        configuration.AlarmEditorSnoozeEnabled = false;
        configuration.AlarmEditorSnoozeMinutes = 5;
        configuration.AlarmEditorRepeatMode = AlarmRepeatMode.None;
        configuration.AlarmSoundId = 9;
        alarmPanelPage = 0;
        alarmPanelPreviousPage = 0;
    }

    private bool DrawCreateAlarmIconButton(string id, string symbol, Vector4 color, string tooltip, bool blocked = false)
    {
        var size = new Vector2(ImGui.GetFrameHeight() + 8f, ImGui.GetFrameHeight() + 6f);
        ImGui.InvisibleButton($"##{id}", size);
        var clicked = ImGui.IsItemClicked() && !blocked;
        var hovered = blocked ? ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) : ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var drawColor = color;

        if (hovered)
            drawColor = blocked ? new Vector4(1.0f, 0.45f, 0.45f, 1f) : GoldTextColor;

        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize() * 1.28f;
            var textSize = ImGui.CalcTextSize(symbol) * 1.28f;
            var textPos = new Vector2(
                min.X + MathF.Floor(((max.X - min.X) - textSize.X) * 0.5f),
                min.Y + MathF.Floor(((max.Y - min.Y) - textSize.Y) * 0.5f));
            ImGui.GetWindowDrawList().AddText(font, fontSize, textPos, ImGui.GetColorU32(drawColor), symbol);
        }

        if (hovered)
            ImGui.SetTooltip(tooltip);

        return clicked;
    }




    private void DrawAlarmSoundPickerPopupOnly()
    {
        if (!ImGui.BeginPopup("ClockAlarmSoundIdPopup"))
            return;

        if (ImGui.Selectable(T("None"), configuration.AlarmSoundId == 0, ImGuiSelectableFlags.DontClosePopups))
        {
            configuration.AlarmSoundId = 0;
            configuration.Save();
        }

        if (configuration.OpenAlarmsOverlayOnAlarmTrigger)
        {
            ImGui.SameLine();
            if (ImGui.Selectable(T("Once"), !configuration.AlarmSoundRepeats, ImGuiSelectableFlags.DontClosePopups))
            {
                configuration.AlarmSoundRepeats = false;
                configuration.Save();
            }

            ImGui.SameLine();
            if (ImGui.Selectable(T("Repeat"), configuration.AlarmSoundRepeats, ImGuiSelectableFlags.DontClosePopups))
            {
                configuration.AlarmSoundRepeats = true;
                configuration.Save();
            }
        }

        for (var soundId = Plugin.MinAlarmSoundEffectId; soundId <= Plugin.MaxAlarmSoundEffectId; soundId++)
        {
            var soundText = soundId.ToString(CultureInfo.InvariantCulture);
            var isSelected = configuration.AlarmSoundId == soundId;
            if (ImGui.Selectable(soundText, isSelected, ImGuiSelectableFlags.DontClosePopups))
            {
                configuration.AlarmSoundId = soundId;
                configuration.Save();
                plugin.PlaySelectedAlarmSoundOnly();
            }

            if (isSelected)
                ImGui.SetItemDefaultFocus();
        }

        ImGui.EndPopup();
    }

    private void DrawAlarmSnoozePickerPopupOnly()
    {
        if (!ImGui.BeginPopup("AlarmSnoozePickerPopup"))
            return;

        if (ImGui.Selectable(T("None"), !configuration.AlarmEditorSnoozeEnabled))
        {
            configuration.AlarmEditorSnoozeEnabled = false;
            configuration.AlarmEditorSnoozeMinutes = 5;
            configuration.Save();
        }

        var choices = new[] { 5, 10, 15, 30 };
        foreach (var minutes in choices)
        {
            var label = $"{minutes} {T("Minutes")}";
            var selected = configuration.AlarmEditorSnoozeEnabled && configuration.AlarmEditorSnoozeMinutes == minutes;
            if (ImGui.Selectable(label, selected))
            {
                configuration.AlarmEditorSnoozeEnabled = true;
                configuration.AlarmEditorSnoozeMinutes = minutes;
                configuration.Save();
            }

            if (selected)
                ImGui.SetItemDefaultFocus();
        }

        ImGui.EndPopup();
    }

    private void DrawAlarmRepeatPickerPopupOnly()
    {
        if (!ImGui.BeginPopup("AlarmRepeatPickerPopup"))
            return;

        var repeatNames = new[] { "Once", "Daily", "Weekly", "Weekdays", "Weekends" };
        for (var i = 0; i < repeatNames.Length; i++)
        {
            var selected = (int)configuration.AlarmEditorRepeatMode == i;
            if (ImGui.Selectable(T(repeatNames[i]), selected))
            {
                configuration.AlarmEditorRepeatMode = (AlarmRepeatMode)i;
                configuration.Save();
            }

            if (selected)
                ImGui.SetItemDefaultFocus();
        }

        ImGui.EndPopup();
    }

    private void DrawAlarmSnoozePickerButton()
    {
        var active = configuration.AlarmEditorSnoozeEnabled;
        var color = active ? GoldTextColor : Vector4.One;
        var tooltip = active
            ? string.Format(T("Snooze for {0}"), $"{configuration.AlarmEditorSnoozeMinutes} {T("Minutes")}")
            : T("Setup Snooze");

        if (DrawCreateAlarmIconButton("AlarmSnoozePicker", "\uf236", color, tooltip))
            ImGui.OpenPopup("AlarmSnoozePickerPopup");

        DrawAlarmSnoozePickerPopupOnly();
    }

    private void DrawAlarmRepeatPickerButton()
    {
        var current = configuration.AlarmEditorRepeatMode;
        var color = current == AlarmRepeatMode.None ? Vector4.One : new Vector4(0.55f, 1.0f, 0.62f, 1f);
        var tooltip = current == AlarmRepeatMode.None
            ? T("Repeat")
            : string.Format(T("Repeat {0}"), T(AlarmConfigurationService.GetRepeatLabel(current)));

        if (DrawCreateAlarmIconButton("AlarmRepeatPicker", "\uf1da", color, tooltip))
            ImGui.OpenPopup("AlarmRepeatPickerPopup");

        DrawAlarmRepeatPickerPopupOnly();
    }






    private void DrawMaintenanceOverlayTitleRight()
    {
        var label = T("Maintenance Overlay");
        var x = ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize(label).X - ImGui.GetFrameHeight() - 4f;
        if (ImGui.GetCursorPosX() < x)
            ImGui.SetCursorPosX(x);

        DrawMaintenanceOverlayPopupButton();
    }

    private void DrawMaintenanceOverlayPopupButton()
    {
        if (ImGui.Button($"{T("Maintenance Overlay")}##MaintenanceOverlayMenu"))
            ImGui.OpenPopup("MaintenanceOverlayMenuPopup");

        if (ImGui.BeginPopup("MaintenanceOverlayMenuPopup"))
        {
            bool enabled = configuration.ShowMaintenanceOnOverlay;
            if (Checkbox(T("Enable"), ref enabled))
            {
                configuration.ShowMaintenanceOnOverlay = enabled;
                configuration.Save();
            }

            var scale = configuration.MaintenanceOverlayTextScale;
            ImGui.SetNextItemWidth(130f);
            if (ImGui.SliderFloat($"{T("Text Size")}##MaintenanceOverlayTextScalePopup", ref scale, 0.45f, 1.80f, "%.2f"))
            {
                configuration.MaintenanceOverlayTextScale = scale;
                configuration.Save();
            }

            var vertical = configuration.MaintenanceOverlayVerticalOffset;
            ImGui.SetNextItemWidth(130f);
            if (ImGui.SliderFloat($"{T("Text Position")}##MaintenanceOverlayVerticalOffsetPopup", ref vertical, -80f, 80f, "%.0f"))
            {
                configuration.MaintenanceOverlayVerticalOffset = vertical;
                configuration.Save();
            }

            ImGui.EndPopup();
        }
    }



    private void DrawAlarmMessageInput()
    {
        var message = configuration.AlarmEditorMessage;
        if (alarmEditorControlStartX > 0f)
            ImGui.SetCursorPosX(alarmEditorControlStartX);

        ImGui.SetNextItemWidth(alarmEditorControlWidth > 0f ? alarmEditorControlWidth : 240f);
        var alarmMessageBg = new Vector4(0.82f, 0.82f, 0.82f, 0.08f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, alarmMessageBg);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.82f, 0.82f, 0.82f, 0.11f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.82f, 0.82f, 0.82f, 0.13f));
        var changed = ImGui.InputText("##AlarmMessage", ref message, 128);
        ImGui.PopStyleColor(3);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var active = ImGui.IsItemActive();

        if (changed)
        {
            configuration.AlarmEditorMessage = message;
            configuration.Save();
        }

        if (!active && string.IsNullOrEmpty(message))
        {
            var disabled = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
            var label = T("Alarm Message");
            var y = min.Y + MathF.Floor((max.Y - min.Y - ImGui.GetTextLineHeight()) * 0.5f);
            var x = min.X + 8f;
            const string icon = "";

            using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
            {
                var font = ImGui.GetFont();
                var fontSize = ImGui.GetFontSize();
                ImGui.GetWindowDrawList().AddText(font, fontSize, new Vector2(x, y), ImGui.GetColorU32(disabled), icon);
                x += ImGui.CalcTextSize(icon).X + 8f;
            }

            ImGui.GetWindowDrawList().AddText(new Vector2(x, y), ImGui.GetColorU32(disabled), label);
        }
    }

    private void DrawAlarmSelectors(bool centerInAvailable = false)
    {
        var editorZone = GetAlarmEditorTimeZone();
        AlarmConfigurationService.RefreshEditorDateForLocalDay(configuration, editorZone);
        if (alarmSelectorRestoreScrollNextFrame)
        {
            ImGui.SetScrollY(alarmSelectorLockedScrollY);
            alarmSelectorRestoreScrollNextFrame = false;
        }

        alarmSelectorCapturedWheel = 0f;
        var selectorWheel = ImGui.GetIO().MouseWheel;
        if (alarmSelectorLastRectValid && MathF.Abs(selectorWheel) > 0.01f && ImGui.IsMouseHoveringRect(alarmSelectorLastMin, alarmSelectorLastMax, true))
        {
            alarmSelectorCapturedWheel = selectorWheel;
            alarmSelectorConsumedWheel = true;
            alarmSelectorHoveredThisFrame = true;
            alarmSelectorRestoreScrollNextFrame = true;
            ImGui.SetScrollY(alarmSelectorLockedScrollY);
            var io = ImGui.GetIO();
            io.MouseWheel = 0f;
            io.MouseWheelH = 0f;
        }

        alarmSelectorScrollY = ImGui.GetScrollY();
        var capturedSelectorWheel = MathF.Abs(alarmSelectorCapturedWheel) > 0.01f;
        alarmSelectorConsumedWheel = capturedSelectorWheel;
        alarmSelectorHoveredThisFrame = capturedSelectorWheel;

        if (chatAlarmSetupPending)
        {
            var wave = (float)((Math.Sin(ImGui.GetTime() * 4.0) + 1.0) * 0.5);
            var color = new Vector4(0.30f + 0.18f * wave, 1.0f, 0.42f + 0.28f * wave, 1.0f);
            ImGui.TextColored(color, T("Finish alarm setup from chat."));
        }

        var alarmEditorSelectionInPast = IsAlarmEditorInPast(editorZone);
        var pastTooltip = T("Can't create a alarm for the past!");

        var culture = ClockLocalizationService.GetCultureInfo(configuration.UiLanguageCultureName);
        var selectedDate = GetAlarmEditorSelectedDate(editorZone);
        var dateItems = BuildAlarmDateWheelItems(selectedDate, culture, out var selectedDateIndex);
        var dateLabels = dateItems.Select(d => d.ToString("dd MMM", culture)).ToArray();

        int hourRangeStart = TimeFormatHelper.UsesTwelveHourClock(configuration.TimeFormat) ? 1 : 0;
        int hourRangeCount = TimeFormatHelper.UsesTwelveHourClock(configuration.TimeFormat) ? 12 : 24;

        int visibleHour = configuration.AlarmEditorHour;
        if (TimeFormatHelper.UsesTwelveHourClock(configuration.TimeFormat))
        {
            if (visibleHour <= 0)
                visibleHour = 12;
            if (visibleHour > 12)
                visibleHour = ((visibleHour - 1) % 12) + 1;
        }

        visibleHour = Math.Clamp(visibleHour, hourRangeStart, hourRangeStart + hourRangeCount - 1);
        var hourIndex = visibleHour - hourRangeStart;
        var hourItems = Enumerable.Range(hourRangeStart, hourRangeCount).Select(h => h.ToString("00")).ToArray();

        var minuteIndex = Math.Clamp(configuration.AlarmEditorMinute, 0, 59);
        var minuteItems = Enumerable.Range(0, 60).Select(m => m.ToString("00")).ToArray();

        var style = ImGui.GetStyle();
        var startX = ImGui.GetCursorPosX();
        var hourWidth = 86f;
        var minuteWidth = 86f;
        var hasMeridiem = TimeFormatHelper.UsesTwelveHourClock(configuration.TimeFormat);
        var meridiemWidth = hasMeridiem ? 88f : 0f;
        var dateWidth = 106f;
        var spacing = style.ItemSpacing.X;
        var pickerHeight = 124f;

        if (centerInAvailable)
        {
            var available = ImGui.GetContentRegionAvail().X;
            var slotCount = hasMeridiem ? 4f : 3f;
            var gapCount = slotCount - 1f;
            var baseWidth = hourWidth + minuteWidth + (hasMeridiem ? meridiemWidth : 0f) + dateWidth;
            var scale = Math.Clamp((available - spacing * gapCount) / MathF.Max(1f, baseWidth), 0.72f, 1f);
            hourWidth = MathF.Floor(hourWidth * scale);
            minuteWidth = MathF.Floor(minuteWidth * scale);
            meridiemWidth = hasMeridiem ? MathF.Floor(meridiemWidth * scale) : 0f;
            dateWidth = MathF.Floor(dateWidth * scale);
        }

        var timeWidth = hourWidth + spacing + minuteWidth + (hasMeridiem ? spacing + meridiemWidth : 0f);
        var totalWidth = timeWidth + spacing + dateWidth;
        if (centerInAvailable)
            startX += MathF.Max(0f, (ImGui.GetContentRegionAvail().X - totalWidth) * 0.5f);

        alarmEditorControlStartX = startX;
        alarmEditorPanelWidth = timeWidth;
        alarmEditorControlWidth = totalWidth;

        var wheelY = ImGui.GetCursorPosY() + 4f;
        ImGui.SetCursorPos(new Vector2(startX, wheelY));

        var pickedHour = hourIndex;
        if (DrawAlarmWheelSelector("AlarmHourWheel", hourItems, ref pickedHour, hourWidth, T("Hour"), pickerHeight, false, null, 2, alarmEditorSelectionInPast, pastTooltip))
        {
            configuration.AlarmEditorHour = int.Parse(hourItems[pickedHour], CultureInfo.InvariantCulture);
            configuration.Save();
        }
        var hourMin = ImGui.GetItemRectMin();
        var hourMax = ImGui.GetItemRectMax();

        ImGui.SameLine();

        var pickedMinute = minuteIndex;
        if (DrawAlarmWheelSelector("AlarmMinuteWheel", minuteItems, ref pickedMinute, minuteWidth, T("Minute"), pickerHeight, false, null, 2, alarmEditorSelectionInPast, pastTooltip))
        {
            configuration.AlarmEditorMinute = pickedMinute;
            configuration.Save();
        }
        var minuteMin = ImGui.GetItemRectMin();
        var minuteMax = ImGui.GetItemRectMax();

        Vector2? meridiemMin = null;
        Vector2? meridiemMax = null;
        if (hasMeridiem)
        {
            ImGui.SameLine();
            var meridiemOptions = new[] { "AM", "PM" };
            var meridiemIndex = configuration.AlarmEditorIsPm ? 1 : 0;
            if (DrawAlarmWheelSelector("AlarmMeridiemWheel", meridiemOptions, ref meridiemIndex, meridiemWidth, T("AM/PM"), pickerHeight, false, null, 2, alarmEditorSelectionInPast, pastTooltip))
            {
                configuration.AlarmEditorIsPm = meridiemIndex == 1;
                configuration.Save();
            }
            meridiemMin = ImGui.GetItemRectMin();
            meridiemMax = ImGui.GetItemRectMax();
        }

        ImGui.SameLine();
        var pickedDateIndex = selectedDateIndex;
        if (DrawAlarmWheelSelector("AlarmDateWheel", dateLabels, ref pickedDateIndex, dateWidth, T("Date"), pickerHeight, true, "AlarmCalendarPopup", 2, alarmEditorSelectionInPast, pastTooltip))
        {
            SetAlarmEditorDate(dateItems[pickedDateIndex]);
            configuration.Save();
        }
        var dateMin = ImGui.GetItemRectMin();
        var dateMax = ImGui.GetItemRectMax();

        DrawAlarmCalendarPopup(editorZone, culture);

        var drawList = ImGui.GetWindowDrawList();
        var disabledColor = ImGui.GetColorU32(ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        var shadeRight = hasMeridiem && meridiemMax.HasValue ? meridiemMax.Value.X : minuteMax.X;
        var shadeMin = new Vector2(hourMin.X, hourMin.Y + pickerHeight * 0.5f - ImGui.GetTextLineHeight() * 0.78f);
        var shadeMax = new Vector2(shadeRight, hourMin.Y + pickerHeight * 0.5f + ImGui.GetTextLineHeight() * 0.78f);
        drawList.AddRectFilled(shadeMin, shadeMax, ImGui.GetColorU32(new Vector4(0.82f, 0.82f, 0.82f, 0.08f)), 3f);

        DrawCenteredSmallLabel(drawList, T("Hour"), hourMin.X, hourMax.X, hourMax.Y + 3f, disabledColor);
        DrawCenteredSmallLabel(drawList, T("Mins"), minuteMin.X, minuteMax.X, minuteMax.Y + 3f, disabledColor);

        var tzText = TimeZoneHelper.ToShortText(editorZone);
        var formatText = T(TimeFormatHelper.UsesTwelveHourClock(configuration.TimeFormat) ? "12hour" : "24hour");
        var meridiemLabelMinX = hasMeridiem && meridiemMin.HasValue ? meridiemMin.Value.X : minuteMin.X;
        var meridiemLabelMaxX = hasMeridiem && meridiemMax.HasValue ? meridiemMax.Value.X : minuteMax.X;
        DrawCenteredSmallLabel(drawList, formatText, meridiemLabelMinX, meridiemLabelMaxX, MathF.Max(minuteMax.Y, meridiemMax?.Y ?? minuteMax.Y) + 3f, disabledColor);
        DrawCenteredSmallLabel(drawList, tzText, dateMin.X, dateMax.X, dateMax.Y + 3f, disabledColor);
        var tzTextSize = ImGui.CalcTextSize(tzText);
        var tzHoverMin = new Vector2(dateMin.X + ((dateMax.X - dateMin.X) - tzTextSize.X) * 0.5f, dateMax.Y + 3f);
        var tzHoverMax = tzHoverMin + new Vector2(tzTextSize.X, ImGui.GetTextLineHeight());
        if (ImGui.IsMouseHoveringRect(tzHoverMin, tzHoverMax))
            ImGui.SetTooltip(T("Change it on General."));

        alarmSelectorLastMin = new Vector2(hourMin.X, MathF.Min(hourMin.Y, dateMin.Y));
        alarmSelectorLastMax = new Vector2(dateMax.X, dateMax.Y + ImGui.GetTextLineHeight() + 6f);
        alarmSelectorLastRectValid = true;

        var rowBottom = MathF.Max(dateMax.Y, MathF.Max(minuteMax.Y, hasMeridiem && meridiemMax.HasValue ? meridiemMax.Value.Y : minuteMax.Y));
        var bottom = rowBottom + ImGui.GetTextLineHeight() * 2.1f;
        ImGui.SetCursorScreenPos(new Vector2(startX, bottom));

        if (alarmSelectorConsumedWheel)
        {
            ImGui.SetScrollY(alarmSelectorLockedScrollY);
            alarmSelectorScrollY = alarmSelectorLockedScrollY;
            alarmSelectorRestoreScrollNextFrame = true;
        }
        else
        {
            alarmSelectorLockedScrollY = ImGui.GetScrollY();
        }

        void DrawCenteredSmallLabel(ImDrawListPtr list, string text, float left, float right, float y, uint color)
        {
            var textSize = ImGui.CalcTextSize(text);
            list.AddText(new Vector2(left + ((right - left) - textSize.X) * 0.5f, y), color, text);
        }
    }

    private DateTime GetAlarmEditorSelectedDate(string editorZone)
    {
        if (!string.IsNullOrWhiteSpace(configuration.AlarmEditorDateOverrideText) &&
            DateTime.TryParseExact(configuration.AlarmEditorDateOverrideText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var picked))
            return picked.Date;

        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, editorZone);
        var day = Math.Clamp(configuration.AlarmEditorDay <= 0 ? zoneNow.Day : configuration.AlarmEditorDay, 1, DateTime.DaysInMonth(zoneNow.Year, zoneNow.Month));
        return new DateTime(zoneNow.Year, zoneNow.Month, day);
    }

    private DateTime[] BuildAlarmDateWheelItems(DateTime selectedDate, CultureInfo culture, out int selectedIndex)
    {
        if (alarmDateWheelAnchor == DateTime.MinValue)
            alarmDateWheelAnchor = selectedDate.Date;

        var start = alarmDateWheelAnchor.AddDays(-365);
        var end = alarmDateWheelAnchor.AddDays(365);
        if (selectedDate.Date < start || selectedDate.Date > end)
        {
            alarmDateWheelAnchor = selectedDate.Date;
            start = alarmDateWheelAnchor.AddDays(-365);
        }

        var items = Enumerable.Range(0, 731).Select(i => start.AddDays(i).Date).ToArray();
        selectedIndex = Math.Clamp(Array.FindIndex(items, d => d.Date == selectedDate.Date), 0, items.Length - 1);
        return items;
    }

    private void SetAlarmEditorDate(DateTime date)
    {
        var picked = date.Date;
        configuration.AlarmEditorDay = picked.Day;
        configuration.AlarmEditorDateOverrideText = picked.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        alarmCalendarVisibleMonth = new DateTime(picked.Year, picked.Month, 1);
        if (alarmDateWheelAnchor == DateTime.MinValue || Math.Abs((picked - alarmDateWheelAnchor).TotalDays) > 365)
            alarmDateWheelAnchor = picked;
    }

    private void DrawAlarmCalendarPopup(string editorZone, CultureInfo culture)
    {
        var selectedDate = GetAlarmEditorSelectedDate(editorZone);
        if (alarmCalendarVisibleMonth == DateTime.MinValue)
            alarmCalendarVisibleMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);

        if (!ImGui.BeginPopup("AlarmCalendarPopup"))
            return;

        var monthText = alarmCalendarVisibleMonth.ToString("MMMM yyyy", culture);
        if (ImGui.SmallButton("‹##AlarmCalendarPrev"))
            alarmCalendarVisibleMonth = alarmCalendarVisibleMonth.AddMonths(-1);

        ImGui.SameLine();
        var headerWidth = 150f;
        var headerX = ImGui.GetCursorPosX() + MathF.Max(0f, (headerWidth - ImGui.CalcTextSize(monthText).X) * 0.5f);
        ImGui.SetCursorPosX(headerX);
        ImGui.TextUnformatted(monthText);
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
        if (ImGui.SmallButton("›##AlarmCalendarNext"))
            alarmCalendarVisibleMonth = alarmCalendarVisibleMonth.AddMonths(1);

        ImGui.Spacing();

        if (ImGui.Button($"{T("Today")}##AlarmCalendarToday"))
        {
            var today = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, editorZone).Date;
            SetAlarmEditorDate(today);
            configuration.Save();
            ImGui.CloseCurrentPopup();
        }

        ImGui.Spacing();

        var firstDay = new DateTime(alarmCalendarVisibleMonth.Year, alarmCalendarVisibleMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(alarmCalendarVisibleMonth.Year, alarmCalendarVisibleMonth.Month);
        var firstOffset = (int)firstDay.DayOfWeek;
        var cell = new Vector2(32f, 24f);
        var dayNames = culture.DateTimeFormat.AbbreviatedDayNames;

        for (var i = 0; i < 7; i++)
        {
            ImGui.TextDisabled(dayNames[i]);
            if (i < 6)
                ImGui.SameLine();
        }

        for (var slot = 0; slot < 42; slot++)
        {
            if (slot % 7 != 0)
                ImGui.SameLine();

            var day = slot - firstOffset + 1;
            if (day < 1 || day > daysInMonth)
            {
                ImGui.Dummy(cell);
                continue;
            }

            var thisDate = new DateTime(alarmCalendarVisibleMonth.Year, alarmCalendarVisibleMonth.Month, day);
            var selected = thisDate.Date == selectedDate.Date;
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, GoldTextColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.90f, 0.55f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.82f, 0.58f, 0.16f, 1f));
            }

            if (ImGui.Button($"{day:00}##AlarmCalendarDay{day}", cell))
            {
                SetAlarmEditorDate(thisDate);
                configuration.Save();
                ImGui.CloseCurrentPopup();
            }

            if (selected)
                ImGui.PopStyleColor(3);
        }

        ImGui.EndPopup();
    }

    private bool DrawAlarmWheelSelector(string id, string[] items, ref int index, float width, string tooltip, float height = 78f, bool clickOpensPopup = false, string? popupId = null, int visibleRadius = 1, bool selectedInvalid = false, string? invalidTooltip = null)
    {
        if (items.Length == 0)
            return false;

        index = Math.Clamp(index, 0, items.Length - 1);
        visibleRadius = Math.Clamp(visibleRadius, 1, 2);

        int WrapIndex(int value)
        {
            var wrapped = value % items.Length;
            return wrapped < 0 ? wrapped + items.Length : wrapped;
        }
        var size = new Vector2(width, height);
        var label = $"##{id}";

        ImGui.InvisibleButton(label, size);
        var itemHovered = ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var hovered = itemHovered || ImGui.IsMouseHoveringRect(min, max, true);
        var drawList = ImGui.GetWindowDrawList();
        var style = ImGui.GetStyle();
        var center = new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f);
        var lineHeight = ImGui.GetTextLineHeight();
        var rowGap = visibleRadius > 1 ? MathF.Min(23f, height * 0.19f) : MathF.Min(28f, height * 0.32f);
        var changed = false;
        var nextIndex = index;

        if (hovered)
        {
            alarmSelectorHoveredThisFrame = true;
            var wheel = MathF.Abs(alarmSelectorCapturedWheel) > 0.01f ? alarmSelectorCapturedWheel : ImGui.GetIO().MouseWheel;
            if (wheel > 0f)
                nextIndex = (index - 1 + items.Length) % items.Length;
            else if (wheel < 0f)
                nextIndex = (index + 1) % items.Length;
            else if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                if (clickOpensPopup && !string.IsNullOrWhiteSpace(popupId))
                    ImGui.OpenPopup(popupId);
                else
                    nextIndex = ImGui.GetMousePos().Y < center.Y ? (index - 1 + items.Length) % items.Length : (index + 1) % items.Length;
            }

            if (MathF.Abs(wheel) > 0.01f)
            {
                alarmSelectorConsumedWheel = true;
                alarmSelectorRestoreScrollNextFrame = true;
                ImGui.SetScrollY(alarmSelectorLockedScrollY);
                var io = ImGui.GetIO();
                io.MouseWheel = 0f;
                io.MouseWheelH = 0f;
                alarmSelectorCapturedWheel = 0f;
            }
        }

        if (nextIndex != index)
        {
            nextIndex = WrapIndex(nextIndex);
            alarmWheelAnimations[id] = new AlarmWheelAnimation
            {
                From = index,
                To = nextIndex,
                StartedAt = ImGui.GetTime()
            };

            index = nextIndex;
            changed = true;
        }

        var textColor = style.Colors[(int)ImGuiCol.Text];
        var invalidColor = new Vector4(1f, 0.42f, 0.42f, 1f);
        var centerColor = selectedInvalid ? invalidColor : textColor;
        var muted = style.Colors[(int)ImGuiCol.TextDisabled];

        drawList.PushClipRect(min, max, true);

        var now = ImGui.GetTime();
        if (alarmWheelAnimations.TryGetValue(id, out var anim))
        {
            const double duration = 0.34;
            var p = Math.Clamp((float)((now - anim.StartedAt) / duration), 0f, 1f);
            var eased = 1f - MathF.Pow(1f - p, 3f);
            var from = WrapIndex(anim.From);
            var to = WrapIndex(anim.To);
            var dir = to == WrapIndex(from + 1) ? 1f : -1f;
            var extra = visibleRadius + 2;

            for (var offset = -extra; offset <= extra; offset++)
            {
                var itemIndex = WrapIndex(from + offset);
                var y = center.Y + rowGap * (offset - dir * eased);
                if (y < min.Y - rowGap || y > max.Y + rowGap)
                    continue;

                var distance = MathF.Abs((y - center.Y) / rowGap);
                var scale = Math.Clamp(1.12f - distance * 0.23f, 0.62f, 1.12f);
                var alpha = Math.Clamp(1.0f - distance * 0.28f, 0.26f, 1.0f);
                var color = distance < 0.46f ? centerColor : WithAlpha(muted, muted.W * alpha);
                DrawAlarmWheelText(items[itemIndex], new Vector2(center.X, y), color, scale);
            }

            if (p >= 1f)
                alarmWheelAnimations.Remove(id);
        }
        else
        {
            for (var offset = -visibleRadius; offset <= visibleRadius; offset++)
            {
                var itemIndex = WrapIndex(index + offset);
                var abs = Math.Abs(offset);
                var scale = offset == 0 ? 1.10f : abs == 1 ? 0.82f : 0.66f;
                var color = offset == 0 ? centerColor : WithAlpha(muted, muted.W * (abs == 1 ? 0.78f : 0.48f));
                DrawAlarmWheelText(items[itemIndex], center + new Vector2(0f, rowGap * offset), color, scale);
            }
        }

        drawList.PopClipRect();

        if (hovered)
            ImGui.SetTooltip(selectedInvalid && !string.IsNullOrWhiteSpace(invalidTooltip) ? invalidTooltip : tooltip);

        return changed;

        Vector4 WithAlpha(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, Math.Clamp(alpha, 0f, 1f));

        void DrawAlarmWheelText(string text, Vector2 pos, Vector4 color, float scale)
        {
            using (plugin.PushClockTimeFont(ClockTimeTextFont.Digital))
            {
                var font = ImGui.GetFont();
                var fontSize = ImGui.GetFontSize() * scale;
                var textSize = ImGui.CalcTextSize(text) * scale;
                var p = new Vector2(pos.X - textSize.X * 0.5f, pos.Y - lineHeight * scale * 0.5f);
                drawList.AddText(font, fontSize, p, ImGui.GetColorU32(color), text);
            }
        }
    }

    private string GetAlarmEditorTimeZone()
    {
        if (editingAlarmId.HasValue || chatAlarmSetupPending)
            return editingAlarmTimeZoneId ?? configuration.SelectedTimeZoneId;

        return configuration.SelectedTimeZoneId;
    }

    private bool IsAlarmEditorInPast(string editorZone)
    {
        var text = AlarmConfigurationService.BuildEditorDateTimeText(configuration, editorZone);
        if (!TimeZoneHelper.TryParseInZone(text, editorZone, out var utc))
            return true;

        return utc <= DateTime.UtcNow;
    }

    private void LoadAlarmIntoEditor(AlarmEntry alarm)
    {
        editingAlarmId = alarm.Id;
        editingAlarmTimeZoneId = alarm.GetEffectiveTimeZoneId();

        if (!TimeZoneHelper.TryParseInZone(alarm.DateTimeText, alarm.GetEffectiveTimeZoneId(), out var alarmUtc))
            return;

        var alarmLocal = TimeZoneHelper.ConvertFromUtc(alarmUtc, alarm.GetEffectiveTimeZoneId());
        configuration.AlarmEditorDay = alarmLocal.Day;
        configuration.AlarmEditorMinute = alarmLocal.Minute;
        configuration.AlarmEditorMessage = alarm.Message ?? "";
        configuration.AlarmEditorSnoozeEnabled = alarm.SnoozeEnabled;
        configuration.AlarmEditorSnoozeMinutes = Math.Clamp(alarm.SnoozeMinutes <= 0 ? 5 : alarm.SnoozeMinutes, 1, 120);
        configuration.AlarmEditorRepeatMode = alarm.RepeatMode;

        if (TimeFormatHelper.UsesTwelveHourClock(configuration.TimeFormat))
        {
            configuration.AlarmEditorIsPm = alarmLocal.Hour >= 12;
            var hour12 = alarmLocal.Hour % 12;
            configuration.AlarmEditorHour = hour12 == 0 ? 12 : hour12;
        }
        else
        {
            configuration.AlarmEditorHour = alarmLocal.Hour;
        }

        configuration.Save();
    }

    private void ClearAlarmEditingState()
    {
        editingAlarmId = null;
        editingAlarmTimeZoneId = null;
    }











    private void DrawCommandsTab()
    {
        ImGui.PushTextWrapPos();

        ImGui.TextColored(new Vector4(1f, 0.85f, 0.45f, 1f), T("Slash Commands"));
        DrawFadeSeparator();
        ImGui.Spacing();

        DrawCommandLine("/clock", "Open settings");
        DrawCommandLine("/clock on", "Show the clock overlay");
        DrawCommandLine("/clock off", "Hide the clock overlay");
        DrawCommandLine("/alarms", "Open alarm overlay");
        DrawCommandLine("/clock timezone <TimeZoneInfo ID or alias>", "Change the main clock timezone");
        DrawCommandLine("/clock format 12|24|12s|24s|weekday|date", "Change time format preset");
        DrawCommandLine("/clock colon default|always|hidden|slow|fast", "Change colon animation");
        DrawCommandLine("/clock layout horizontal|vertical", "Change active profile layout");
        DrawCommandLine("/clock <timezone1> to <timezone2>", "Compare the current time between two timezones");
        DrawCommandLine("/clock lock | /clock unlock", "Lock or unlock clock movement");
        DrawCommandLine("/clock profile next|list|set <n>|add <name>|rename <name>|delete", "Manage profiles");

        ImGui.PopTextWrapPos();
    }


    private void DrawPluginConfigTab()
    {
        ImGui.SetCursorPosX(ImGui.GetStyle().WindowPadding.X);
        ImGui.Spacing();
        ImGui.SetCursorPosX(ImGui.GetStyle().WindowPadding.X);
        ImGui.TextWrapped(T("Export/Import 'Clock' plugin configuration containing all configuration/customizations/Options etc"));

        ImGui.Spacing();

        if (ImGui.Button(T("Export"), new Vector2(86f, 0f)))
            OpenExportDialog();

        ImGui.SameLine();

        if (ImGui.Button(T("Import"), new Vector2(86f, 0f)))
            OpenImportDialog();
    }

    private void OpenExportDialog()
    {
        fileDialogManager.SaveFileDialog(
            "Export Clock configuration",
            "Text files{.txt},.*",
            "ClockConfiguration",
            ".txt",
            (success, path) =>
            {
                if (!success || string.IsNullOrWhiteSpace(path))
                    return;

                if (plugin.TryExportConfiguration(path, out var error))
                    ShowConfigurationPopup(T("Plugin configuration exported with success!"));
                else
                    ShowConfigurationPopup(string.Format(T("Failed to export plugin configuration: {0}"), error));
            });
    }

    private void OpenImportDialog()
    {
        fileDialogManager.OpenFileDialog(
            "Import Clock configuration",
            "Text files{.txt},.*",
            (success, path) =>
            {
                if (!success || string.IsNullOrWhiteSpace(path))
                    return;

                if (plugin.TryImportConfiguration(path, out var error))
                {
                    RefreshLocalUiStateFromConfiguration();
                    ShowConfigurationPopup(T("Plugin configuration imported with success!"));
                }
                else
                {
                    ShowConfigurationPopup(string.Format(T("Failed to import plugin configuration: {0}"), error));
                }
            });
    }

    private void RefreshLocalUiStateFromConfiguration()
    {
        textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
        presetSelection = configuration.PreviewPresetSelection;
        editingAlarmId = null;
        editingAlarmTimeZoneId = null;
        timeZoneFilter = "";
        countryTimeZoneFilter = "";
        selectedCountryTimeZone = null;
    }

    private void ShowConfigurationPopup(string message)
    {
        configurationPopupMessage = message;
        openConfigurationPopupNextFrame = true;
    }

    private void DrawConfigurationResultPopup()
    {
        const string popupId = "ClockConfigurationResultPopup";

        if (openConfigurationPopupNextFrame)
        {
            var center = ImGui.GetWindowPos() + (ImGui.GetWindowSize() * 0.5f);
            ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(360f, 95f), ImGuiCond.Appearing);
            ImGui.OpenPopup(popupId);
            openConfigurationPopupNextFrame = false;
        }

        if (ImGui.BeginPopup(popupId))
        {
            var messageSize = ImGui.CalcTextSize(configurationPopupMessage);
            var availableWidth = ImGui.GetContentRegionAvail().X;
            if (messageSize.X < availableWidth)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availableWidth - messageSize.X) * 0.5f));

            ImGui.TextWrapped(configurationPopupMessage);
            ImGui.Spacing();

            var buttonSize = new Vector2(80f, 0f);
            ImGui.SetCursorPosX((ImGui.GetWindowSize().X - buttonSize.X) * 0.5f);
            if (ImGui.Button(T("Ok"), buttonSize))
                ImGui.CloseCurrentPopup();

            ImGui.EndPopup();
        }
    }

    private void DrawCommandLine(string command, string description)
    {
        ImGui.Bullet();
        ImGui.TextColored(new Vector4(0.75f, 0.90f, 1f, 1f), command);
        ImGui.Indent(22f);
        ImGui.TextDisabled(T(description));
        ImGui.Unindent(22f);
        ImGui.Spacing();
    }

    private void DrawTextSizeControl(string? undoKey = null, ClockProfile? undoBefore = null)
    {
        var profile = configuration.GetActiveProfile();

        ImGui.SetNextItemWidth(AppearanceSliderWidth);
        float scale = profile.ClockTextScale;
        if (DrawEditableSliderFloat("Text Size", ref scale, 0.5f, 5.0f, "%.2f", "MainTextSize", undoKey, undoBefore))
        {
            profile.ClockTextScale = scale;
            textSizeInputValue = scale;
            configuration.Save();
        }
    }



    private bool DrawEditableSliderFloat(string label, ref float value, float min, float max, string format, string? id = null, string? undoKey = null, ClockProfile? undoBefore = null)
    {
        var key = id ?? label;
        if (editingSliderId == key)
        {
            if (focusSliderInputNextFrame)
            {
                ImGui.SetKeyboardFocusHere();
                focusSliderInputNextFrame = false;
            }

            float inputValue = editingSliderValue;
            bool pressedEnter = ImGui.InputFloat($"{T(label)}##{key}Input", ref inputValue, 0.0f, 0.0f, format, ImGuiInputTextFlags.EnterReturnsTrue);
            editingSliderValue = inputValue;

            if (pressedEnter || ImGui.IsItemDeactivated())
            {
                value = Math.Clamp(editingSliderValue, min, max);
                editingSliderId = null;

                if (undoKey != null && sliderEditUndoStart.TryGetValue(key, out var beforeEdit))
                {
                    SaveAppearanceChange(undoKey, beforeEdit);
                    sliderEditUndoStart.Remove(key);
                }

                return true;
            }

            return false;
        }

        bool changed = ImGui.SliderFloat($"{T(label)}##{key}", ref value, min, max, format);

        if (undoKey != null && undoBefore != null && ImGui.IsItemActivated())
            sliderEditUndoStart[key] = undoBefore.Clone();

        if (undoKey != null && ImGui.IsItemDeactivatedAfterEdit())
        {
            if (sliderEditUndoStart.TryGetValue(key, out var beforeEdit))
            {
                SaveAppearanceChange(undoKey, beforeEdit);
                sliderEditUndoStart.Remove(key);
            }
        }

        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            editingSliderId = key;
            editingSliderValue = value;
            focusSliderInputNextFrame = true;
            if (undoKey != null && undoBefore != null && !sliderEditUndoStart.ContainsKey(key))
                sliderEditUndoStart[key] = undoBefore.Clone();
        }

        return changed;
    }

    private void Section(string title, Action drawContent, Action? drawTitleRight = null, bool drawTopDivider = true)
    {
        ImGui.Spacing();
        if (drawTopDivider)
        {
            DrawFadeSeparator();
            ImGui.Spacing();
        }

        ImGui.TextColored(new Vector4(1f, 0.82f, 0.42f, 1f), title);
        if (drawTitleRight != null)
        {
            ImGui.SameLine();
            drawTitleRight();
        }

        ImGui.Spacing();
        drawContent();
        ImGui.Spacing();
    }

    private static void DrawAppearanceSeparator()
    {
        ImGui.Spacing();
        DrawFadeSeparator();
        ImGui.Spacing();
    }

    private bool Checkbox(string label, ref bool value)
    {
        var changed = ImGui.Checkbox(label, ref value);
        if (!value)
            DrawUncheckedCheckboxMarker();

        return changed;
    }

    private void DrawUncheckedCheckboxMarker()
    {
        const string icon = "";
        var min = ImGui.GetItemRectMin();
        var size = ImGui.GetFrameHeight();
        var markerColor = new Vector4(0.78f, 0.78f, 0.78f, 0.40f);

        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var font = ImGui.GetFont();
            var markerScale = 0.86f;
            var fontSize = ImGui.GetFontSize() * markerScale;
            var iconSize = ImGui.CalcTextSize(icon) * markerScale;
            var pos = new Vector2(
                min.X + MathF.Floor((size - iconSize.X) * 0.5f),
                min.Y + MathF.Floor((size - iconSize.Y) * 0.5f));
            ImGui.GetWindowDrawList().AddText(font, fontSize, pos, ImGui.GetColorU32(markerColor), icon);
        }
    }

    private static void DrawFadeSeparator()
    {
        var width = ImGui.GetContentRegionAvail().X;
        if (width <= 1f)
        {
            ImGui.Dummy(new Vector2(0f, 4f));
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var y = pos.Y + 4f;
        var styleColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Separator];
        if (styleColor.W <= 0f)
            styleColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];

        const int pieces = 32;
        for (var i = 0; i < pieces; i++)
        {
            var t0 = i / (float)pieces;
            var t1 = (i + 1) / (float)pieces;
            var mid = (t0 + t1) * 0.5f;
            var alpha = MathF.Sin(mid * MathF.PI) * styleColor.W;
            var color = new Vector4(styleColor.X, styleColor.Y, styleColor.Z, alpha);
            drawList.AddLine(
                new Vector2(pos.X + (width * t0), y),
                new Vector2(pos.X + (width * t1), y),
                ImGui.GetColorU32(color),
                1f);
        }

        ImGui.Dummy(new Vector2(width, 9f));
    }

    private static void Help(string text)
    {
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(text);
        ImGui.PopTextWrapPos();
    }

    private bool DrawCombo(string label, string[] items, ref int currentIndex, bool translateItems = true)
    {
        bool changed = false;

        if (items.Length == 0)
            return false;

        currentIndex = Math.Clamp(currentIndex, 0, items.Length - 1);
        var preview = translateItems ? T(items[currentIndex]) : items[currentIndex];

        if (ImGui.BeginCombo(T(label), preview))
        {
            for (int i = 0; i < items.Length; i++)
            {
                bool isSelected = i == currentIndex;
                var itemText = translateItems ? T(items[i]) : items[i];
                if (ImGui.Selectable(itemText, isSelected))
                {
                    currentIndex = i;
                    changed = true;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        return changed;
    }












}
