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
    private const string HelpUrl = "https://github.com/seventity7/ESTClock";

    private readonly Plugin plugin;
    private readonly Configuration configuration;

    private bool isEditingTextSize;
    private bool focusTextSizeInput;
    private float textSizeInputValue;

    private ClockPreset presetSelection = ClockPreset.Classic;
    private string newProfileName = "";

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

            if (ImGui.BeginTabItem("Alarms"))
            {
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

            Help($"Badge automatically follows timezone: {configuration.SelectedTimeZone.ToShortText()}");
        });
    }

    private void DrawCompactTimezoneCombo()
    {
        var items = new[] { "EST", "PST", "UTC" };
        int zoneIndex = configuration.SelectedTimeZone switch
        {
            ClockTimeZone.EST => 0,
            ClockTimeZone.Pacific => 1,
            ClockTimeZone.Universal => 2,
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

            var message = configuration.AlarmEditorMessage;
            ImGui.SetNextItemWidth(142f);
            if (ImGui.InputText("Alarm Message", ref message, 128))
            {
                configuration.AlarmEditorMessage = message;
                configuration.Save();
            }

            PushColoredButton("#ffb300", Vector4.One);
            if (ImGui.Button("Add Alarm"))
            {
                configuration.AddAlarmFromEditor();
                configuration.Save();
            }
            PopColoredButton();

            ImGui.SameLine();

            PushColoredButton("#228700", Vector4.One);
            if (ImGui.Button("Test Alarm"))
            {
                var temp = new AlarmEntry
                {
                    DateTimeText = configuration.BuildAlarmEditorDateTimeText(),
                    Message = string.IsNullOrWhiteSpace(configuration.AlarmEditorMessage) ? "Alarm" : configuration.AlarmEditorMessage.Trim(),
                    TimeZone = configuration.SelectedTimeZone
                };

                plugin.TestAlarmOutput(temp.BuildTriggerMessage(configuration.TimeFormat));
            }
            PopColoredButton();

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
                    var removeId = $"Remove##{alarm.Id}";
                    if (ImGui.SmallButton(removeId))
                    {
                        configuration.RemoveAlarm(alarm.Id);
                        configuration.Save();
                        PopSmallRemoveButton();
                        break;
                    }
                    PopSmallRemoveButton();
                }
            }
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
            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(string.IsNullOrWhiteSpace(configuration.LastDetectedMaintenanceMessage)
                ? "No maintenance detected"
                : configuration.LastDetectedMaintenanceMessage);
            ImGui.PopTextWrapPos();

            if (configuration.HasDetectedMaintenanceTime)
            {
                ImGui.TextColored(
                    new Vector4(0.55f, 1f, 0.55f, 1f),
                    $"Detected maintenance time: {configuration.DetectedMaintenanceDateTimeText} ({configuration.SelectedTimeZone.ToShortText()})");
            }

            if (configuration.LastMaintenanceDetectionTimestampUtc != DateTime.MinValue)
            {
                ImGui.TextDisabled($"Last detection: {configuration.LastMaintenanceDetectionTimestampUtc:yyyy-MM-dd HH:mm:ss} UTC");
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
            if (ImGui.SliderFloat("Background Opacity", ref opacity, 0.0f, 1.0f, "%.2f"))
            {
                profile.ClockBackgroundOpacity = opacity;
                configuration.Save();
            }

            ImGui.SetNextItemWidth(122f);
            float borderOpacity = profile.BorderOpacity;
            if (ImGui.SliderFloat("Border Opacity", ref borderOpacity, 0.0f, 1.0f, "%.2f"))
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
            if (ImGui.SliderFloat("Icon Border Opacity", ref iconBorderOpacity, 0.0f, 1.0f, "%.2f"))
            {
                profile.IconBorderOpacity = iconBorderOpacity;
                configuration.Save();
            }
        });
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
        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, configuration.SelectedTimeZone);
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

        ImGui.SameLine();
        ImGui.TextDisabled(configuration.SelectedTimeZone.ToShortText());
    }

    private void DrawCommandsTab()
    {
        ImGui.PushTextWrapPos();

        ImGui.TextColored(new Vector4(1f, 0.85f, 0.45f, 1f), "Slash Commands");
        ImGui.Separator();
        ImGui.Spacing();

        DrawCommandLine("/est", "Toggle the clock window");
        DrawCommandLine("/est settings", "Open settings");
        DrawCommandLine("/est timezone est|pst|utc", "Change the main clock timezone");
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

        if (!isEditingTextSize)
        {
            ImGui.SetNextItemWidth(145f);
            float scale = profile.ClockTextScale;
            if (ImGui.SliderFloat("Text Size", ref scale, 0.5f, 5.0f, "%.2f"))
            {
                profile.ClockTextScale = scale;
                textSizeInputValue = scale;
                configuration.Save();
            }

            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                isEditingTextSize = true;
                focusTextSizeInput = true;
                textSizeInputValue = profile.ClockTextScale;
            }

            return;
        }

        if (focusTextSizeInput)
        {
            ImGui.SetKeyboardFocusHere();
            focusTextSizeInput = false;
        }

        ImGui.SetNextItemWidth(145f);

        bool pressedEnter = ImGui.InputFloat(
            "Text Size",
            ref textSizeInputValue,
            0.0f,
            0.0f,
            "%.2f",
            ImGuiInputTextFlags.EnterReturnsTrue);

        if (pressedEnter)
        {
            ApplyTextSizeInput();
            isEditingTextSize = false;
            return;
        }

        if (!ImGui.IsItemActive() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsItemHovered())
        {
            ApplyTextSizeInput();
            isEditingTextSize = false;
        }
    }

    private void ApplyTextSizeInput()
    {
        var profile = configuration.GetActiveProfile();
        textSizeInputValue = Math.Clamp(textSizeInputValue, 0.5f, 5.0f);
        profile.ClockTextScale = textSizeInputValue;
        configuration.Save();
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