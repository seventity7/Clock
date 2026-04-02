using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ESTClock.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private const float MinuteDigitGap = 0.3f;
    private const float SuffixHorizontalOffset = -0.3f;
    private const string ColonText = " : ";

    public MainWindow(Plugin plugin)
        : base("###ESTClockMainWindow")
    {
        this.plugin = plugin;

        Flags =
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoDecoration;

        RespectCloseHotkey = false;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(50, 20),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (plugin.Configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
            Flags &= ~ImGuiWindowFlags.NoResize;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
            Flags |= ImGuiWindowFlags.NoResize;
        }

        var profile = plugin.Configuration.GetActiveProfile();
        var styleMetrics = GetStyleMetrics(profile.DisplayStyle);
        var mainScale = MathF.Max(0.5f, profile.ClockTextScale);
        var badgeScale = MathF.Max(0.35f, mainScale * styleMetrics.BadgeScaleMultiplier);

        var parts = GetClockParts();
        var badgeText = plugin.Configuration.SelectedTimeZone.ToShortText();
        var badgeSize = profile.ShowIcon
            ? CalculateScaledTextSize(badgeText, badgeScale)
            : Vector2.Zero;

        var layout = GetClockLayoutMetrics(mainScale, parts);
        Vector2 totalSize;

        if (profile.LayoutMode == ClockLayoutMode.Horizontal)
        {
            var iconWidth = profile.ShowIcon ? (styleMetrics.BadgePaddingX * 2.0f) + badgeSize.X : 0.0f;
            var gapWidth = profile.ShowIcon ? styleMetrics.BadgeGap : 0.0f;
            var contentHeight = profile.ShowIcon
                ? MathF.Max(layout.TotalSize.Y, badgeSize.Y + (styleMetrics.BadgePaddingY * 2.0f))
                : layout.TotalSize.Y;

            totalSize = new Vector2(
                styleMetrics.MainPaddingX + iconWidth + gapWidth + layout.TotalSize.X + styleMetrics.MainPaddingX,
                styleMetrics.MainPaddingY + contentHeight + styleMetrics.MainPaddingY
            );
        }
        else
        {
            var badgeRotatedWidth = profile.ShowIcon ? GetBadgeVerticalWidth(badgeSize, styleMetrics) : 0f;

            float fullWidth = badgeRotatedWidth +
                              (badgeRotatedWidth > 0 ? styleMetrics.BadgeGap : 0f) +
                              layout.TotalSize.X;

            totalSize = new Vector2(
                styleMetrics.MainPaddingX + fullWidth + styleMetrics.MainPaddingX,
                styleMetrics.MainPaddingY + layout.TotalSize.Y + styleMetrics.MainPaddingY
            );
        }

        ImGui.SetNextWindowSize(totalSize, ImGuiCond.Always);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
    }

    public override void Draw()
    {
        var profile = plugin.Configuration.GetActiveProfile();
        var styleMetrics = GetStyleMetrics(profile.DisplayStyle);

        var mainScale = MathF.Max(0.5f, profile.ClockTextScale);
        var badgeScale = MathF.Max(0.35f, mainScale * styleMetrics.BadgeScaleMultiplier);

        var parts = GetClockParts();
        var badgeText = plugin.Configuration.SelectedTimeZone.ToShortText();

        var badgeTextSize = profile.ShowIcon
            ? CalculateScaledTextSize(badgeText, badgeScale)
            : Vector2.Zero;

        var layout = GetClockLayoutMetrics(mainScale, parts);

        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var drawList = ImGui.GetWindowDrawList();

        var panelMin = windowPos;
        var panelMax = windowPos + windowSize;

        var panelColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
            profile.ClockBackgroundColor.X,
            profile.ClockBackgroundColor.Y,
            profile.ClockBackgroundColor.Z,
            profile.ClockBackgroundOpacity));

        drawList.AddRectFilled(panelMin, panelMax, panelColor, styleMetrics.MainRounding);

        if (profile.ShowBorder)
        {
            var borderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                profile.BorderColor.X,
                profile.BorderColor.Y,
                profile.BorderColor.Z,
                profile.BorderOpacity));
            drawList.AddRect(panelMin, panelMax, borderColor, styleMetrics.MainRounding, ImDrawFlags.None, styleMetrics.BorderThickness);
        }

        if (profile.LayoutMode == ClockLayoutMode.Horizontal)
            DrawHorizontal(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, windowPos, windowSize);
        else
            DrawVertical(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, windowPos);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);
    }

    private void DrawHorizontal(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        ClockParts parts,
        string badgeText,
        Vector2 badgeTextSize,
        ClockLayoutMetrics layout,
        float mainScale,
        float badgeScale,
        Vector2 windowPos,
        Vector2 windowSize)
    {
        float currentX = windowPos.X + styleMetrics.MainPaddingX;

        if (profile.ShowIcon)
        {
            var badgeHeight = badgeTextSize.Y + (styleMetrics.BadgePaddingY * 2.0f);
            var badgeWidth = badgeTextSize.X + (styleMetrics.BadgePaddingX * 2.0f);
            var contentHeight = MathF.Max(layout.TotalSize.Y, badgeHeight);
            var contentTop = windowPos.Y + MathF.Floor((windowSize.Y - contentHeight) * 0.5f);

            var badgeMin = new Vector2(
                currentX,
                contentTop + MathF.Floor((contentHeight - badgeHeight) * 0.5f) + styleMetrics.BadgeVerticalOffset
            );

            var badgeMax = new Vector2(
                badgeMin.X + badgeWidth,
                badgeMin.Y + badgeHeight
            );

            DrawBadge(profile, styleMetrics, badgeText, badgeScale, badgeMin, badgeMax, windowPos);
            currentX = badgeMax.X + styleMetrics.BadgeGap;
        }

        var timePos = new Vector2(
            currentX,
            windowPos.Y + MathF.Floor((windowSize.Y - layout.TotalSize.Y) * 0.5f)
        );

        DrawClockHorizontal(
            parts,
            timePos - windowPos,
            mainScale,
            profile.ClockTextColor,
            profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0),
            styleMetrics);
    }

    private void DrawVertical(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        ClockParts parts,
        string badgeText,
        Vector2 badgeTextSize,
        ClockLayoutMetrics layout,
        float mainScale,
        float badgeScale,
        Vector2 windowPos)
    {
        float lineHeight = CalculateScaledTextSize("8", mainScale).Y;
        float badgeRotatedWidth = profile.ShowIcon ? GetBadgeVerticalWidth(badgeTextSize, styleMetrics) : 0f;
        float badgeRotatedHeight = profile.ShowIcon ? GetBadgeVerticalHeight(badgeTextSize, styleMetrics, badgeText, badgeScale) : 0f;

        float centerStartX = windowPos.X + styleMetrics.MainPaddingX + badgeRotatedWidth + (badgeRotatedWidth > 0 ? styleMetrics.BadgeGap : 0f);
        float centerLineWidth = layout.TotalSize.X;

        var leftDigits = GetVerticalLeftLines(parts.Left);
        float startY = windowPos.Y + styleMetrics.MainPaddingY;

        float leftBlockHeight = leftDigits.Length * lineHeight;
        float colonY = startY + leftBlockHeight;
        float minuteStartY = colonY + lineHeight;

        if (profile.ShowIcon)
        {
            float badgeMinY = colonY - MathF.Floor((badgeRotatedHeight - lineHeight) * 0.5f);
            var badgeMin = new Vector2(
                windowPos.X + styleMetrics.MainPaddingX,
                badgeMinY + styleMetrics.BadgeVerticalOffset
            );

            var badgeMax = new Vector2(
                badgeMin.X + badgeRotatedWidth,
                badgeMin.Y + badgeRotatedHeight
            );

            DrawBadgeVertical(profile, styleMetrics, badgeText, badgeScale, badgeMin, badgeMax, windowPos);
        }

        for (int i = 0; i < leftDigits.Length; i++)
        {
            DrawCenteredLine(
                leftDigits[i],
                centerStartX,
                centerLineWidth,
                startY + (i * lineHeight),
                mainScale,
                profile,
                styleMetrics);
        }

        var colonVisible = ShouldShowColon(plugin.Configuration.ColonAnimation);
        DrawCenteredLine(
            ColonText,
            centerStartX,
            centerLineWidth,
            colonY,
            mainScale,
            profile,
            styleMetrics,
            colonVisible);

        DrawCenteredLine(parts.MinuteLeft, centerStartX, centerLineWidth, minuteStartY, mainScale, profile, styleMetrics);
        DrawCenteredLine(parts.MinuteRight, centerStartX, centerLineWidth, minuteStartY + lineHeight, mainScale, profile, styleMetrics);
    }

    private static float GetBadgeVerticalWidth(Vector2 badgeTextSize, StyleMetrics styleMetrics)
    {
        return badgeTextSize.Y + (styleMetrics.BadgePaddingX * 2.0f);
    }

    private static float GetBadgeVerticalHeight(Vector2 badgeTextSize, StyleMetrics styleMetrics, string badgeText, float badgeScale)
    {
        float totalLetterHeight = 0f;
        foreach (var letter in badgeText)
            totalLetterHeight += ImGui.CalcTextSize(letter.ToString()).Y * badgeScale;

        return totalLetterHeight + (styleMetrics.BadgePaddingY * 2.0f) + 2.0f;
    }

    private string[] GetVerticalLeftLines(string left)
    {
        if (string.IsNullOrWhiteSpace(left))
            return new[] { "0" };

        return left.Select(c => c.ToString()).ToArray();
    }

    private void DrawCenteredLine(
        string text,
        float startX,
        float availableWidth,
        float lineY,
        float scale,
        ClockProfile profile,
        StyleMetrics styleMetrics,
        bool visible = true)
    {
        var size = CalculateScaledTextSize(text, scale);
        var pos = new Vector2(
            startX + MathF.Floor((availableWidth - size.X) * 0.5f),
            lineY
        );

        var color = visible
            ? profile.ClockTextColor
            : new Vector4(profile.ClockTextColor.X, profile.ClockTextColor.Y, profile.ClockTextColor.Z, 0f);

        var shadow = visible && profile.ShowShadowText
            ? profile.ClockShadowColor
            : new Vector4(0, 0, 0, 0);

        DrawOutlinedTextScaled(text, pos - ImGui.GetWindowPos(), scale, color, shadow, styleMetrics);
    }

    private void DrawBadge(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        string badgeText,
        float badgeScale,
        Vector2 badgeMin,
        Vector2 badgeMax,
        Vector2 windowPos)
    {
        var drawList = ImGui.GetWindowDrawList();

        var badgeFillColor = ImGui.ColorConvertFloat4ToU32(profile.IconBackgroundColor);
        drawList.AddRectFilled(badgeMin, badgeMax, badgeFillColor, styleMetrics.BadgeRounding);

        if (profile.ShowIconBorder)
        {
            var badgeBorderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                profile.IconBorderColor.X,
                profile.IconBorderColor.Y,
                profile.IconBorderColor.Z,
                profile.IconBorderOpacity));
            drawList.AddRect(badgeMin, badgeMax, badgeBorderColor, styleMetrics.BadgeRounding, ImDrawFlags.None, styleMetrics.BorderThickness);
        }

        var badgeTextPos = new Vector2(
            badgeMin.X + styleMetrics.BadgePaddingX,
            badgeMin.Y + styleMetrics.BadgePaddingY
        );

        DrawTextScaled(badgeText, badgeTextPos - windowPos, badgeScale, profile.IconTextColor);
    }

    private void DrawBadgeVertical(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        string badgeText,
        float badgeScale,
        Vector2 badgeMin,
        Vector2 badgeMax,
        Vector2 windowPos)
    {
        var drawList = ImGui.GetWindowDrawList();

        var badgeFillColor = ImGui.ColorConvertFloat4ToU32(profile.IconBackgroundColor);
        drawList.AddRectFilled(badgeMin, badgeMax, badgeFillColor, styleMetrics.BadgeRounding);

        if (profile.ShowIconBorder)
        {
            var badgeBorderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                profile.IconBorderColor.X,
                profile.IconBorderColor.Y,
                profile.IconBorderColor.Z,
                profile.IconBorderOpacity));
            drawList.AddRect(badgeMin, badgeMax, badgeBorderColor, styleMetrics.BadgeRounding, ImDrawFlags.None, styleMetrics.BorderThickness);
        }

        var letters = badgeText.Select(c => c.ToString()).ToArray();
        if (letters.Length == 0)
            return;

        float totalHeight = 0f;
        foreach (var letter in letters)
            totalHeight += CalculateScaledTextSize(letter, badgeScale).Y;

        float availableHeight = badgeMax.Y - badgeMin.Y;
        float startY = badgeMin.Y + MathF.Floor((availableHeight - totalHeight) * 0.5f);
        float centerX = badgeMin.X + MathF.Floor((badgeMax.X - badgeMin.X) * 0.5f);

        foreach (var letter in letters)
        {
            var size = CalculateScaledTextSize(letter, badgeScale);
            var pos = new Vector2(
                centerX - (size.X * 0.5f),
                startY
            );

            DrawTextScaled(letter, pos - windowPos, badgeScale, profile.IconTextColor);
            startY += size.Y;
        }
    }

    private void DrawClockHorizontal(
        ClockParts parts,
        Vector2 basePos,
        float scale,
        Vector4 color,
        Vector4 shadow,
        StyleMetrics styleMetrics)
    {
        var leftSize = CalculateScaledTextSize(parts.Left, scale);
        var colonSize = CalculateScaledTextSize(ColonText, scale);
        var minuteLeftSize = CalculateScaledTextSize(parts.MinuteLeft, scale);
        var minuteRightSize = CalculateScaledTextSize(parts.MinuteRight, scale);
        var suffixText = string.IsNullOrWhiteSpace(parts.Suffix) ? "" : " " + parts.Suffix;

        DrawOutlinedTextScaled(parts.Left, basePos, scale, color, shadow, styleMetrics);

        var colonPos = new Vector2(basePos.X + leftSize.X, basePos.Y);
        var colonVisible = ShouldShowColon(plugin.Configuration.ColonAnimation);

        var colonColor = colonVisible ? color : new Vector4(color.X, color.Y, color.Z, 0f);
        var colonShadow = colonVisible ? shadow : new Vector4(0, 0, 0, 0);

        DrawOutlinedTextScaled(ColonText, colonPos, scale, colonColor, colonShadow, styleMetrics);

        float x = basePos.X + leftSize.X + colonSize.X;

        DrawOutlinedTextScaled(parts.MinuteLeft, new Vector2(x, basePos.Y), scale, color, shadow, styleMetrics);

        float secondMinuteX = x + minuteLeftSize.X + (MinuteDigitGap * scale);
        DrawOutlinedTextScaled(parts.MinuteRight, new Vector2(secondMinuteX, basePos.Y), scale, color, shadow, styleMetrics);

        if (!string.IsNullOrWhiteSpace(suffixText))
        {
            float suffixX = secondMinuteX + minuteRightSize.X + (SuffixHorizontalOffset * scale);
            DrawOutlinedTextScaled(suffixText, new Vector2(suffixX, basePos.Y), scale, color, shadow, styleMetrics);
        }
    }

    private bool ShouldShowColon(ColonAnimationMode mode)
    {
        long ms = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        return mode switch
        {
            ColonAnimationMode.AlwaysVisible => true,
            ColonAnimationMode.Hidden => false,
            ColonAnimationMode.SlowBlink => (ms % 2200) < 1200,
            ColonAnimationMode.FastBlink => (ms % 900) < 450,
            _ => (ms % 1800) < 1000
        };
    }

    private void DrawTextScaled(string text, Vector2 basePos, float scale, Vector4 textColor)
    {
        ImGui.SetWindowFontScale(scale);
        ImGui.SetCursorPos(basePos);
        ImGui.TextColored(textColor, text);
        ImGui.SetWindowFontScale(1.0f);
    }

    private void DrawOutlinedTextScaled(string text, Vector2 basePos, float scale, Vector4 textColor, Vector4 outlineColor, StyleMetrics styleMetrics)
    {
        ImGui.SetWindowFontScale(scale);

        if (outlineColor.W > 0.0f)
        {
            float outline = styleMetrics.OutlineOffset;

            var offsets = new[]
            {
                new Vector2(-outline, 0f),
                new Vector2( outline, 0f),
                new Vector2( 0f,-outline),
                new Vector2( 0f, outline),
                new Vector2(-outline,-outline),
                new Vector2( outline,-outline),
                new Vector2(-outline, outline),
                new Vector2( outline, outline),
            };

            foreach (var offset in offsets)
            {
                ImGui.SetCursorPos(basePos + offset);
                ImGui.TextColored(outlineColor, text);
            }
        }

        ImGui.SetCursorPos(basePos);
        ImGui.TextColored(textColor, text);

        ImGui.SetWindowFontScale(1.0f);
    }

    private static Vector2 CalculateScaledTextSize(string text, float scale)
    {
        return ImGui.CalcTextSize(text) * scale;
    }

    private ClockParts GetClockParts()
    {
        var zone = plugin.Configuration.SelectedTimeZone;
        var dateInZone = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, zone);

        var minutes = dateInZone.ToString("mm");
        var suffix = dateInZone.ToString("tt").ToLower().Replace("am", "a.m.").Replace("pm", "p.m.");

        return new ClockParts
        {
            Left = plugin.Configuration.TimeFormat == ClockTimeFormat.TwentyFourHour
                ? dateInZone.ToString("HH")
                : dateInZone.ToString("%h"),
            MinuteLeft = minutes[0].ToString(),
            MinuteRight = minutes[1].ToString(),
            Suffix = suffix
        };
    }

    private ClockLayoutMetrics GetClockLayoutMetrics(float scale, ClockParts parts)
    {
        if (plugin.Configuration.GetActiveProfile().LayoutMode == ClockLayoutMode.Vertical)
        {
            var leftLines = GetVerticalLeftLines(parts.Left);
            float maxWidth = 0f;
            float verticalTotalHeight = 0f;
            float lineHeight = CalculateScaledTextSize("8", scale).Y;

            foreach (var line in leftLines)
            {
                var size = CalculateScaledTextSize(line, scale);
                maxWidth = MathF.Max(maxWidth, size.X);
                verticalTotalHeight += lineHeight;
            }

            maxWidth = MathF.Max(maxWidth, CalculateScaledTextSize(ColonText, scale).X);
            maxWidth = MathF.Max(maxWidth, CalculateScaledTextSize(parts.MinuteLeft, scale).X);
            maxWidth = MathF.Max(maxWidth, CalculateScaledTextSize(parts.MinuteRight, scale).X);

            verticalTotalHeight += lineHeight;
            verticalTotalHeight += lineHeight;
            verticalTotalHeight += lineHeight;

            return new ClockLayoutMetrics
            {
                TotalSize = new Vector2(maxWidth, verticalTotalHeight)
            };
        }

        var leftSize = CalculateScaledTextSize(parts.Left, scale);
        var colonSize = CalculateScaledTextSize(ColonText, scale);
        var minuteLeftSize = CalculateScaledTextSize(parts.MinuteLeft, scale);
        var minuteRightSize = CalculateScaledTextSize(parts.MinuteRight, scale);
        var suffixText = string.IsNullOrWhiteSpace(parts.Suffix) ? "" : " " + parts.Suffix;
        var suffixSize = CalculateScaledTextSize(suffixText, scale);

        float totalWidth =
            leftSize.X +
            colonSize.X +
            minuteLeftSize.X +
            (MinuteDigitGap * scale) +
            minuteRightSize.X +
            (string.IsNullOrWhiteSpace(suffixText) ? 0f : (SuffixHorizontalOffset * scale) + suffixSize.X);

        float horizontalTotalHeight = MathF.Max(
            leftSize.Y,
            MathF.Max(colonSize.Y, MathF.Max(minuteLeftSize.Y, MathF.Max(minuteRightSize.Y, suffixSize.Y)))
        );

        return new ClockLayoutMetrics
        {
            TotalSize = new Vector2(totalWidth, horizontalTotalHeight)
        };
    }

    private static StyleMetrics GetStyleMetrics(ClockDisplayStyle style)
    {
        return style switch
        {
            ClockDisplayStyle.Minimal => new StyleMetrics
            {
                MainPaddingX = 6f,
                MainPaddingY = 3f,
                BadgePaddingX = 4f,
                BadgePaddingY = 1f,
                BadgeGap = 5f,
                MainRounding = 4f,
                BadgeRounding = 3f,
                BadgeVerticalOffset = 0f,
                BorderThickness = 1f,
                OutlineOffset = 0.6f,
                BadgeScaleMultiplier = 0.42f
            },

            ClockDisplayStyle.StrongShadow => new StyleMetrics
            {
                MainPaddingX = 8f,
                MainPaddingY = 4f,
                BadgePaddingX = 5f,
                BadgePaddingY = 2f,
                BadgeGap = 8f,
                MainRounding = 9f,
                BadgeRounding = 5f,
                BadgeVerticalOffset = 1f,
                BorderThickness = 1.3f,
                OutlineOffset = 1.4f,
                BadgeScaleMultiplier = 0.48f
            },

            ClockDisplayStyle.SoftGlass => new StyleMetrics
            {
                MainPaddingX = 8f,
                MainPaddingY = 4f,
                BadgePaddingX = 5f,
                BadgePaddingY = 2f,
                BadgeGap = 7f,
                MainRounding = 11f,
                BadgeRounding = 7f,
                BadgeVerticalOffset = 0f,
                BorderThickness = 1f,
                OutlineOffset = 0.85f,
                BadgeScaleMultiplier = 0.46f
            },

            ClockDisplayStyle.RetroPanel => new StyleMetrics
            {
                MainPaddingX = 9f,
                MainPaddingY = 5f,
                BadgePaddingX = 5f,
                BadgePaddingY = 2f,
                BadgeGap = 9f,
                MainRounding = 2f,
                BadgeRounding = 2f,
                BadgeVerticalOffset = 1f,
                BorderThickness = 1.5f,
                OutlineOffset = 1.2f,
                BadgeScaleMultiplier = 0.44f
            },

            _ => new StyleMetrics
            {
                MainPaddingX = 7f,
                MainPaddingY = 3f,
                BadgePaddingX = 4f,
                BadgePaddingY = 1f,
                BadgeGap = 7f,
                MainRounding = 8f,
                BadgeRounding = 4f,
                BadgeVerticalOffset = 1f,
                BorderThickness = 1f,
                OutlineOffset = 1f,
                BadgeScaleMultiplier = 0.45f
            }
        };
    }

    private sealed class ClockParts
    {
        public string Left = "";
        public string MinuteLeft = "";
        public string MinuteRight = "";
        public string Suffix = "";
    }

    private sealed class ClockLayoutMetrics
    {
        public Vector2 TotalSize;
    }

    private sealed class StyleMetrics
    {
        public float MainPaddingX;
        public float MainPaddingY;
        public float BadgePaddingX;
        public float BadgePaddingY;
        public float BadgeGap;
        public float MainRounding;
        public float BadgeRounding;
        public float BadgeVerticalOffset;
        public float BorderThickness;
        public float OutlineOffset;
        public float BadgeScaleMultiplier;
    }
}