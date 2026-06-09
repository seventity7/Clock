using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Clock.Services;

namespace Clock.Windows;

public sealed class ChatTimeHoverPopupWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly Func<string, string> t;
    private readonly Action<ChatTimeHoverService.ChatAlarmSetupRequest> setupAlarmFromChat;

    private ChatTimeHoverService.ChatTimeMatch match;
    private DateTime openedAt;
    private DateTime closeAt;

    public ChatTimeHoverPopupWindow(
        Configuration configuration,
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        Func<string, string> translate,
        Action<ChatTimeHoverService.ChatAlarmSetupRequest> setupAlarmFromChat)
        : base("###ClockChatTimeHoverPopup")
    {
        this.configuration = configuration;
        this.pluginInterface = pluginInterface;
        this.log = log;
        t = translate;
        this.setupAlarmFromChat = setupAlarmFromChat;

        // This is a Dalamud window instead of a config-window inline popup, so it floats over chat/config UI and never pushes layout around.
        Flags =
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoNav;

        RespectCloseHotkey = false;
        IsOpen = false;
    }

    public void Dispose() { }

    public void Show(ChatTimeHoverService.ChatTimeMatch chatMatch)
    {
        match = chatMatch;
        openedAt = DateTime.UtcNow;
        closeAt = openedAt.AddSeconds(Math.Clamp(configuration.ChatTimeHoverTooltipDurationSeconds, 2f, 5f));

        // Keep the hover window near the click but as its own overlay so it does not steal layout space from chat or the config window.
        Position = ImGui.GetMousePos() + new Vector2(14f, 14f);
        PositionCondition = ImGuiCond.Always;
        IsOpen = true;

    }

    private bool ShouldShowSetupButton(out string blockedReason)
    {
        blockedReason = string.Empty;

        if (!configuration.ChatTimeHoverShowAlarmSetupOption)
        {
            blockedReason = t("Alarm setup is disabled in settings.");
            return false;
        }

        if (match.TargetUtc <= DateTime.MinValue)
        {
            blockedReason = t("Alarm setup is not available for this time.");
            return false;
        }

        var targetUtc = DateTime.SpecifyKind(match.TargetUtc, DateTimeKind.Utc);
        var nowUtc = DateTime.UtcNow;
        // UTC comparison avoids DST/local-zone; the small buffer prevents creating an alarm that is already stale by the click frame.
        var allowed = targetUtc > nowUtc.AddSeconds(2);
        if (!allowed)
            blockedReason = t("Alarm setup is only available for future times.");

        return allowed;
    }

    private float GetPopupContentWidth()
    {
        var arrowWidth = 0f;
        using (pluginInterface.UiBuilder.IconFontHandle.Push())
            arrowWidth = ImGui.CalcTextSize("\uf061").X;

        // Width follows the rendered text instead of a fixed constant, that way short single-time conversions can stay compact.
        var lineWidth = ImGui.CalcTextSize(match.SourceDisplay).X;
        if (!string.IsNullOrWhiteSpace(match.TargetDisplay))
        {
            var spacing = ImGui.GetStyle().ItemSpacing.X * 2f;
            lineWidth += spacing + arrowWidth + ImGui.CalcTextSize(match.TargetDisplay).X;
        }

        var diffWidth = string.IsNullOrWhiteSpace(match.DifferenceText)
            ? 0f
            : ImGui.CalcTextSize(match.DifferenceText).X;

        var buttonWidth = ImGui.CalcTextSize(t("Create Alarm")).X + 18f;
        var width = Math.Max(lineWidth, Math.Max(diffWidth, buttonWidth));
        return Math.Clamp(width, 120f, 460f);
    }

    private bool DrawSetupButton(float width)
    {
        var label = t("Create Alarm");
        var height = ImGui.GetFrameHeight() + 4f;
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);
        var mouse = ImGui.GetMousePos();
        var hovered = mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;

        // This is drawn manually so the label can stay centered while still using the tighter dynamic tooltip width.
        var draw = ImGui.GetWindowDrawList();
        var bg = hovered
            ? ImGui.GetColorU32(new Vector4(0.32f, 0.42f, 0.32f, 0.95f))
            : ImGui.GetColorU32(new Vector4(0.18f, 0.24f, 0.18f, 0.92f));
        var border = ImGui.GetColorU32(new Vector4(0.52f, 0.72f, 0.52f, 1f));
        var text = ImGui.GetColorU32(new Vector4(0.88f, 1f, 0.88f, 1f));

        draw.AddRectFilled(min, max, bg, 4f);
        draw.AddRect(min, max, border, 4f);

        var textSize = ImGui.CalcTextSize(label);
        var textPos = min + new Vector2(
            Math.Max(6f, (width - textSize.X) * 0.5f),
            (height - textSize.Y) * 0.5f);
        draw.AddText(textPos, text, label);

        ImGui.Dummy(new Vector2(width, height));

        return hovered && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Left));
    }

    public override void Draw()
    {
        if (Math.Abs(ImGui.GetIO().MouseWheel) > 0.01f || ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            IsOpen = false;
            return;
        }

        var popupWidth = GetPopupContentWidth();

        ImGui.TextColored(new Vector4(1.0f, 0.86f, 0.25f, 1.0f), match.SourceDisplay);
        if (!string.IsNullOrWhiteSpace(match.TargetDisplay))
        {
            ImGui.SameLine();
            using (pluginInterface.UiBuilder.IconFontHandle.Push())
                ImGui.TextUnformatted("\uf061");

            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1.0f, 0.45f, 0.45f, 1.0f), match.TargetDisplay);
        }

        if (!string.IsNullOrWhiteSpace(match.DifferenceText))
            ImGui.TextDisabled(match.DifferenceText);

        if (ShouldShowSetupButton(out var blockedReason))
        {
            DrawFadeSeparator();
            if (DrawSetupButton(popupWidth))
            {
                setupAlarmFromChat(new ChatTimeHoverService.ChatAlarmSetupRequest(match.TargetLocal, match.TargetTimeZoneId));
                IsOpen = false;
                return;
            }
        }
        else if (!string.IsNullOrWhiteSpace(blockedReason))
        {
            ImGui.TextDisabled(blockedReason);
        }

        var mouse = ImGui.GetMousePos();
        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + ImGui.GetWindowSize();
        var mouseInside = mouse.X >= windowMin.X && mouse.X <= windowMax.X && mouse.Y >= windowMin.Y && mouse.Y <= windowMax.Y;

        var canClose = DateTime.UtcNow - openedAt > TimeSpan.FromMilliseconds(150);
        if (canClose && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right)) && !mouseInside)
            IsOpen = false;

        if (DateTime.UtcNow > closeAt && !mouseInside)
            IsOpen = false;
    }

    private static void DrawFadeSeparator()
    {
        var width = ImGui.GetContentRegionAvail().X;
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
            drawList.AddLine(new Vector2(pos.X + (width * t0), y), new Vector2(pos.X + (width * t1), y), ImGui.GetColorU32(new Vector4(styleColor.X, styleColor.Y, styleColor.Z, alpha)), 1f);
        }

        ImGui.Dummy(new Vector2(width, 9f));
    }

}
