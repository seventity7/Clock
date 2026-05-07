using System.Numerics;

namespace Clock;

public static class ClockProfileFactory
{
    public static ClockProfile CreatePresetProfile(string name, ClockPreset preset)
    {
        return preset switch
        {
            ClockPreset.Minimal => new ClockProfile
            {
                Name = name,
                ShowBorder = false,
                ShowIcon = false,
                ShowShadowText = false,
                ShowIconBorder = false,
                ClockTextScale = 2.0f,
                ClockTextColor = new Vector4(1f, 1f, 1f, 1f),
                ClockShadowColor = new Vector4(0f, 0f, 0f, 0f),
                IconTextColor = new Vector4(1f, 1f, 1f, 1f),
                IconBackgroundColor = new Vector4(0f, 0f, 0f, 0f),
                IconBorderColor = new Vector4(0f, 0f, 0f, 0f),
                IconBorderOpacity = 0f,
                ClockBackgroundOpacity = 0.15f,
                ClockBackgroundColor = new Vector4(0.05f, 0.05f, 0.05f, 1f),
                BorderColor = new Vector4(0f, 0f, 0f, 0f),
                BorderOpacity = 0f,
                DisplayStyle = ClockDisplayStyle.Minimal,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            ClockPreset.GoldHud => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.15f,
                ClockTextColor = new Vector4(1.00f, 0.88f, 0.52f, 1f),
                ClockShadowColor = new Vector4(0.12f, 0.06f, 0.01f, 0.95f),
                IconTextColor = new Vector4(0.17f, 0.09f, 0.02f, 1f),
                IconBackgroundColor = new Vector4(0.96f, 0.83f, 0.46f, 0.97f),
                IconBorderColor = new Vector4(1.00f, 0.93f, 0.70f, 1f),
                IconBorderOpacity = 1f,
                ClockBackgroundOpacity = 0.87f,
                ClockBackgroundColor = new Vector4(0.19f, 0.11f, 0.04f, 1f),
                BorderColor = new Vector4(0.92f, 0.72f, 0.33f, 1f),
                BorderOpacity = 1f,
                DisplayStyle = ClockDisplayStyle.StrongShadow,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            ClockPreset.RetroPanel => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.0f,
                ClockTextColor = new Vector4(0.78f, 1.00f, 0.76f, 1f),
                ClockShadowColor = new Vector4(0.04f, 0.11f, 0.04f, 1f),
                IconTextColor = new Vector4(0.05f, 0.15f, 0.05f, 1f),
                IconBackgroundColor = new Vector4(0.54f, 0.84f, 0.56f, 0.95f),
                IconBorderColor = new Vector4(0.72f, 1.00f, 0.72f, 1f),
                IconBorderOpacity = 1f,
                ClockBackgroundOpacity = 0.92f,
                ClockBackgroundColor = new Vector4(0.07f, 0.17f, 0.08f, 1f),
                BorderColor = new Vector4(0.58f, 0.94f, 0.58f, 1f),
                BorderOpacity = 1f,
                DisplayStyle = ClockDisplayStyle.RetroPanel,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            _ => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.0f,
                ClockTextColor = new Vector4(1f, 1f, 1f, 1f),
                ClockShadowColor = new Vector4(0f, 0f, 0f, 0.8f),
                IconTextColor = new Vector4(0f, 0f, 0f, 1f),
                IconBackgroundColor = new Vector4(0.90f, 0.86f, 0.80f, 0.96f),
                IconBorderColor = new Vector4(0.98f, 0.96f, 0.92f, 1.0f),
                IconBorderOpacity = 1.0f,
                ClockBackgroundOpacity = 0.82f,
                ClockBackgroundColor = new Vector4(0.29f, 0.17f, 0.12f, 1.0f),
                BorderColor = new Vector4(0.47f, 0.31f, 0.22f, 0.95f),
                BorderOpacity = 0.95f,
                DisplayStyle = ClockDisplayStyle.Classic,
                LayoutMode = ClockLayoutMode.Horizontal
            },
        };
    }
}
