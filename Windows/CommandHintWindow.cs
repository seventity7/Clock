using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;

// Kept the window separate to avoid crowding the main plugin class.


namespace Clock.Windows;

// Lightweight helper window for command reminders.
public sealed class CommandHintWindow : Window, IDisposable
{
    private readonly Func<string, string> t;

    // Command list here, some rows are examples/placeholders
    private readonly List<HintLine> commands = new()
    {
        new("/clock", "Open Clock settings", "/clock"),
        new("/clock on", "Show the clock overlay", "/clock on"),
        new("/clock off", "Hide the clock overlay", "/clock off"),
        new("/clock timezone <timezone>", "Change the main clock timezone", "/clock timezone "),
        new("/clock format 12|24|12s|24s|weekday|date", "Change the time format", "/clock format "),
        new("/clock colon default|always|hidden|slow|fast", "Change colon animation", "/clock colon "),
        new("/clock layout horizontal|vertical", "Change the active profile layout", "/clock layout "),
        new("/clock <timezone1> to <timezone2>", "Compare the current time between two timezones", "/clock "),
        new("/clock lock", "Lock clock movement", "/clock lock"),
        new("/clock unlock", "Unlock clock movement", "/clock unlock"),
        new("/clock profile next|list|set <n>|add <name>|rename <name>|delete", "Manage profiles", "/clock profile "),
        new("/alarms", "Open alarm overlay", "/alarms"),
    };

    private List<HintLine> visible = new();
    private string typed = string.Empty;
    private Vector2 anchor;
    private Vector2 lastSize = new(430f, 190f);
    private bool hideExact;
    private IDisposable?[]? styleScopes;

    public CommandHintWindow(Func<string, string> t)
        : base("###ClockCommandHints")
    {
        this.t = t;
        Flags = ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.AlwaysAutoResize |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNavFocus |
                ImGuiWindowFlags.NoSavedSettings;

        RespectCloseHotkey = false;
        IsOpen = false;
        Update(string.Empty, Vector2.Zero);
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

    public void Dispose()
    {
    }

    public void Update(string currentText, Vector2 textInputAnchor)
    {
        // Trim is only at the end so partially typed command arguments are still filtered naturally while typing.
        typed = (currentText ?? string.Empty).TrimEnd();
        anchor = textInputAnchor;
        hideExact = IsCompleteCommand(typed);

        // The popup is only a hint list; it should disappear once the typed command is completed.
        var query = typed.Trim();
        visible = commands
            .Where(line => Matches(line, query) || (query.StartsWith("/clock", StringComparison.OrdinalIgnoreCase) && line.Command == "/alarms"))
            .OrderBy(line => line.Command == "/alarms" && query.StartsWith("/clock", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(line => line.SortText.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(line => line.SortText.Length)
            .Take(12)
            .ToList();
    }

    public override void PreDraw()
    {
        // Keep the hints above the chat input. We do not read the ChatLog addon directly here,
        // so the fallback leaves a larger bottom gap and only overlaps the scrollback area.
        var viewport = ImGui.GetMainViewport();
        var pos = anchor;
        if (pos.X <= 0f && pos.Y <= 0f)
            pos = new Vector2(viewport.Pos.X + 24f, viewport.Pos.Y + viewport.Size.Y - lastSize.Y - 168f);
        else
            pos = new Vector2(pos.X, MathF.Max(viewport.Pos.Y + 24f, pos.Y - lastSize.Y - 14f));

        pos.X = MathF.Min(pos.X, viewport.Pos.X + viewport.Size.X - lastSize.X - 12f);
        pos.X = MathF.Max(viewport.Pos.X + 12f, pos.X);

        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);

        // Keep this styling local to the hint popup to keep the normal Clock windows/buttons themes.
        DisposeStyleScopes();
        styleScopes =
        [
            ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(8f, 7f)),
            ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 3f)),
            ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 1f),
            ImRaii.PushColor(ImGuiCol.WindowBg, new Vector4(0.03f, 0.03f, 0.035f, 0.92f)),
            ImRaii.PushColor(ImGuiCol.Border, new Vector4(0.95f, 0.74f, 0.32f, 0.72f))
        ];
    }

    public override void PostDraw()
    {
        DisposeStyleScopes();
    }

    private void DisposeStyleScopes()
    {
        if (styleScopes == null)
            return;

        for (var i = styleScopes.Length - 1; i >= 0; i--)
            styleScopes[i]?.Dispose();

        styleScopes = null;
    }
    // Draw paths are intentionally explicit; tiny UI changes are easier to spot this way.

    public override void Draw()
    {
        if (visible.Count == 0)
        {
            ImGui.TextDisabled(t("No Clock commands found"));
            lastSize = ImGui.GetWindowSize();
            return;
        }

        ImGui.TextColored(new Vector4(1f, 0.84f, 0.42f, 1f), t("Clock commands"));
        DrawFadeSeparator();

        foreach (var line in visible)
        {
            // The translated description is intentionally muted.
            ImGui.TextUnformatted(line.Command);
            ImGui.Indent(16f);
            ImGui.TextDisabled(t(line.Description));
            ImGui.Unindent(16f);
            ImGui.Spacing();
        }

        lastSize = ImGui.GetWindowSize();
    }

    public override bool DrawConditions()
    {
        return IsOpen && !hideExact && (typed.StartsWith("/clock", StringComparison.OrdinalIgnoreCase) || typed.StartsWith("/alarms", StringComparison.OrdinalIgnoreCase)) && visible.Count > 0;
    }

    private bool IsCompleteCommand(string text)
    {
        // Exact static commands should close the popup but example rows with <arguments> stay visible
        // until the user provides enough text to make the command meaningful.
        if (!string.Equals(text, "/clock", StringComparison.OrdinalIgnoreCase) &&
            commands.Any(line => string.Equals(line.InsertText.TrimEnd(), text, StringComparison.OrdinalIgnoreCase) && !line.Command.Contains('<')))
            return true;

        var lower = text.ToLowerInvariant();

        // Timezone comparisons use free-form aliases, so both sides of "to" are treated as completion.
        if (lower.StartsWith("/clock ") && lower.Contains(" to "))
        {
            var bits = lower["/clock ".Length..].Split(" to ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (bits.Length == 2 && bits[0].Length > 0 && bits[1].Length > 0)
                return true;
        }

        return false;
    }

    private static bool Matches(HintLine line, string query)
    {
        // This keeps the list shrinking predictably as the user types.
        if (string.IsNullOrWhiteSpace(query) || string.Equals(query, "/clock", StringComparison.OrdinalIgnoreCase))
            return true;

        if (line.SortText.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (line.Command.Contains('<') && query.StartsWith("/clock ", StringComparison.OrdinalIgnoreCase))
        {
            var afterClock = query["/clock ".Length..].TrimStart();
            return line.Command.StartsWith("/clock <", StringComparison.OrdinalIgnoreCase) && afterClock.Length > 0;
        }

        return false;
    }

    private readonly record struct HintLine(string Command, string Description, string InsertText)
    {
        public string SortText => Command.Replace("<timezone1>", "").Replace("<timezone2>", "").Replace("<timezone>", "").TrimEnd();
    }
}
