using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ESTClock.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    private bool isEditingTextSize = false;
    private bool focusTextSizeInput = false;
    private float textSizeInputValue;

    public ConfigWindow(Plugin plugin)
        : base("###ConfigWindow")
    {
        Flags =
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoTitleBar;

        Size = new Vector2(460, 700);
        SizeCondition = ImGuiCond.FirstUseEver;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 620),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        configuration = plugin.Configuration;
        textSizeInputValue = configuration.ClockTextScale;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, Vector4.Zero);

        // borda branca fina da janela inteira
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 1f, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.0f);
    }

    public override void Draw()
    {
        var windowSize = ImGui.GetWindowSize();

        var savedCursor = ImGui.GetCursorPos();

        ImGui.SetCursorPos(new Vector2(windowSize.X - 54, 4));

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.2f, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1.0f, 0.2f, 0.2f, 1.0f));

        if (ImGui.Button("X", new Vector2(44, 21)))
        {
            IsOpen = false;
            ImGui.PopStyleColor(3);
            return;
        }

        ImGui.PopStyleColor(3);

        ImGui.SetCursorPos(savedCursor);

        ImGui.PushTextWrapPos();

        var stick = !configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Stick clock", ref stick))
        {
            configuration.IsConfigWindowMovable = !stick;
            configuration.Save();
        }

        bool autoStart = configuration.AutoStart;
        if (ImGui.Checkbox("Auto Start", ref autoStart))
        {
            configuration.AutoStart = autoStart;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawCategory("Visibility", DrawVisibilityCategory);

        DrawCategory("Text Options", () =>
        {
            DrawTextSizeControl();

            Vector4 textColor = configuration.ClockTextColor;
            if (ImGui.ColorEdit4("Text Color", ref textColor))
            {
                configuration.ClockTextColor = textColor;
                configuration.Save();
            }

            Vector4 shadowColor = configuration.ClockShadowColor;
            if (ImGui.ColorEdit4("Shadow Color", ref shadowColor))
            {
                configuration.ClockShadowColor = shadowColor;
                configuration.Save();
            }
        });

        DrawCategory("Background Options", () =>
        {
            float opacity = configuration.ClockBackgroundOpacity;
            if (ImGui.SliderFloat("Background Opacity", ref opacity, 0.0f, 1.0f, "%.2f"))
            {
                configuration.ClockBackgroundOpacity = opacity;
                configuration.Save();
            }

            Vector4 bgColor = configuration.ClockBackgroundColor;
            if (ImGui.ColorEdit4("Background Color", ref bgColor))
            {
                configuration.ClockBackgroundColor = bgColor;
                configuration.Save();
            }

            Vector4 borderColor = configuration.BorderColor;
            if (ImGui.ColorEdit4("Border Color", ref borderColor))
            {
                configuration.BorderColor = borderColor;
                configuration.Save();
            }

            float borderOpacity = configuration.BorderOpacity;
            if (ImGui.SliderFloat("Border Opacity", ref borderOpacity, 0.0f, 1.0f, "%.2f"))
            {
                configuration.BorderOpacity = borderOpacity;
                configuration.Save();
            }
        });

        DrawCategory("EST Icon Options", () =>
        {
            Vector4 iconTextColor = configuration.IconTextColor;
            if (ImGui.ColorEdit4("Icon Text Color", ref iconTextColor))
            {
                configuration.IconTextColor = iconTextColor;
                configuration.Save();
            }

            Vector4 iconBgColor = configuration.IconBackgroundColor;
            if (ImGui.ColorEdit4("Icon Background", ref iconBgColor))
            {
                configuration.IconBackgroundColor = iconBgColor;
                configuration.Save();
            }

            Vector4 iconBorderColor = configuration.IconBorderColor;
            if (ImGui.ColorEdit4("Icon Border", ref iconBorderColor))
            {
                configuration.IconBorderColor = iconBorderColor;
                configuration.Save();
            }

            float iconBorderOpacity = configuration.IconBorderOpacity;
            if (ImGui.SliderFloat("Border Opacity##IconBorderOpacity", ref iconBorderOpacity, 0.0f, 1.0f, "%.2f"))
            {
                configuration.IconBorderOpacity = iconBorderOpacity;
                configuration.Save();
            }
        });

        ImGui.PopTextWrapPos();
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(1);
        ImGui.PopStyleColor(5);
    }

    private void DrawCategory(string title, Action drawContent)
    {
        ImGui.Spacing();

        if (ImGui.CollapsingHeader(title, ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent(10.0f);
            drawContent();
            ImGui.Unindent(10.0f);
        }

        ImGui.Spacing();
    }

    private void DrawVisibilityCategory()
    {
        ImGui.Columns(2, "VisibilityColumns", false);

        bool showBorder = configuration.ShowBorder;
        if (ImGui.Checkbox("Border", ref showBorder))
        {
            configuration.ShowBorder = showBorder;
            configuration.Save();
        }

        bool showIcon = configuration.ShowIcon;
        if (ImGui.Checkbox("Icon", ref showIcon))
        {
            configuration.ShowIcon = showIcon;
            configuration.Save();
        }

        ImGui.NextColumn();

        bool showShadowText = configuration.ShowShadowText;
        if (ImGui.Checkbox("Shadow Text", ref showShadowText))
        {
            configuration.ShowShadowText = showShadowText;
            configuration.Save();
        }

        bool showIconBorder = configuration.ShowIconBorder;
        if (ImGui.Checkbox("Icon Border", ref showIconBorder))
        {
            configuration.ShowIconBorder = showIconBorder;
            configuration.Save();
        }

        ImGui.Columns(1);
    }

    private void DrawTextSizeControl()
    {
        if (!isEditingTextSize)
        {
            float scale = configuration.ClockTextScale;
            if (ImGui.SliderFloat("Text Size", ref scale, 0.5f, 5.0f, "%.2f"))
            {
                configuration.ClockTextScale = scale;
                textSizeInputValue = scale;
                configuration.Save();
            }

            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                isEditingTextSize = true;
                focusTextSizeInput = true;
                textSizeInputValue = configuration.ClockTextScale;
            }

            return;
        }

        if (focusTextSizeInput)
        {
            ImGui.SetKeyboardFocusHere();
            focusTextSizeInput = false;
        }

        ImGui.SetNextItemWidth(-1);

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
        textSizeInputValue = Math.Clamp(textSizeInputValue, 0.5f, 5.0f);
        configuration.ClockTextScale = textSizeInputValue;
        configuration.Save();
    }
}