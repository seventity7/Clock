using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ESTClock.Windows;

public class ConfigWindow : Window, IDisposable
{
    private enum ConfigTabRequest
    {
        None,
        Alarms
    }

    private const string HelpUrl = "https://github.com/seventity7/ESTClock";

    private readonly Plugin plugin;
    private readonly Configuration configuration;

    private bool isEditingTextSize;
    private bool focusTextSizeInput;
    private float textSizeInputValue;
    private string? editingSliderId;
    private float editingSliderValue;
    private bool focusSliderInputNextFrame;

    private ClockPreset presetSelection = ClockPreset.Classic;
    private string newProfileName = "";
    private ConfigTabRequest requestedTab = ConfigTabRequest.None;
    private Guid? editingAlarmId;
    private ClockTimeZone? editingAlarmTimeZone;
    private Vector2 reenabledPopupPosition;
    private bool openReenabledPopupNextFrame;

    public ConfigWindow(Plugin plugin)
        : base("###ConfigWindow")
    {
        this.plugin = plugin;
        this.configuration = plugin.Configuration;

        Flags =
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoTitleBar;

        Size = new Vector2(490, 580);
        SizeCondition = ImGuiCond.Always;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(490, 580),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
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
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.04f, 0.04f, 0.05f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 1f, 1f));

        var orange = HexToColor("#ffb300");
        var orangeHover = MultiplyColor(orange, 1.08f);
        var orangeActive = MultiplyColor(orange, 0.92f);

        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, orange);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, orangeHover);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, orangeActive);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 10.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 8.0f);
    }

    public override void Draw()
    {
        var windowSize = ImGui.GetWindowSize();
        DrawTopButtons(windowSize);

        ImGui.TextColored(new Vector4(1f, 0.88f, 0.55f, 1f), "EST Clock");
        ImGui.SameLine();
        ImGui.TextDisabled("Advanced Settings");
        ImGui.Separator();
        ImGui.Spacing();

        DrawProfileHeader();

        if (ImGui.BeginTabBar("ESTClockTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }

            var alarmsTabFlags = requestedTab == ConfigTabRequest.Alarms
                ? ImGuiTabItemFlags.SetSelected
                : ImGuiTabItemFlags.None;

            if (ImGui.BeginTabItem("Alarms", alarmsTabFlags))
            {
                requestedTab = ConfigTabRequest.None;
                DrawAlarmsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Profiles"))
            {
                DrawProfilesTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Appearance"))
            {
                DrawAppearanceTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Commands"))
            {
                DrawCommandsTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(8);
    }

    private void DrawTopButtons(Vector2 windowSize)
    {
        var savedCursor = ImGui.GetCursorPos();

        ImGui.SetCursorPos(new Vector2(windowSize.X - 118, 4));

        PushColoredButton("#ffb300", Vector4.One);
        if (ImGui.Button("HELP", new Vector2(58, 21)))
            OpenHelpUrl();
        PopColoredButton();

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.2f, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1.0f, 0.2f, 0.2f, 1.0f));

        if (ImGui.Button("X", new Vector2(44, 21)))
        {
            IsOpen = false;
            ImGui.PopStyleColor(3);
            ImGui.SetCursorPos(savedCursor);
            return;
        }

        ImGui.PopStyleColor(3);
        ImGui.SetCursorPos(savedCursor);
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
        ImGui.Text("Active Profile");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(126f);

        if (ImGui.BeginCombo("##ActiveProfileCombo", $"Profile: {configuration.GetActiveProfile().Name}"))
        {
            var profileIndices = Enumerable.Range(0, configuration.Profiles.Count)
                .Where(i => IsUserProfile(configuration.Profiles[i].Name))
                .ToList();

            if (profileIndices.Count == 0)
            {
                ImGui.TextDisabled("No user profiles");
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
        Section("Behavior", () =>
        {
            var stick = !configuration.IsConfigWindowMovable;
            if (ImGui.Checkbox("Stick clock", ref stick))
            {
                configuration.IsConfigWindowMovable = !stick;
                configuration.Save();
            }
            Help("Locks or unlocks movement/resizing of the clock window.");

            bool autoStart = configuration.AutoStart;
            if (ImGui.Checkbox("Auto Start", ref autoStart))
            {
                configuration.AutoStart = autoStart;
                configuration.Save();
            }
            Help("Automatically opens the clock after login.");

            bool hideDuringCutscenes = configuration.HideDuringCutscenes;
            if (ImGui.Checkbox("Hide during cutscenes", ref hideDuringCutscenes))
            {
                configuration.HideDuringCutscenes = hideDuringCutscenes;
                configuration.Save();
            }
            Help("Hides only the clock during cutscenes.");
        });

        Section("Time Display", () =>
        {
            DrawCompactTimezoneCombo();
            DrawCompactFormatCombo();
            DrawCompactColonCombo();

            var profile = configuration.GetActiveProfile();
            bool showLocalTime = profile.ShowLocalTime;
            if (ImGui.Checkbox("Show Local Time", ref showLocalTime))
            {
                profile.ShowLocalTime = showLocalTime;
                configuration.Save();
            }

            Help($"Badge automatically follows timezone: {configuration.SelectedTimeZone.ToShortText()}");
        });
    }

    private void DrawCompactTimezoneCombo()
    {
        var items = new[] { "EST", "PST", "UTC", "BST", "JST", "MST", "ACST" };
        int zoneIndex = configuration.SelectedTimeZone switch
        {
            ClockTimeZone.EST => 0,
            ClockTimeZone.Pacific => 1,
            ClockTimeZone.Universal => 2,
            ClockTimeZone.BST => 3,
            ClockTimeZone.JST => 4,
            ClockTimeZone.MST => 5,
            ClockTimeZone.ACST => 6,
            _ => 0
        };

        ImGui.Text("Primary Timezone");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(88f);

        if (ImGui.BeginCombo("##PrimaryTimezone", items[zoneIndex]))
        {
            for (int i = 0; i < items.Length; i++)
            {
                bool selected = i == zoneIndex;
                if (ImGui.Selectable(items[i], selected))
                {
                    configuration.SelectedTimeZone = i switch
                    {
                        0 => ClockTimeZone.EST,
                        1 => ClockTimeZone.Pacific,
                        2 => ClockTimeZone.Universal,
                        3 => ClockTimeZone.BST,
                        4 => ClockTimeZone.JST,
                        5 => ClockTimeZone.MST,
                        6 => ClockTimeZone.ACST,
                        _ => ClockTimeZone.EST
                    };
                    configuration.Save();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }

    private void DrawCompactFormatCombo()
    {
        var formatNames = new[] { "12-hour", "24-hour" };
        var formatIndex = (int)configuration.TimeFormat;

        ImGui.Text("Time Format");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(88f);

        if (ImGui.BeginCombo("##TimeFormat", formatNames[Math.Clamp(formatIndex, 0, formatNames.Length - 1)]))
        {
            for (int i = 0; i < formatNames.Length; i++)
            {
                bool selected = i == formatIndex;
                if (ImGui.Selectable(formatNames[i], selected))
                {
                    configuration.TimeFormat = (ClockTimeFormat)i;
                    configuration.Save();
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

        ImGui.Text("Colon Animation");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(88f);

        if (ImGui.BeginCombo("##ColonAnimation", colonNames[Math.Clamp(colonIndex, 0, colonNames.Length - 1)]))
        {
            for (int i = 0; i < colonNames.Length; i++)
            {
                bool selected = i == colonIndex;
                if (ImGui.Selectable(colonNames[i], selected))
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
        Section("Create Alarm", () =>
        {
            DrawAlarmSelectors();

            string formatText = configuration.TimeFormat == ClockTimeFormat.TwelveHour ? "12-hour" : "24-hour";
            ImGui.SetNextItemWidth(84f);
            ImGui.BeginDisabled();
            ImGui.InputText("Alarm Format", ref formatText, 16);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("You can change it on \"General\"");
            }

            var message = configuration.AlarmEditorMessage;
            ImGui.SetNextItemWidth(142f);
            if (ImGui.InputText("Alarm Message", ref message, 128))
            {
                configuration.AlarmEditorMessage = message;
                configuration.Save();
            }

            bool isEditingAlarm = editingAlarmId.HasValue;
            var editorZone = GetAlarmEditorTimeZone();

            ImGui.BeginDisabled(isEditingAlarm);
            PushColoredButton("#ffb300", Vector4.One);
            if (ImGui.Button("Add Alarm"))
            {
                configuration.AddAlarmFromEditor(editorZone);
                configuration.Save();
            }
            PopColoredButton();
            ImGui.EndDisabled();

            ImGui.SameLine();

            PushColoredButton("#228700", Vector4.One);
            if (ImGui.Button("Test Alarm"))
            {
                var temp = new AlarmEntry
                {
                    DateTimeText = configuration.BuildAlarmEditorDateTimeText(editorZone),
                    Message = string.IsNullOrWhiteSpace(configuration.AlarmEditorMessage) ? "Alarm" : configuration.AlarmEditorMessage.Trim(),
                    TimeZone = editorZone
                };

                plugin.TestAlarmOutput(temp.BuildTriggerMessage(configuration.TimeFormat));
            }
            PopColoredButton();

            ImGui.SameLine();

            ImGui.BeginDisabled(!isEditingAlarm);
            PushColoredButton("#D180FF", Vector4.One);
            if (ImGui.Button("Edit Alarm") && editingAlarmId.HasValue)
            {
                if (configuration.UpdateAlarmFromEditor(editingAlarmId.Value, editorZone))
                {
                    configuration.Save();
                    ClearAlarmEditingState();
                }
            }
            PopColoredButton();
            ImGui.EndDisabled();

            ImGui.TextDisabled("Alarm notifications are shown in chat and on screen.");
        });

        Section("Alarms", () =>
        {
            var orderedAlarms = configuration.Alarms
                .OrderBy(a => a.HasTriggered ? 1 : 0)
                .ThenByDescending(a => a.Id)
                .ToList();

            if (orderedAlarms.Count == 0)
            {
                ImGui.TextDisabled("No alarms created.");
            }
            else
            {
                for (int i = 0; i < orderedAlarms.Count; i++)
                {
                    var alarm = orderedAlarms[i];
                    var color = alarm.HasTriggered
                        ? new Vector4(1.0f, 0.55f, 0.55f, 1f)
                        : new Vector4(0.45f, 1.0f, 0.45f, 1f);

                    var line = $"{i + 1}. {alarm.BuildListLine(configuration.TimeFormat)}";
                    ImGui.TextColored(color, line);

                    ImGui.SameLine();

                    PushSmallRemoveButton();
                    if (ImGui.SmallButton($"X##RemoveAlarm{alarm.Id}"))
                    {
                        configuration.RemoveAlarm(alarm.Id);
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
                            if (configuration.ReenableAlarmForToday(alarm.Id))
                            {
                                configuration.Save();
                                reenabledPopupPosition = new Vector2(popupAnchor.X, popupAnchor.Y - 6f);
                                openReenabledPopupNextFrame = true;
                            }
                        }
                    }
                }
            }

            DrawAlarmReenabledPopup();
        });

        Section("Maintenance Reminders", () =>
        {
            bool enabled = configuration.MaintenanceReminderEnabled;
            if (ImGui.Checkbox("Enable Maintenance Reminders", ref enabled))
            {
                configuration.MaintenanceReminderEnabled = enabled;
                configuration.Save();
            }

            bool remind24 = configuration.MaintenanceRemind24Hours;
            if (ImGui.Checkbox("24 hours before", ref remind24))
            {
                configuration.MaintenanceRemind24Hours = remind24;
                configuration.Save();
            }

            bool remind1 = configuration.MaintenanceRemind1Hour;
            if (ImGui.Checkbox("1 hour before", ref remind1))
            {
                configuration.MaintenanceRemind1Hour = remind1;
                configuration.Save();
            }

            bool remind15 = configuration.MaintenanceRemind15Minutes;
            if (ImGui.Checkbox("15 minutes before", ref remind15))
            {
                configuration.MaintenanceRemind15Minutes = remind15;
                configuration.Save();
            }

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.45f, 1f), "Detected system message:");
            ImGui.TextWrapped(string.IsNullOrWhiteSpace(configuration.LastDetectedMaintenanceMessage)
                ? "No maintenance message captured yet."
                : configuration.LastDetectedMaintenanceMessage);

            if (configuration.HasDetectedMaintenanceTime)
            {
                ImGui.Spacing();
                ImGui.Text($"Detected maintenance time: {configuration.DetectedMaintenanceDateTimeText} {configuration.SelectedTimeZone.ToShortText()}");
            }
        });
    }

    private void DrawProfilesTab()
    {
        Section("Profiles", () =>
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
                if (DrawCombo("Saved Profiles", userProfiles, ref savedProfileIndex))
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
                ImGui.TextDisabled("Saved Profiles");
                ImGui.SameLine();
                ImGui.TextDisabled("No user profiles");
            }

            var rename = configuration.GetActiveProfile().Name;
            ImGui.SetNextItemWidth(122f);
            if (ImGui.InputText("Rename Active Profile", ref rename, 64))
            {
                configuration.GetActiveProfile().Name = rename;
                configuration.Save();
            }

            if (string.IsNullOrWhiteSpace(newProfileName))
                newProfileName = $"Profile {configuration.Profiles.Count + 1}";

            ImGui.SetNextItemWidth(114f);
            ImGui.InputText("New Profile", ref newProfileName, 64);

            PushColoredButton("#228700", Vector4.One);
            if (ImGui.Button("Create From Current"))
            {
                configuration.AddProfile(newProfileName);
                configuration.Save();
                textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
            }
            PopColoredButton();

            ImGui.SameLine();

            PushColoredButton("#ff5757", Vector4.One);
            if (ImGui.Button("Delete Active Profile"))
            {
                configuration.DeleteActiveProfile();
                configuration.Save();
                textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
            }
            PopColoredButton();
        });

        Section("Presets", () =>
        {
            ImGui.BulletText("Classic");
            ImGui.BulletText("Minimal");
            ImGui.BulletText("Gold HUD");
            ImGui.BulletText("Retro Panel");
            ImGui.TextDisabled("Presets are built-in themes. Profiles are your own saved custom setups.");
        });
    }

    private void DrawAppearanceTab()
    {
        var profile = configuration.GetActiveProfile();

        Section("Layout & Style", () =>
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
            if (ImGui.Button("Apply Preset To Active Profile"))
            {
                configuration.ApplyPresetToActiveProfile(presetSelection);
                configuration.Save();
                textSizeInputValue = configuration.GetActiveProfile().ClockTextScale;
            }
            PopColoredButton();
        });

        Section("Visibility", () =>
        {
            float startX = ImGui.GetCursorPosX();

            bool showBorder = profile.ShowBorder;
            if (ImGui.Checkbox("Border", ref showBorder))
            {
                profile.ShowBorder = showBorder;
                configuration.Save();
            }

            ImGui.SameLine(startX + 92f);
            bool showShadowText = profile.ShowShadowText;
            if (ImGui.Checkbox("Shadow Text", ref showShadowText))
            {
                profile.ShowShadowText = showShadowText;
                configuration.Save();
            }

            bool showIcon = profile.ShowIcon;
            if (ImGui.Checkbox("Icon", ref showIcon))
            {
                profile.ShowIcon = showIcon;
                configuration.Save();
            }

            ImGui.SameLine(startX + 92f);
            bool showIconBorder = profile.ShowIconBorder;
            if (ImGui.Checkbox("Icon Border", ref showIconBorder))
            {
                profile.ShowIconBorder = showIconBorder;
                configuration.Save();
            }
        });

        Section("Text", () =>
        {
            DrawTextSizeControl();
            DrawCompactColorRow("Text Color", ref profile.ClockTextColor, "##TextColor");
            DrawCompactColorRow("Shadow Color", ref profile.ClockShadowColor, "##ShadowColor");
        });

        Section("Background", () =>
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

        Section("Icon", () =>
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

        Section("Local Time Layout", () =>
        {
            var placementNames = new[] { "Inside main display", "Outside main display" };
            var placementIndex = (int)profile.LocalTimePlacement;
            ImGui.SetNextItemWidth(170f);
            if (DrawCombo("Placement", placementNames, ref placementIndex))
            {
                profile.LocalTimePlacement = (LocalTimePlacement)placementIndex;
                configuration.Save();
            }

            var formatNames = new[] { "12-hour", "24-hour" };
            var localFormatIndex = (int)profile.LocalTimeFormat;
            ImGui.SetNextItemWidth(88f);
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

            ImGui.TextDisabled("Use this to move the local time block up/down or left/right without changing the main clock.");
        });

        Section("Local Time Text", () =>
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
            if (ImGui.Checkbox("Local Shadow Text", ref localShadow))
            {
                profile.LocalTimeShowShadowText = localShadow;
                configuration.Save();
            }

            DrawLocalTextSizeControl();
            DrawCompactColorRow("Local Text Color", ref profile.LocalTimeTextColor, "##LocalTextColor");
            DrawCompactColorRow("Local Shadow Color", ref profile.LocalTimeShadowColor, "##LocalShadowColor");
        });

        Section("Local Time Icon", () =>
        {
            bool localIcon = profile.LocalTimeShowIcon;
            if (ImGui.Checkbox("Local Icon", ref localIcon))
            {
                profile.LocalTimeShowIcon = localIcon;
                configuration.Save();
            }

            bool localIconBorder = profile.LocalTimeShowIconBorder;
            if (ImGui.Checkbox("Local Icon Border", ref localIconBorder))
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

        Section("Local Time Background", () =>
        {
            bool localBorder = profile.LocalTimeShowBorder;
            if (ImGui.Checkbox("Local Border", ref localBorder))
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

            ImGui.TextDisabled("Local time follows its own style settings and can be placed inside or outside the main display.");
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
        ImGui.TextUnformatted(label);
    }

    private void DrawAlarmSelectors()
    {
        var editorZone = GetAlarmEditorTimeZone();
        configuration.RefreshAlarmEditorDateForLocalDay(editorZone);

        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, editorZone);
        var year = zoneNow.Year;
        var month = zoneNow.Month;
        var maxDay = DateTime.DaysInMonth(year, month);

        configuration.AlarmEditorDay = Math.Clamp(configuration.AlarmEditorDay, 1, maxDay);

        var dayIndex = configuration.AlarmEditorDay - 1;
        var dayItems = Enumerable.Range(1, maxDay).Select(d => d.ToString("00")).ToArray();

        int hourRangeStart = configuration.TimeFormat == ClockTimeFormat.TwelveHour ? 1 : 0;
        int hourRangeCount = configuration.TimeFormat == ClockTimeFormat.TwelveHour ? 12 : 24;

        int visibleHour = configuration.AlarmEditorHour;
        if (configuration.TimeFormat == ClockTimeFormat.TwelveHour)
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

        ImGui.Text("Alarm Date/Time");

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
        ImGui.TextDisabled(zoneNow.ToString("MMMM yyyy", CultureInfo.InvariantCulture));

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

        if (configuration.TimeFormat == ClockTimeFormat.TwelveHour)
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
        ImGui.TextDisabled(editorZone.ToShortText());
    }


    private ClockTimeZone GetAlarmEditorTimeZone()
    {
        return editingAlarmTimeZone ?? configuration.SelectedTimeZone;
    }

    private void LoadAlarmIntoEditor(AlarmEntry alarm)
    {
        editingAlarmId = alarm.Id;
        editingAlarmTimeZone = alarm.TimeZone;

        if (!TimeZoneHelper.TryParseInZone(alarm.DateTimeText, alarm.TimeZone, out var alarmUtc))
            return;

        var alarmLocal = TimeZoneHelper.ConvertFromUtc(alarmUtc, alarm.TimeZone);
        configuration.AlarmEditorDay = alarmLocal.Day;
        configuration.AlarmEditorMinute = alarmLocal.Minute;
        configuration.AlarmEditorMessage = alarm.Message ?? "";

        if (configuration.TimeFormat == ClockTimeFormat.TwelveHour)
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
        editingAlarmTimeZone = null;
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
            ImGui.SetTooltip("Reenable Alarm for today");

        return clicked;
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
            ImGui.TextUnformatted("Alarm reenabled for today");
            if (ImGui.Button("OK"))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    private void DrawCommandsTab()
    {
        ImGui.PushTextWrapPos();

        ImGui.TextColored(new Vector4(1f, 0.85f, 0.45f, 1f), "Slash Commands");
        ImGui.Separator();
        ImGui.Spacing();

        DrawCommandLine("/est", "Toggle the clock window");
        DrawCommandLine("/est settings", "Open settings");
        DrawCommandLine("/estalarms", "Open settings directly on the Alarms tab");
        DrawCommandLine("/est timezone est|pst|utc|bst|jst|mst|acst", "Change the main clock timezone");
        DrawCommandLine("/est format 12|24", "Switch between 12h and 24h");
        DrawCommandLine("/est colon default|always|hidden|slow|fast", "Change colon animation");
        DrawCommandLine("/est layout horizontal|vertical", "Change active profile layout");
        DrawCommandLine("/est preset classic|minimal|gold|retro", "Select a preset preview");
        DrawCommandLine("/est lock | /est unlock", "Lock or unlock clock movement");
        DrawCommandLine("/est profile next|list|set <n>|add <name>|rename <name>|delete", "Manage profiles");

        ImGui.PopTextWrapPos();
    }

    private void DrawCommandLine(string command, string description)
    {
        ImGui.Bullet();
        ImGui.TextColored(new Vector4(0.75f, 0.90f, 1f, 1f), command);
        ImGui.SameLine();
        ImGui.TextDisabled($"- {description}");
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
            bool pressedEnter = ImGui.InputFloat(label, ref inputValue, 0.0f, 0.0f, format, ImGuiInputTextFlags.EnterReturnsTrue);
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

        bool changed = ImGui.SliderFloat(label, ref value, min, max, format);
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            editingSliderId = label;
            editingSliderValue = value;
            focusSliderInputNextFrame = true;
        }

        return changed;
    }

    private void Section(string title, Action drawContent)
    {
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.82f, 0.42f, 1f), title);
        ImGui.Separator();
        ImGui.Spacing();
        drawContent();
        ImGui.Spacing();
    }

    private static void Help(string text)
    {
        ImGui.TextDisabled(text);
    }

    private static bool DrawCombo(string label, string[] items, ref int currentIndex)
    {
        bool changed = false;

        if (items.Length == 0)
            return false;

        currentIndex = Math.Clamp(currentIndex, 0, items.Length - 1);

        if (ImGui.BeginCombo(label, items[currentIndex]))
        {
            for (int i = 0; i < items.Length; i++)
            {
                bool isSelected = i == currentIndex;
                if (ImGui.Selectable(items[i], isSelected))
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
        var color = HexToColor(hexColor);
        var hover = MultiplyColor(color, 1.08f);
        var active = MultiplyColor(color, 0.92f);

        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
    }

    private void PopColoredButton()
    {
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
    }

    private void PushSmallRemoveButton()
    {
        var color = HexToColor("#ff5757");
        var hover = MultiplyColor(color, 1.08f);
        var active = MultiplyColor(color, 0.92f);

        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.One);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
    }

    private void PopSmallRemoveButton()
    {
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
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