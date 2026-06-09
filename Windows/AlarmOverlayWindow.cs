using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Clock.Windows;

public sealed class AlarmOverlayWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private bool editorOpen;
    private float slideT;
    private bool alarmSessionOpen;
    private bool alarmSessionClosing;
    private float alarmSessionAlpha;
    private Guid alarmSessionAlarmId;
    private string alarmSessionMessage = string.Empty;
    private string alarmSessionTimeText = string.Empty;
    private int alarmSessionSoundId;
    private bool alarmSessionShowSnooze = true;

    public AlarmOverlayWindow(Plugin plugin)
        : base("###ClockAlarmOverlay")
    {
        this.plugin = plugin;
        configuration = plugin.Configuration;

        Flags =
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoScrollbar;

        Size = new Vector2(328, 465);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(260, 360),
            MaximumSize = new Vector2(328, 465)
        };
    }

    public void ShowTriggeredAlarm(Guid alarmId, string message, int soundId, string timeText, bool showSnooze)
    {
        editorOpen = false;
        slideT = 0f;
        alarmSessionOpen = true;
        alarmSessionClosing = false;
        alarmSessionAlpha = 1f;
        alarmSessionAlarmId = alarmId;
        alarmSessionMessage = message ?? string.Empty;
        alarmSessionTimeText = timeText ?? string.Empty;
        alarmSessionSoundId = soundId;
        alarmSessionShowSnooze = showSnooze;
    }

    public bool HasTriggeredAlarmSession => alarmSessionOpen;

    private void ClearAlarmSession()
    {
        if (alarmSessionOpen || alarmSessionClosing)
            plugin.StopAlarmOverlaySessionSound();

        alarmSessionOpen = false;
        alarmSessionClosing = false;
        alarmSessionAlpha = 0f;
        alarmSessionAlarmId = Guid.Empty;
        alarmSessionMessage = string.Empty;
        alarmSessionTimeText = string.Empty;
        alarmSessionSoundId = 0;
        alarmSessionShowSnooze = true;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.005f, 0.005f, 0.007f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 1f, 0.07f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 18f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 0f);
    }

    public override void PostDraw()
    {
        if (!IsOpen)
            ClearAlarmSession();

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(3);
    }

    public override void Draw()
    {
        var target = editorOpen ? 1f : 0f;
        var dt = MathF.Min(ImGui.GetIO().DeltaTime, 1f / 30f);
        slideT += (target - slideT) * MathF.Min(1f, dt * 11.5f);
        if (MathF.Abs(slideT - target) < 0.001f)
            slideT = target;

        if (alarmSessionClosing)
        {
            alarmSessionAlpha = MathF.Max(0f, alarmSessionAlpha - dt * 4.2f);
            if (alarmSessionAlpha <= 0.001f)
            {
                alarmSessionOpen = false;
                alarmSessionClosing = false;
                alarmSessionAlarmId = Guid.Empty;
                alarmSessionMessage = string.Empty;
                alarmSessionTimeText = string.Empty;
                alarmSessionSoundId = 0;
                alarmSessionShowSnooze = true;
        alarmSessionShowSnooze = true;
            }
        }
        else if (alarmSessionOpen)
        {
            alarmSessionAlpha = MathF.Min(1f, alarmSessionAlpha + dt * 5.0f);
        }

        if (!alarmSessionOpen)
            DrawHeader();

        var area = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();
        var width = avail.X;

        if (alarmSessionOpen)
        {
            DrawTriggeredAlarmSession(area, avail);
        }
        else
        {
            var xOffset = -slideT * (width + 26f);
            if (ImGui.BeginChild("##ClockAlarmOverlayPages", avail, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                var basePos = ImGui.GetCursorScreenPos();

                ImGui.SetCursorScreenPos(new Vector2(basePos.X + xOffset, basePos.Y));
                if (ImGui.BeginChild("##ClockAlarmOverlayHistoryPage", new Vector2(width, avail.Y), false, ImGuiWindowFlags.NoScrollbar))
                {
                    // The overlay delegates alarm-row rendering to ConfigWindow so the standalone /alarms view and editor share selection/editing state.
                    if (plugin.ConfigWindow.DrawAlarmPanelAlarmHistoryOverlay())
                        editorOpen = true;
                }
                ImGui.EndChild();

                ImGui.SetCursorScreenPos(new Vector2(basePos.X + xOffset + width + 26f, basePos.Y));
                if (ImGui.BeginChild("##ClockAlarmOverlayEditorPage", new Vector2(width, avail.Y), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                {
                    ImGui.Dummy(new Vector2(1f, 6f));
                    if (plugin.ConfigWindow.DrawAlarmOverlayCreateContent(true))
                        editorOpen = false;
                }
                ImGui.EndChild();
            }
            ImGui.EndChild();
        }

        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var borderMin = windowPos + new Vector2(2f, 2f);
        var borderMax = windowPos + windowSize - new Vector2(2f, 2f);
        var borderDrawList = ImGui.GetWindowDrawList();
        borderDrawList.PushClipRectFullScreen();
        var borderColor = ImGui.GetColorU32(new Vector4(0.72f, 0.72f, 0.76f, 0.44f));
        borderDrawList.AddRect(borderMin, borderMax, borderColor, 17f, ImDrawFlags.None, 1.0f); // ClockAlarmOverlaySilverBorder
        var sideBorder = ImGui.GetColorU32(new Vector4(0.72f, 0.72f, 0.76f, 0.34f));
        borderDrawList.AddLine(new Vector2(borderMin.X + 1f, borderMin.Y + 16f), new Vector2(borderMin.X + 1f, borderMax.Y - 16f), sideBorder, 0.85f);
        borderDrawList.AddLine(new Vector2(borderMax.X - 1f, borderMin.Y + 16f), new Vector2(borderMax.X - 1f, borderMax.Y - 16f), sideBorder, 0.85f);
        borderDrawList.PopClipRect();
    }


    private void DrawTriggeredAlarmSession(Vector2 area, Vector2 avail)
    {
        var alpha = Math.Clamp(alarmSessionAlpha, 0f, 1f);
        var drawList = ImGui.GetWindowDrawList();
        var min = area;
        var max = area + avail;
        var center = min + avail * 0.5f;
        var time = (float)ImGui.GetTime();

        drawList.PushClipRect(min, max, true);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.005f, 0.005f, 0.007f, 0.94f * alpha)), 0f);
        DrawAlarmPulseCircles(drawList, center, MathF.Min(avail.X, avail.Y), time, alpha);
        drawList.PopClipRect();

        var shake = MathF.Round(MathF.Sin(time * 30f) * 4.0f + MathF.Sin(time * 53f) * 1.5f);
        using var largeIconFont = plugin.LockLargeAlarmIconFont();
        using (ImRaii.PushFont(largeIconFont?.ImFont ?? plugin.PluginInterface.UiBuilder.FontIcon))
        {
            var icon = FontAwesomeIcon.AlarmClock.ToIconString();
            var iconSize = ImGui.CalcTextSize(icon);
            var iconPos = new Vector2(MathF.Round(center.X - iconSize.X * 0.5f + shake), MathF.Round(center.Y - 154f));
            drawList.AddText(iconPos, ImGui.GetColorU32(new Vector4(1f, 0.68f, 0.12f, 0.96f * alpha)), icon);
        }

        if (!string.IsNullOrWhiteSpace(alarmSessionTimeText))
        {
            using var alarmTimeFont = plugin.LockAlarmSessionDigitalFont();
            using (ImRaii.PushFont(alarmTimeFont?.ImFont ?? ImGui.GetFont()))
            {
                var timeSize = ImGui.CalcTextSize(alarmSessionTimeText);
                var timePos = new Vector2(MathF.Round(center.X - timeSize.X * 0.5f), MathF.Round(center.Y - 70f));
                drawList.AddText(timePos, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.98f * alpha)), alarmSessionTimeText);
            }
        }

        var verticalShift = alarmSessionShowSnooze ? 0f : 22f;
        var title = string.IsNullOrWhiteSpace(alarmSessionMessage) ? plugin.T("Alarm") : alarmSessionMessage.Trim();
        var titleSize = ImGui.CalcTextSize(title);
        drawList.AddText(new Vector2(MathF.Round(center.X - titleSize.X * 0.5f), MathF.Round(center.Y - 28f + verticalShift)), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.98f * alpha)), title);

        if (alarmSessionShowSnooze)
        {
            var snoozeSize = new Vector2(MathF.Min(190f, avail.X - 56f), 34f);
            var snoozeMin = new Vector2(center.X - snoozeSize.X * 0.5f, center.Y + 20f);
            if (DrawAlarmSessionButton("##ClockAlarmSessionSnooze", snoozeMin, snoozeSize, plugin.T("Snooze"), new Vector4(1f, 0.68f, 0.12f, 0.96f * alpha), new Vector4(1f, 0.82f, 0.32f, 1f * alpha), new Vector4(0f, 0f, 0f, 0.95f * alpha)))
            {
                plugin.SnoozeAlarmFromOverlay(alarmSessionAlarmId, 10);
                BeginAlarmSessionDismiss();
            }
        }

        var closeSize = new Vector2(MathF.Min(190f, avail.X - 56f), 30f);
        var closeMin = new Vector2(center.X - closeSize.X * 0.5f, max.Y - closeSize.Y - 12f);
        if (DrawAlarmSessionButton("##ClockAlarmSessionClose", closeMin, closeSize, plugin.T("Close"), new Vector4(0.30f, 0.30f, 0.34f, 0.88f * alpha), new Vector4(0.40f, 0.40f, 0.45f, 0.92f * alpha), new Vector4(1f, 1f, 1f, 0.92f * alpha)))
        {
            plugin.DismissAlarmOverlaySession(alarmSessionAlarmId);
            BeginAlarmSessionDismiss();
        }
    }

    private void DrawAlarmPulseCircles(ImDrawListPtr drawList, Vector2 center, float baseSize, float time, float alpha)
    {
        for (var i = 0; i < 3; i++)
        {
            var phase = (time * 0.42f + i / 3f) % 1f;
            var fadeIn = MathF.Min(1f, phase * 3f);
            var fadeOut = 1f - MathF.Max(0f, phase - 0.55f) / 0.45f;
            var pulseAlpha = Math.Clamp(MathF.Min(fadeIn, fadeOut), 0f, 1f) * alpha;
            var outer = baseSize * (0.48f + phase * 0.46f);
            var inner = baseSize * (0.30f + phase * 0.32f);
            drawList.AddCircleFilled(center, outer, ImGui.GetColorU32(new Vector4(0.30f, 0.30f, 0.34f, 0.14f * pulseAlpha)), 96);
            drawList.AddCircleFilled(center, inner, ImGui.GetColorU32(new Vector4(0.42f, 0.42f, 0.46f, 0.16f * pulseAlpha)), 96);
        }
    }

    private bool DrawAlarmSessionButton(string id, Vector2 pos, Vector2 size, string text, Vector4 color, Vector4 hoverColor, Vector4 textColor)
    {
        ImGui.SetCursorScreenPos(pos);
        ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked();
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(pos, pos + size, ImGui.GetColorU32(hovered ? hoverColor : color), size.Y * 0.5f);
        var textSize = ImGui.CalcTextSize(text);
        drawList.AddText(new Vector2(MathF.Round(pos.X + (size.X - textSize.X) * 0.5f), MathF.Round(pos.Y + (size.Y - textSize.Y) * 0.5f)), ImGui.GetColorU32(textColor), text);
        return clicked;
    }

    private void BeginAlarmSessionDismiss()
    {
        alarmSessionClosing = true;
    }

    private void DrawHeader()
    {
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gold = new Vector4(1f, 0.68f, 0.12f, 1f);
        var goldHover = new Vector4(1f, 0.82f, 0.32f, 1f);
        var red = new Vector4(1f, 0.42f, 0.42f, 1f);
        var centerY = start.Y + 12f;

        ImGui.Dummy(new Vector2(width, 24f));

        var closeHovered = false;
        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var close = "\uf410";
            var closeSize = ImGui.CalcTextSize(close);
            var closeCenter = new Vector2(start.X + 10f, centerY);
            var closePos = new Vector2(MathF.Round(closeCenter.X - closeSize.X * 0.5f), MathF.Round(closeCenter.Y - closeSize.Y * 0.5f));
            ImGui.SetCursorScreenPos(closeCenter - new Vector2(12f, 12f));
            ImGui.InvisibleButton("##ClockAlarmOverlayClose", new Vector2(24f, 24f));
            closeHovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked())
                IsOpen = false;
            drawList.AddText(closePos, ImGui.GetColorU32(closeHovered ? goldHover : gold), close);
            if (closeHovered)
                drawList.AddCircle(closeCenter, 12f, ImGui.GetColorU32(new Vector4(goldHover.X, goldHover.Y, goldHover.Z, 0.35f)), 24, 1.2f);
        }
        if (closeHovered)
            ImGui.SetTooltip(plugin.T("Close"));

        var leftTextX = start.X + 26f;

        if (editorOpen)
        {
            var cancelText = plugin.T("Cancel");
            var cancelSize = ImGui.CalcTextSize(cancelText);
            var cancelPos = new Vector2(leftTextX, MathF.Round(centerY - cancelSize.Y * 0.5f));
            ImGui.SetCursorScreenPos(cancelPos - new Vector2(6f, 4f));
            ImGui.InvisibleButton("##ClockAlarmOverlayCancel", cancelSize + new Vector2(12f, 8f));
            var hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked())
            {
                plugin.ConfigWindow.CancelAlarmOverlayEditor();
                editorOpen = false;
            }

            drawList.AddText(cancelPos, ImGui.GetColorU32(hovered ? goldHover : gold), cancelText);
            if (hovered)
                drawList.AddLine(new Vector2(cancelPos.X, cancelPos.Y + cancelSize.Y + 1f), new Vector2(cancelPos.X + cancelSize.X, cancelPos.Y + cancelSize.Y + 1f), ImGui.GetColorU32(goldHover), 1f);

            var saveText = plugin.T("Save");
            var saveSize = ImGui.CalcTextSize(saveText);
            var savePos = new Vector2(start.X + width - saveSize.X - 2f, MathF.Round(centerY - saveSize.Y * 0.5f));
            ImGui.SetCursorScreenPos(savePos - new Vector2(6f, 4f));
            ImGui.InvisibleButton("##ClockAlarmOverlaySave", saveSize + new Vector2(12f, 8f));
            var saveHovered = ImGui.IsItemHovered();
            var saveBlocked = plugin.ConfigWindow.IsAlarmOverlaySaveBlocked;
            if (saveHovered && saveBlocked)
                ImGui.SetTooltip(plugin.T("Can't create a alarm for the past!"));
            if (ImGui.IsItemClicked() && !saveBlocked && plugin.ConfigWindow.CommitAlarmOverlayEditorFromHeader())
                editorOpen = false;
            var saveColor = saveBlocked ? red : (saveHovered ? goldHover : gold);
            drawList.AddText(savePos, ImGui.GetColorU32(saveColor), saveText);
            if (saveHovered && !saveBlocked)
                drawList.AddLine(new Vector2(savePos.X, savePos.Y + saveSize.Y + 1f), new Vector2(savePos.X + saveSize.X, savePos.Y + saveSize.Y + 1f), ImGui.GetColorU32(goldHover), 1f);
        }
        else
        {
            var settingsText = plugin.T("Settings");
            var settingsSize = ImGui.CalcTextSize(settingsText);
            var settingsPos = new Vector2(leftTextX, MathF.Round(centerY - settingsSize.Y * 0.5f));
            ImGui.SetCursorScreenPos(settingsPos - new Vector2(6f, 4f));
            ImGui.InvisibleButton("##ClockAlarmOverlaySettings", settingsSize + new Vector2(12f, 8f));
            var settingsHovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked())
                plugin.ConfigWindow.IsOpen = true;

            drawList.AddText(settingsPos, ImGui.GetColorU32(settingsHovered ? goldHover : gold), settingsText);
            if (settingsHovered)
                drawList.AddLine(new Vector2(settingsPos.X, settingsPos.Y + settingsSize.Y + 1f), new Vector2(settingsPos.X + settingsSize.X, settingsPos.Y + settingsSize.Y + 1f), ImGui.GetColorU32(goldHover), 1f);

            var plusHovered = false;
            var deleteMode = plugin.ConfigWindow.HasSelectedAlarmPanelAlarms;
            using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
            {
                var plus = deleteMode ? "\uf1f8" : "\uf067";
                var plusSize = ImGui.CalcTextSize(plus);
                var plusCenter = new Vector2(start.X + width - 10f, centerY);
                var plusPos = new Vector2(MathF.Round(plusCenter.X - plusSize.X * 0.5f), MathF.Round(plusCenter.Y - plusSize.Y * 0.5f));
                ImGui.SetCursorScreenPos(plusCenter - new Vector2(12f, 12f));
                ImGui.InvisibleButton("##ClockAlarmOverlayPlus", new Vector2(24f, 24f));
                plusHovered = ImGui.IsItemHovered();
                if (ImGui.IsItemClicked())
                {
                    if (deleteMode)
                    {
                        plugin.ConfigWindow.DeleteSelectedAlarmPanelAlarms();
                    }
                    else
                    {
                        plugin.ConfigWindow.BeginNewAlarmFromOverlay();
                        editorOpen = true;
                    }
                }

                var plusColor = deleteMode ? red : gold;
                drawList.AddText(plusPos, ImGui.GetColorU32(plusHovered && !deleteMode ? goldHover : plusColor), plus);
                if (plusHovered)
                    drawList.AddCircle(plusCenter, 12f, ImGui.GetColorU32(deleteMode ? new Vector4(1f, 0.42f, 0.42f, 0.32f) : new Vector4(goldHover.X, goldHover.Y, goldHover.Z, 0.35f)), 24, 1.2f);
            }
            if (plusHovered && deleteMode)
                ImGui.SetTooltip(plugin.T("Delete Selected"));
        }

        var helpHovered = false;
        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var help = "\uf059";
            var helpButtonSize = new Vector2(24f, 24f);
            var helpCenter = new Vector2(start.X + width * 0.5f, centerY);
            ImGui.SetCursorScreenPos(helpCenter - helpButtonSize * 0.5f);
            ImGui.InvisibleButton("##ClockAlarmOverlayHelp", helpButtonSize);
            helpHovered = ImGui.IsItemHovered();
            if (helpHovered)
                drawList.AddCircleFilled(helpCenter, 12f, ImGui.GetColorU32(new Vector4(0.16f, 0.16f, 0.18f, 0.95f)), 32);
            var helpSize = ImGui.CalcTextSize(help);
            drawList.AddText(new Vector2(MathF.Round(helpCenter.X - helpSize.X * 0.5f), MathF.Round(helpCenter.Y - helpSize.Y * 0.5f)), ImGui.GetColorU32(helpHovered ? goldHover : gold), help);
        }
        if (helpHovered)
            DrawAlarmHelpTooltip();


        var triggerHovered = false;
        using (plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var triggerIcon = configuration.OpenAlarmsOverlayOnAlarmTrigger ? "\ue509" : "\ue50b";
            var triggerSize = ImGui.CalcTextSize(triggerIcon);
            var triggerCenter = new Vector2(start.X + width * 0.5f + 26f, centerY);
            var triggerPos = new Vector2(MathF.Round(triggerCenter.X - triggerSize.X * 0.5f), MathF.Round(triggerCenter.Y - triggerSize.Y * 0.5f));
            ImGui.SetCursorScreenPos(triggerCenter - new Vector2(12f, 12f));
            ImGui.InvisibleButton("##ClockAlarmOverlayTriggerWindowToggle", new Vector2(24f, 24f));
            triggerHovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked())
            {
                configuration.OpenAlarmsOverlayOnAlarmTrigger = !configuration.OpenAlarmsOverlayOnAlarmTrigger;
                configuration.Save();
            }

            drawList.AddText(triggerPos, ImGui.GetColorU32(triggerHovered ? goldHover : gold), triggerIcon);
            if (triggerHovered)
                drawList.AddCircle(triggerCenter, 12f, ImGui.GetColorU32(new Vector4(goldHover.X, goldHover.Y, goldHover.Z, 0.35f)), 24, 1.2f);
        }
        if (triggerHovered)
        {
            if (configuration.OpenAlarmsOverlayOnAlarmTrigger)
                ImGui.SetTooltip(plugin.T("This window will open") + "\n" + plugin.T("when an alarm goes off"));
            else
                ImGui.SetTooltip(plugin.T("Click to make this window open") + "\n" + plugin.T("when an alarm goes off"));
        }

        var sepY = start.Y + 23f;
        drawList.AddLine(new Vector2(start.X - 10f, sepY), new Vector2(start.X + width + 10f, sepY), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)), 1f);
    }

    private void DrawAlarmHelpTooltip()
    {
        var tooltipGold = new Vector4(1f, 0.92f, 0.08f, 1f);
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(plugin.T("Create a new alarm clicking the "));
        ImGui.SameLine(0f, 0f);
        ImGui.TextColored(tooltipGold, "+");
        ImGui.SameLine(0f, 0f);
        ImGui.TextUnformatted(plugin.T(" button."));
        ImGui.TextUnformatted(plugin.T("Hold "));
        ImGui.SameLine(0f, 0f);
        ImGui.TextColored(tooltipGold, "CTRL");
        ImGui.SameLine(0f, 0f);
        ImGui.TextUnformatted(plugin.T(" to select and delete multiple alarms."));
        ImGui.TextUnformatted(plugin.T("Click a alarm for edit it."));
        ImGui.EndTooltip();
    }


}
