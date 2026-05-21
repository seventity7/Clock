using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
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
        Alarms
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
    private static readonly Vector4 GoldTextColor = new(1f, 0.82f, 0.42f, 1f);
    private string timeZoneFilter = "";
    private string chatTimestampTimeZoneFilter = "";
    private string countryTimeZoneFilter = "";
    private CountryTimeZoneOption? selectedCountryTimeZone;
    private bool favoriteTimezonesExpanded = true;
    private Vector2 reenabledPopupPosition;
    private bool openReenabledPopupNextFrame;
    private string alarmActionPopupMessage = "";
    private string configurationPopupMessage = "";
    private bool openConfigurationPopupNextFrame;
    private string languageFilter = "";

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
        requestedTab = ConfigTabRequest.Alarms;
        IsOpen = true;
    }

    public override void PreDraw()
    {
    }

    public override void Draw()
    {
        var windowSize = ImGui.GetWindowSize();
        DrawTopButtons(windowSize);

        ImGui.TextColored(new Vector4(1f, 0.88f, 0.55f, 1f), "Clock");
        ImGui.SameLine();
        ImGui.TextDisabled(T("Advanced Settings"));
        ImGui.Separator();
        ImGui.Spacing();

        var contentHeight = ImGui.GetContentRegionAvail().Y;
        if (ImGui.BeginChild("##ClockSettingsContent", new Vector2(0f, contentHeight), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            DrawProfileHeader();

            if (ImGui.BeginTabBar("ClockTabs"))
            {
                if (ImGui.BeginTabItem(T("General")))
                {
                    DrawGeneralTab();
                    ImGui.EndTabItem();
                }

                var alarmsTabFlags = requestedTab == ConfigTabRequest.Alarms
                    ? ImGuiTabItemFlags.SetSelected
                    : ImGuiTabItemFlags.None;

                if (ImGui.BeginTabItem(T("Alarms"), alarmsTabFlags))
                {
                    requestedTab = ConfigTabRequest.None;
                    DrawAlarmsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(T("Profiles")))
                {
                    DrawProfilesTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(T("Appearance")))
                {
                    DrawAppearanceTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(T("Commands")))
                {
                    DrawCommandsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(T("Plugin Config"), ImGuiTabItemFlags.Trailing))
                {
                    DrawPluginConfigTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }
        ImGui.EndChild();

        DrawConfigurationResultPopup();
        fileDialogManager.Draw();
    }

    public override void PostDraw()
    {
    }

    private void DrawTopButtons(Vector2 windowSize)
    {
        var savedCursor = ImGui.GetCursorPos();

        var currentLanguageName = ClockLocalizationService.GetCultureDisplayName(configuration.UiLanguageCultureName);
        var languageButtonSize = new Vector2(Math.Clamp(ImGui.CalcTextSize(currentLanguageName).X + 18f, 78f, 148f), 21f);
        var helpButtonSize = new Vector2(58, 21);
        var closeButtonSize = new Vector2(24, 21);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetCursorPos(new Vector2(windowSize.X - languageButtonSize.X - helpButtonSize.X - closeButtonSize.X - (spacing * 2f) - 8f, 4));

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
            IsOpen = false;
            ImGui.SetCursorPos(savedCursor);
            return;
        }
        ImGui.SetCursorPos(savedCursor);
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

        ImGui.Separator();

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

        ImGui.Spacing();
    }

    private static bool IsUserProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var lowered = name.Trim().ToLowerInvariant();
        return lowered is not ("default" or "minimal" or "gold hud" or "retro panel" or "retro" or "classic");
    }

    private void DrawGeneralTab()
    {
        Section(T("Behavior"), () =>
        {
            var stick = !configuration.IsConfigWindowMovable;
            if (ImGui.Checkbox(T("Stick clock"), ref stick))
            {
                configuration.IsConfigWindowMovable = !stick;
                configuration.Save();
            }
            Help(T("Locks or unlocks movement/resizing of the clock window."));

            bool autoStart = configuration.AutoStart;
            if (ImGui.Checkbox(T("Auto Start"), ref autoStart))
            {
                configuration.AutoStart = autoStart;
                configuration.Save();
            }
            Help(T("Automatically opens the clock after login."));

            bool hideDuringCutscenes = configuration.HideDuringCutscenes;
            if (ImGui.Checkbox(T("Hide during cutscenes"), ref hideDuringCutscenes))
            {
                configuration.HideDuringCutscenes = hideDuringCutscenes;
                configuration.Save();
            }
            Help(T("Hides only the clock during cutscenes."));

        });

        Section(T("Time Display"), () =>
        {
            DrawCompactTimezoneCombo();
            DrawCompactFormatCombo();

            var profile = configuration.GetActiveProfile();
            bool showLocalTime = profile.ShowLocalTime;
            if (ImGui.Checkbox(T("Show Local Time"), ref showLocalTime))
            {
                profile.ShowLocalTime = showLocalTime;
                configuration.Save();
            }

            DrawFavoriteTimezonesSection();
        });

        Section(T("Chat Timestamp"), () =>
        {
            DrawChatTimestampSettings();
        }, DrawChatTimestampHeaderRight);
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
            ImGui.TextUnformatted(T("Recommend to turn off any similar feature"));
            ImGui.TextUnformatted(T("from other plugins to avoid any issues."));
            ImGui.EndTooltip();
        }
    }


    private void DrawChatTimestampSettings()
    {
        bool showCustomTimestamp = configuration.ShowCustomTimestampInChat;
        if (ImGui.Checkbox(T("Show custom timestamp"), ref showCustomTimestamp))
        {
            configuration.ShowCustomTimestampInChat = showCustomTimestamp;
            configuration.SanitizeChatTimestampOptions();
            configuration.Save();
            plugin.RefreshChatTimestampSettings();
        }

        ImGui.SameLine();

        bool showAmPm = configuration.ChatTimestampShowAmPm;
        if (ImGui.Checkbox(T("Show AM/PM"), ref showAmPm))
        {
            configuration.ChatTimestampShowAmPm = showAmPm;
            configuration.Save();
            plugin.RefreshChatTimestampSettings();
        }

        ImGui.Indent();
        if (!configuration.ShowCustomTimestampInChat)
            ImGui.BeginDisabled();

        bool useCustomColor = configuration.ChatTimestampUseCustomColor;
        if (ImGui.Checkbox(T("Custom color"), ref useCustomColor))
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

        if (!configuration.ShowCustomTimestampInChat)
            ImGui.EndDisabled();
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
            ImGui.Separator();

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

            ImGui.Separator();

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

                ImGui.Separator();
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
            ImGui.Separator();

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

                ImGui.Separator();
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

        var arrow = favoriteTimezonesExpanded ? "▼" : "▶";
        if (ImGui.Selectable($"{arrow} {T("Favorite Timezones")}##FavoriteTimezonesHeader", false))
            favoriteTimezonesExpanded = !favoriteTimezonesExpanded;

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
            ImGui.Separator();

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

    private void DrawCompactFormatCombo()
    {
        var formatNames = TimeFormatHelper.Names;
        var formatIndex = Math.Clamp((int)configuration.TimeFormat, 0, formatNames.Length - 1);

        ImGui.Text(T("Time Format"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(162f);

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

    private void DrawAlarmsTab()
    {
        Section(T("Create Alarm"), () =>
        {
            DrawAlarmSelectors();

            string formatText = TimeFormatHelper.GetName(configuration.TimeFormat);
            ImGui.SetNextItemWidth(158f);
            ImGui.BeginDisabled();
            ImGui.InputText(T("Alarm Format"), ref formatText, 16);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(T("You can change it on \"General\""));
            }

            DrawAlarmSoundRow();

            var message = configuration.AlarmEditorMessage;
            ImGui.SetNextItemWidth(240f);
            if (ImGui.InputText(T("Alarm Message"), ref message, 128))
            {
                configuration.AlarmEditorMessage = message;
                configuration.Save();
            }

            bool snoozeEnabled = configuration.AlarmEditorSnoozeEnabled;
            if (ImGui.Checkbox(T("Allow Snooze after trigger"), ref snoozeEnabled))
            {
                configuration.AlarmEditorSnoozeEnabled = snoozeEnabled;
                configuration.Save();
            }

            if (configuration.AlarmEditorSnoozeEnabled)
            {
                var snoozeOptions = new[] { "5", "10", "15", "30" };
                var currentSnooze = configuration.AlarmEditorSnoozeMinutes.ToString(CultureInfo.InvariantCulture);
                var snoozeIndex = Array.IndexOf(snoozeOptions, currentSnooze);
                if (snoozeIndex < 0)
                    snoozeIndex = 0;

                ImGui.SetNextItemWidth(72f);
                if (DrawCombo("Snooze Duration", snoozeOptions, ref snoozeIndex, false))
                {
                    configuration.AlarmEditorSnoozeMinutes = int.Parse(snoozeOptions[snoozeIndex], CultureInfo.InvariantCulture);
                    configuration.Save();
                }

                ImGui.SameLine();
                ImGui.TextDisabled(T("minutes"));
            }

            bool isEditingAlarm = editingAlarmId.HasValue;
            var editorZone = GetAlarmEditorTimeZone();

            ImGui.BeginDisabled(isEditingAlarm);
            PushColoredButton("#ffb300", Vector4.One);
            if (ImGui.Button(T("Add Alarm")))
            {
                AlarmConfigurationService.AddFromEditor(configuration, editorZone);
                configuration.Save();
            }
            PopColoredButton();
            ImGui.EndDisabled();

            ImGui.SameLine();

            PushColoredButton("#228700", Vector4.One);
            if (ImGui.Button(T("Test Alarm")))
            {
                var temp = new AlarmEntry
                {
                    DateTimeText = AlarmConfigurationService.BuildEditorDateTimeText(configuration, editorZone),
                    Message = string.IsNullOrWhiteSpace(configuration.AlarmEditorMessage) ? "Alarm" : configuration.AlarmEditorMessage.Trim(),
                    TimeZoneId = editorZone,
                    SnoozeEnabled = configuration.AlarmEditorSnoozeEnabled,
                    SnoozeMinutes = Math.Clamp(configuration.AlarmEditorSnoozeMinutes, 1, 120)
                };

                plugin.TestAlarmOutput(temp.BuildTriggerMessage(configuration.TimeFormat));
            }
            PopColoredButton();

            ImGui.SameLine();

            ImGui.BeginDisabled(!isEditingAlarm);
            PushColoredButton("#D180FF", Vector4.One);
            if (ImGui.Button(T("Edit Alarm")) && editingAlarmId.HasValue)
            {
                if (AlarmConfigurationService.UpdateFromEditor(configuration, editingAlarmId.Value, editorZone))
                {
                    configuration.Save();
                    ClearAlarmEditingState();
                }
            }
            PopColoredButton();
            ImGui.EndDisabled();

            ImGui.TextDisabled(T("Alarm notifications are shown in chat and on screen."));
        });

        Section(T("Alarms"), () =>
        {
            var orderedAlarms = configuration.Alarms
                .OrderBy(a => a.HasTriggered ? 1 : 0)
                .ThenByDescending(a => a.Id)
                .ToList();

            if (orderedAlarms.Count == 0)
            {
                ImGui.TextDisabled(T("No alarms created."));
            }
            else
            {
                for (int i = 0; i < orderedAlarms.Count; i++)
                {
                    var alarm = orderedAlarms[i];
                    var color = alarm.HasTriggered
                        ? new Vector4(1.0f, 0.55f, 0.55f, 1f)
                        : new Vector4(0.45f, 1.0f, 0.45f, 1f);

                    var line = $"{i + 1}. {alarm.BuildListLine(configuration.TimeFormat, T("Alarm"))}";
                    ImGui.TextColored(color, line);

                    ImGui.SameLine();

                    PushSmallRemoveButton();
                    if (ImGui.SmallButton($"X##RemoveAlarm{alarm.Id}"))
                    {
                        AlarmConfigurationService.Remove(configuration, alarm.Id);
                        if (editingAlarmId == alarm.Id)
                            ClearAlarmEditingState();
                        configuration.Save();
                        PopSmallRemoveButton();
                        break;
                    }
                    var alarmActionButtonSize = ImGui.GetItemRectSize();
                    PopSmallRemoveButton();

                    ImGui.SameLine();

                    if (!alarm.HasTriggered)
                    {
                        if (DrawAlarmEditIconButton($"EditAlarm{alarm.Id}", alarmActionButtonSize))
                        {
                            LoadAlarmIntoEditor(alarm);
                        }
                    }
                    else
                    {
                        if (DrawAlarmReenableButton($"ReenableAlarm{alarm.Id}", alarmActionButtonSize))
                        {
                            var popupAnchor = ImGui.GetItemRectMin();
                            if (AlarmConfigurationService.ReenableForToday(configuration, alarm.Id))
                            {
                                plugin.ClearRecentlyTriggeredAlarm(alarm.Id);
                                configuration.Save();
                                ShowAlarmActionPopup(T("Alarm reenabled for today"), popupAnchor);
                            }
                        }

                        DrawSnoozeStatusLine(alarm);
                    }
                }
            }

            DrawAlarmReenabledPopup();
        });

        Section(T("Maintenance Reminders"), () =>
        {
            bool enabled = configuration.MaintenanceReminderEnabled;
            if (ImGui.Checkbox(T("Enable Maintenance Reminders"), ref enabled))
            {
                configuration.MaintenanceReminderEnabled = enabled;
                configuration.Save();
            }

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
            if (ImGui.Checkbox(T("24 hours before"), ref remind24))
            {
                configuration.MaintenanceRemind24Hours = remind24;
                configuration.Save();
            }

            bool remind1 = configuration.MaintenanceRemind1Hour;
            if (ImGui.Checkbox(T("1 hour before"), ref remind1))
            {
                configuration.MaintenanceRemind1Hour = remind1;
                configuration.Save();
            }

            bool remind15 = configuration.MaintenanceRemind15Minutes;
            if (ImGui.Checkbox(T("15 minutes before"), ref remind15))
            {
                configuration.MaintenanceRemind15Minutes = remind15;
                configuration.Save();
            }

            ImGui.Spacing();
            var selectedLanguageName = LodestoneMaintenanceService.GetLanguageName(configuration.MaintenanceLanguage);
            ImGui.TextDisabled(string.Format(T("Uses {0} Lodestone maintenance notices."), selectedLanguageName));
            ImGui.TextDisabled(T("Automatic checks run only while maintenance reminders are enabled."));
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

            if (!string.IsNullOrWhiteSpace(configuration.LastMaintenanceNewsUrl))
            {
                ImGui.Spacing();
                ImGui.TextWrapped(configuration.LastMaintenanceNewsUrl);
            }
        });
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
                ImGui.SetNextItemWidth(108f);
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

            var rename = configuration.GetActiveProfile().Name;
            ImGui.SetNextItemWidth(122f);
            if (ImGui.InputText(T("Rename Active Profile"), ref rename, 64))
            {
                configuration.GetActiveProfile().Name = rename;
                configuration.Save();
            }

            if (string.IsNullOrWhiteSpace(newProfileName))
                newProfileName = $"Profile {configuration.Profiles.Count + 1}";

            ImGui.SetNextItemWidth(114f);
            ImGui.InputText(T("New Profile"), ref newProfileName, 64);

            PushColoredButton("#228700", Vector4.One);
            if (ImGui.Button(T("Create From Current")))
            {
                configuration.AddProfile(newProfileName);
                configuration.Save();
                textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
            }
            PopColoredButton();

            ImGui.SameLine();

            PushColoredButton("#ff5757", Vector4.One);
            if (ImGui.Button(T("Delete Active Profile")))
            {
                configuration.DeleteActiveProfile();
                configuration.Save();
                textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
            }
            PopColoredButton();
        });

        Section(T("Presets"), () =>
        {
            ImGui.BulletText(T("Classic"));
            ImGui.BulletText(T("Minimal"));
            ImGui.BulletText(T("Gold HUD"));
            ImGui.BulletText(T("Retro Panel"));
            ImGui.TextDisabled(T("Presets are built-in themes. Profiles are your own saved custom setups."));
        });
    }

    private void DrawAppearanceTab()
    {
        var profile = configuration.GetActiveProfile();

        Section(T("Layout & Style"), () =>
        {
            var layoutNames = new[] { "Horizontal", "Vertical" };
            var layoutIndex = (int)profile.LayoutMode;
            ImGui.SetNextItemWidth(98f);
            if (DrawCombo("Layout Mode", layoutNames, ref layoutIndex))
            {
                profile.LayoutMode = (ClockLayoutMode)layoutIndex;
                configuration.Save();
            }

            var styleNames = new[] { "Classic", "Minimal", "Strong Shadow", "Soft Glass", "Retro Panel" };
            var styleIndex = (int)profile.DisplayStyle;
            ImGui.SetNextItemWidth(114f);
            if (DrawCombo("Display Style", styleNames, ref styleIndex))
            {
                profile.DisplayStyle = (ClockDisplayStyle)styleIndex;
                configuration.Save();
            }

            var presetNames = new[] { "Classic", "Minimal", "Gold HUD", "Retro Panel" };
            var presetIndex = (int)presetSelection;
            ImGui.SetNextItemWidth(108f);
            if (DrawCombo("Preset", presetNames, ref presetIndex))
            {
                presetSelection = (ClockPreset)presetIndex;
                configuration.PreviewPresetSelection = presetSelection;
                configuration.Save();
            }

            PushColoredButton("#ffb300", Vector4.One);
            if (ImGui.Button(T("Apply Preset To Active Profile")))
            {
                configuration.ApplyPresetToActiveProfile(presetSelection);
                configuration.Save();
                textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
            }
            PopColoredButton();

            DrawCompactColonCombo();
        });


        Section(T("Visibility"), () =>
        {
            float startX = ImGui.GetCursorPosX();

            bool showBorder = profile.ShowBorder;
            if (ImGui.Checkbox(T("Border"), ref showBorder))
            {
                profile.ShowBorder = showBorder;
                configuration.Save();
            }

            ImGui.SameLine(startX + 92f);
            bool showShadowText = profile.ShowShadowText;
            if (ImGui.Checkbox(T("Shadow Text"), ref showShadowText))
            {
                profile.ShowShadowText = showShadowText;
                configuration.Save();
            }

            bool showIcon = profile.ShowIcon;
            if (ImGui.Checkbox(T("Icon"), ref showIcon))
            {
                profile.ShowIcon = showIcon;
                configuration.Save();
            }

            ImGui.SameLine(startX + 92f);
            bool showIconBorder = profile.ShowIconBorder;
            if (ImGui.Checkbox(T("Icon Border"), ref showIconBorder))
            {
                profile.ShowIconBorder = showIconBorder;
                configuration.Save();
            }
        });

        Section(T("Text"), () =>
        {
            DrawTextSizeControl();
            DrawCompactColorRow("Text Color", ref profile.ClockTextColor, "##TextColor");
            DrawCompactColorRow("Shadow Color", ref profile.ClockShadowColor, "##ShadowColor");
        });

        Section(T("Background"), () =>
        {
            DrawCompactColorRow("Background Color", ref profile.ClockBackgroundColor, "##BgColor");
            DrawCompactColorRow("Border Color", ref profile.BorderColor, "##BorderColor");

            ImGui.SetNextItemWidth(122f);
            float opacity = profile.ClockBackgroundOpacity;
            if (DrawEditableSliderFloat("Background Opacity", ref opacity, 0.0f, 1.0f, "%.2f"))
            {
                profile.ClockBackgroundOpacity = opacity;
                configuration.Save();
            }

            ImGui.SetNextItemWidth(122f);
            float borderOpacity = profile.BorderOpacity;
            if (DrawEditableSliderFloat("Border Opacity", ref borderOpacity, 0.0f, 1.0f, "%.2f"))
            {
                profile.BorderOpacity = borderOpacity;
                configuration.Save();
            }
        });

        Section(T("Icon"), () =>
        {
            DrawCompactColorRow("Icon Text Color", ref profile.IconTextColor, "##IconTextColor");
            DrawCompactColorRow("Icon Background", ref profile.IconBackgroundColor, "##IconBgColor");
            DrawCompactColorRow("Icon Border", ref profile.IconBorderColor, "##IconBorderColor");

            ImGui.SetNextItemWidth(122f);
            float iconBorderOpacity = profile.IconBorderOpacity;
            if (DrawEditableSliderFloat("Icon Border Opacity", ref iconBorderOpacity, 0.0f, 1.0f, "%.2f"))
            {
                profile.IconBorderOpacity = iconBorderOpacity;
                configuration.Save();
            }
        });

        Section(T("Local Time Layout"), () =>
        {
            var placementNames = new[] { "Inside main display", "Outside main display" };
            var placementIndex = (int)profile.LocalTimePlacement;
            ImGui.SetNextItemWidth(170f);
            if (DrawCombo("Placement", placementNames, ref placementIndex))
            {
                profile.LocalTimePlacement = (LocalTimePlacement)placementIndex;
                configuration.Save();
            }

            var formatNames = TimeFormatHelper.Names;
            var localFormatIndex = Math.Clamp((int)profile.LocalTimeFormat, 0, formatNames.Length - 1);
            ImGui.SetNextItemWidth(162f);
            if (DrawCombo("Local Time Format", formatNames, ref localFormatIndex))
            {
                profile.LocalTimeFormat = (ClockTimeFormat)localFormatIndex;
                configuration.Save();
            }

            ImGui.SetNextItemWidth(145f);
            float localVerticalOffset = profile.LocalTimeVerticalOffset;
            if (DrawEditableSliderFloat("Local Vertical Offset", ref localVerticalOffset, -40.0f, 40.0f, "%.1f"))
            {
                profile.LocalTimeVerticalOffset = localVerticalOffset;
                configuration.Save();
            }

            ImGui.SetNextItemWidth(145f);
            float localHorizontalOffset = profile.LocalTimeHorizontalOffset;
            if (DrawEditableSliderFloat("Local Horizontal Offset", ref localHorizontalOffset, -40.0f, 40.0f, "%.1f"))
            {
                profile.LocalTimeHorizontalOffset = localHorizontalOffset;
                configuration.Save();
            }

            ImGui.TextDisabled(T("Use this to move the local time block up/down or left/right without changing the main clock."));
        });

        Section(T("Local Time Text"), () =>
        {
            var localStyleNames = new[] { "Classic", "Minimal", "Strong Shadow", "Soft Glass", "Retro Panel" };
            var localStyleIndex = (int)profile.LocalTimeDisplayStyle;
            ImGui.SetNextItemWidth(114f);
            if (DrawCombo("Local Display Style", localStyleNames, ref localStyleIndex))
            {
                profile.LocalTimeDisplayStyle = (ClockDisplayStyle)localStyleIndex;
                configuration.Save();
            }

            bool localShadow = profile.LocalTimeShowShadowText;
            if (ImGui.Checkbox(T("Local Shadow Text"), ref localShadow))
            {
                profile.LocalTimeShowShadowText = localShadow;
                configuration.Save();
            }

            DrawLocalTextSizeControl();
            DrawCompactColorRow("Local Text Color", ref profile.LocalTimeTextColor, "##LocalTextColor");
            DrawCompactColorRow("Local Shadow Color", ref profile.LocalTimeShadowColor, "##LocalShadowColor");
        });

        Section(T("Local Time Icon"), () =>
        {
            bool localIcon = profile.LocalTimeShowIcon;
            if (ImGui.Checkbox(T("Local Icon"), ref localIcon))
            {
                profile.LocalTimeShowIcon = localIcon;
                configuration.Save();
            }

            bool localIconBorder = profile.LocalTimeShowIconBorder;
            if (ImGui.Checkbox(T("Local Icon Border"), ref localIconBorder))
            {
                profile.LocalTimeShowIconBorder = localIconBorder;
                configuration.Save();
            }

            DrawCompactColorRow("Local Icon Text Color", ref profile.LocalTimeIconTextColor, "##LocalIconTextColor");
            DrawCompactColorRow("Local Icon Background", ref profile.LocalTimeIconBackgroundColor, "##LocalIconBgColor");
            DrawCompactColorRow("Local Icon Border Color", ref profile.LocalTimeIconBorderColor, "##LocalIconBorderColor");

            ImGui.SetNextItemWidth(122f);
            float localIconBorderOpacity = profile.LocalTimeIconBorderOpacity;
            if (DrawEditableSliderFloat("Local Icon Border Opacity", ref localIconBorderOpacity, 0.0f, 1.0f, "%.2f"))
            {
                profile.LocalTimeIconBorderOpacity = localIconBorderOpacity;
                configuration.Save();
            }
        });

        Section(T("Local Time Background"), () =>
        {
            bool localBorder = profile.LocalTimeShowBorder;
            if (ImGui.Checkbox(T("Local Border"), ref localBorder))
            {
                profile.LocalTimeShowBorder = localBorder;
                configuration.Save();
            }

            DrawCompactColorRow("Local Background", ref profile.LocalTimeBackgroundColor, "##LocalBgColor");
            DrawCompactColorRow("Local Border Color", ref profile.LocalTimeBorderColor, "##LocalBorderColor");

            ImGui.SetNextItemWidth(122f);
            float localOpacity = profile.LocalTimeBackgroundOpacity;
            if (DrawEditableSliderFloat("Local Background Opacity", ref localOpacity, 0.0f, 1.0f, "%.2f"))
            {
                profile.LocalTimeBackgroundOpacity = localOpacity;
                configuration.Save();
            }

            ImGui.SetNextItemWidth(122f);
            float localBorderOpacity = profile.LocalTimeBorderOpacity;
            if (DrawEditableSliderFloat("Local Border Opacity", ref localBorderOpacity, 0.0f, 1.0f, "%.2f"))
            {
                profile.LocalTimeBorderOpacity = localBorderOpacity;
                configuration.Save();
            }

            ImGui.TextDisabled(T("Local time follows its own style settings and can be placed inside or outside the main display."));
        });
    }


    private void DrawLocalTextSizeControl()
    {
        var profile = configuration.GetActiveProfile();

        ImGui.SetNextItemWidth(145f);
        float scale = profile.LocalTimeTextScale;
        if (DrawEditableSliderFloat("Local Text Size", ref scale, 0.35f, 5.0f, "%.2f"))
        {
            profile.LocalTimeTextScale = scale;
            configuration.Save();
        }
    }

    private void DrawCompactColorRow(string label, ref Vector4 color, string id)
    {
        ImGui.SetNextItemWidth(34f);
        if (ImGui.ColorEdit4(id, ref color, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.AlphaBar))
        {
            configuration.Save();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(T(label));
    }


    private void DrawSnoozeStatusLine(AlarmEntry alarm)
    {
        if (!alarm.SnoozeEnabled || !alarm.HasTriggered)
            return;

        var hasPendingSnooze = alarm.SnoozedUntilUtc > DateTime.MinValue && !alarm.SnoozeTriggered && !alarm.SnoozeCanceled;
        if (!hasPendingSnooze && !alarm.SnoozeTriggered && !alarm.SnoozeCanceled)
            return;

        ImGui.Indent(18f);

        if (hasPendingSnooze)
        {
            PushSmallRemoveButton();
            if (ImGui.SmallButton($"X##CancelSnooze{alarm.Id}"))
            {
                if (AlarmConfigurationService.CancelSnooze(configuration, alarm.Id))
                {
                    plugin.ClearRecentlyTriggeredAlarm(alarm.Id);
                    configuration.Save();
                }
            }
            PopSmallRemoveButton();
            ImGui.SameLine();

            var snoozeLocal = TimeZoneHelper.ConvertFromUtc(alarm.SnoozedUntilUtc, alarm.GetEffectiveTimeZoneId());
            var snoozeText = TimeFormatHelper.FormatClock(snoozeLocal, configuration.TimeFormat);
            ImGui.TextDisabled(string.Format(T("Snooze will trigger at {0}"), snoozeText));
        }
        else if (alarm.SnoozeTriggered)
        {
            ImGui.TextDisabled(T("Snooze already triggered."));
        }
        else if (alarm.SnoozeCanceled)
        {
            ImGui.TextDisabled(T("Snooze canceled."));
        }

        ImGui.Unindent(18f);
    }

    private void DrawAlarmSoundRow()
    {
        var selectedSound = Math.Clamp(configuration.AlarmSoundId, Plugin.MinAlarmSoundEffectId, Plugin.MaxAlarmSoundEffectId);

        ImGui.SetNextItemWidth(84f);
        if (ImGui.BeginCombo("##ClockAlarmSoundId", selectedSound.ToString(CultureInfo.InvariantCulture)))
        {
            for (var soundId = Plugin.MinAlarmSoundEffectId; soundId <= Plugin.MaxAlarmSoundEffectId; soundId++)
            {
                var soundText = soundId.ToString(CultureInfo.InvariantCulture);
                var isSelected = selectedSound == soundId;

                if (ImGui.Selectable(soundText, isSelected))
                {
                    configuration.AlarmSoundId = soundId;
                    configuration.Save();
                    selectedSound = soundId;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();

        if (ImGui.Button($"{T("Test")}##ClockAlarmSoundTest"))
            plugin.PlaySelectedAlarmSoundOnly();

        ImGui.SameLine();
        ImGui.BeginDisabled();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(T("Sound"));
        ImGui.EndDisabled();
    }

    private void DrawAlarmSelectors()
    {
        var editorZone = GetAlarmEditorTimeZone();
        AlarmConfigurationService.RefreshEditorDateForLocalDay(configuration, editorZone);

        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, editorZone);
        var year = zoneNow.Year;
        var month = zoneNow.Month;
        var maxDay = DateTime.DaysInMonth(year, month);

        configuration.AlarmEditorDay = Math.Clamp(configuration.AlarmEditorDay, 1, maxDay);

        var dayIndex = configuration.AlarmEditorDay - 1;
        var dayItems = Enumerable.Range(1, maxDay).Select(d => d.ToString("00")).ToArray();

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

        ImGui.Text(T("Alarm Date/Time"));

        float dayWidth = 64f;
        float hourWidth = 52f;
        float minuteWidth = 52f;
        float meridiemWidth = 58f;

        ImGui.SetNextItemWidth(dayWidth);
        if (ImGui.BeginCombo("##AlarmDay", dayItems[dayIndex]))
        {
            for (int i = 0; i < dayItems.Length; i++)
            {
                bool selected = i == dayIndex;
                if (ImGui.Selectable(dayItems[i], selected))
                {
                    configuration.AlarmEditorDay = i + 1;
                    configuration.Save();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.TextDisabled(zoneNow.ToString("MMMM yyyy", ClockLocalizationService.GetCultureInfo(configuration.UiLanguageCultureName)));

        ImGui.SetNextItemWidth(hourWidth);
        if (ImGui.BeginCombo("##AlarmHour", hourItems[hourIndex]))
        {
            for (int i = 0; i < hourItems.Length; i++)
            {
                bool selected = i == hourIndex;
                if (ImGui.Selectable(hourItems[i], selected))
                {
                    configuration.AlarmEditorHour = int.Parse(hourItems[i], CultureInfo.InvariantCulture);
                    configuration.Save();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.Text(":");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(minuteWidth);
        if (ImGui.BeginCombo("##AlarmMinute", minuteItems[minuteIndex]))
        {
            for (int i = 0; i < minuteItems.Length; i++)
            {
                bool selected = i == minuteIndex;
                if (ImGui.Selectable(minuteItems[i], selected))
                {
                    configuration.AlarmEditorMinute = i;
                    configuration.Save();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        if (TimeFormatHelper.UsesTwelveHourClock(configuration.TimeFormat))
        {
            ImGui.SameLine();

            var meridiemOptions = new[] { "AM", "PM" };
            var meridiemIndex = configuration.AlarmEditorIsPm ? 1 : 0;
            ImGui.SetNextItemWidth(meridiemWidth);
            if (ImGui.BeginCombo("##AlarmMeridiem", meridiemOptions[meridiemIndex]))
            {
                for (int i = 0; i < meridiemOptions.Length; i++)
                {
                    bool selected = i == meridiemIndex;
                    if (ImGui.Selectable(meridiemOptions[i], selected))
                    {
                        configuration.AlarmEditorIsPm = i == 1;
                        configuration.Save();
                    }

                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled(TimeZoneHelper.ToShortText(editorZone));
    }


    private string GetAlarmEditorTimeZone()
    {
        return editingAlarmTimeZoneId ?? configuration.SelectedTimeZoneId;
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

    private bool DrawAlarmEditIconButton(string id, Vector2 buttonSize)
    {
        PushColoredButton("#D180FF", Vector4.One);
        bool clicked = ImGui.Button($"##{id}", buttonSize);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        PopColoredButton();

        var drawList = ImGui.GetWindowDrawList();
        uint iconColor = ImGui.GetColorU32(Vector4.One);
        float width = max.X - min.X;
        float height = max.Y - min.Y;
        float padding = MathF.Max(2f, MathF.Min(width, height) * 0.22f);
        float thickness = MathF.Max(1f, MathF.Min(width, height) * 0.08f);

        var rectMin = new Vector2(min.X + padding, min.Y + padding);
        var rectMax = new Vector2(max.X - padding, max.Y - padding);
        drawList.AddRect(rectMin, rectMax, iconColor, 0f, ImDrawFlags.None, thickness);

        float pencilInset = padding + 1f;
        var pencilStart = new Vector2(min.X + pencilInset, max.Y - pencilInset);
        var pencilEnd = new Vector2(max.X - pencilInset, min.Y + pencilInset);
        drawList.AddLine(pencilStart, pencilEnd, iconColor, thickness);

        float offset = MathF.Max(1f, thickness);
        drawList.AddLine(
            new Vector2(pencilStart.X + offset, pencilStart.Y + offset),
            new Vector2(pencilEnd.X + offset, pencilEnd.Y + offset),
            iconColor,
            thickness);

        drawList.AddLine(
            new Vector2(max.X - (padding + 2f), min.Y + padding),
            new Vector2(max.X - padding, min.Y + padding + 2f),
            iconColor,
            thickness);

        return clicked;
    }

    private bool DrawAlarmReenableButton(string id, Vector2 buttonSize)
    {
        PushColoredButton("#32a84e", Vector4.One);
        bool clicked = ImGui.Button($"##{id}", buttonSize);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        bool hovered = ImGui.IsItemHovered();
        PopColoredButton();

        var drawList = ImGui.GetWindowDrawList();
        const string symbol = "R";
        var textSize = ImGui.CalcTextSize(symbol);
        var textPos = new Vector2(
            min.X + MathF.Floor(((max.X - min.X) - textSize.X) * 0.5f),
            min.Y + MathF.Floor(((max.Y - min.Y) - textSize.Y) * 0.5f)
        );

        drawList.AddText(textPos, ImGui.GetColorU32(Vector4.One), symbol);

        if (hovered)
            ImGui.SetTooltip(T("Reenable Alarm for today"));

        return clicked;
    }

    private void ShowAlarmActionPopup(string message, Vector2 anchor)
    {
        alarmActionPopupMessage = message;
        reenabledPopupPosition = new Vector2(anchor.X, anchor.Y - 6f);
        openReenabledPopupNextFrame = true;
    }

    private void DrawAlarmReenabledPopup()
    {
        const string popupId = "AlarmReenabledPopup";

        if (openReenabledPopupNextFrame)
        {
            ImGui.OpenPopup(popupId);
            openReenabledPopupNextFrame = false;
        }

        ImGui.SetNextWindowPos(reenabledPopupPosition, ImGuiCond.Appearing, new Vector2(0f, 1f));
        if (ImGui.BeginPopup(popupId))
        {
            ImGui.TextUnformatted(string.IsNullOrWhiteSpace(alarmActionPopupMessage) ? T("Alarm updated") : T(alarmActionPopupMessage));
            if (ImGui.Button(T("OK")))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    private void DrawCommandsTab()
    {
        ImGui.PushTextWrapPos();

        ImGui.TextColored(new Vector4(1f, 0.85f, 0.45f, 1f), T("Slash Commands"));
        ImGui.Separator();
        ImGui.Spacing();

        DrawCommandLine("/clock", "Toggle the clock window");
        DrawCommandLine("/clock settings", "Open settings");
        DrawCommandLine("/clockalarms", "Open settings directly on the Alarms tab");
        DrawCommandLine("/alarms", "Open settings directly on the Alarms tab");
        DrawCommandLine("/clock timezone <TimeZoneInfo ID or alias>", "Change the main clock timezone");
        DrawCommandLine("/clock format 12|24|12s|24s|weekday|date", "Change time format preset");
        DrawCommandLine("/clock colon default|always|hidden|slow|fast", "Change colon animation");
        DrawCommandLine("/clock layout horizontal|vertical", "Change active profile layout");
        DrawCommandLine("/clock preset classic|minimal|gold|retro", "Select a preset preview");
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
        ImGui.SameLine();
        ImGui.TextDisabled($"- {T(description)}");
    }

    private void DrawTextSizeControl()
    {
        var profile = configuration.GetActiveProfile();

        ImGui.SetNextItemWidth(145f);
        float scale = profile.ClockTextScale;
        if (DrawEditableSliderFloat("Text Size", ref scale, 0.5f, 5.0f, "%.2f"))
        {
            profile.ClockTextScale = scale;
            textSizeInputValue = scale;
            configuration.Save();
        }
    }

    private void ApplyTextSizeInput()
    {
        var profile = configuration.GetActiveProfile();
        textSizeInputValue = Math.Clamp(textSizeInputValue, 0.5f, 5.0f);
        profile.ClockTextScale = textSizeInputValue;
        configuration.Save();
    }

    private bool DrawEditableSliderFloat(string label, ref float value, float min, float max, string format)
    {
        if (editingSliderId == label)
        {
            if (focusSliderInputNextFrame)
            {
                ImGui.SetKeyboardFocusHere();
                focusSliderInputNextFrame = false;
            }

            float inputValue = editingSliderValue;
            bool pressedEnter = ImGui.InputFloat($"{T(label)}##{label}", ref inputValue, 0.0f, 0.0f, format, ImGuiInputTextFlags.EnterReturnsTrue);
            editingSliderValue = inputValue;

            if (pressedEnter)
            {
                value = Math.Clamp(editingSliderValue, min, max);
                editingSliderId = null;
                return true;
            }

            if (ImGui.IsItemDeactivated())
            {
                value = Math.Clamp(editingSliderValue, min, max);
                editingSliderId = null;
                return true;
            }

            return false;
        }

        bool changed = ImGui.SliderFloat($"{T(label)}##{label}", ref value, min, max, format);
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            editingSliderId = label;
            editingSliderValue = value;
            focusSliderInputNextFrame = true;
        }

        return changed;
    }

    private void Section(string title, Action drawContent, Action? drawTitleRight = null)
    {
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.82f, 0.42f, 1f), title);
        if (drawTitleRight != null)
        {
            ImGui.SameLine();
            drawTitleRight();
        }
        ImGui.Separator();
        ImGui.Spacing();
        drawContent();
        ImGui.Spacing();
    }

    private static void Help(string text)
    {
        ImGui.TextDisabled(text);
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

    private void PushColoredButton(string hexColor, Vector4 textColor)
    {
    }

    private void PopColoredButton()
    {
    }

    private void PushSmallRemoveButton()
    {
    }

    private void PopSmallRemoveButton()
    {
    }

    private static Vector4 HexToColor(string hex)
    {
        hex = hex.Trim().TrimStart('#');

        if (hex.Length != 6)
            return new Vector4(1f, 1f, 1f, 1f);

        var r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        var g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        var b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;

        return new Vector4(r, g, b, 1f);
    }

    private static Vector4 MultiplyColor(Vector4 color, float factor)
    {
        return new Vector4(
            Math.Clamp(color.X * factor, 0f, 1f),
            Math.Clamp(color.Y * factor, 0f, 1f),
            Math.Clamp(color.Z * factor, 0f, 1f),
            color.W);
    }
}
