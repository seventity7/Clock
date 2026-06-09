using System.Numerics;

namespace Clock;

public static class ClockProfileFactory
{
    public static ClockProfile CreatePresetProfile(string name, ClockPreset preset)
    {
        // Keeps every preset fully populated so new profiles and preset previews do not inherit
        // stale colors from whatever profile happened to be active before.
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
                IconBorderOpacity = 0f,
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


            ClockPreset.CrystalBlue => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.05f,
                ClockTextColor = new Vector4(0.72f, 0.94f, 1.00f, 1f),
                ClockShadowColor = new Vector4(0.02f, 0.10f, 0.18f, 0.95f),
                IconTextColor = new Vector4(0.04f, 0.12f, 0.20f, 1f),
                IconBackgroundColor = new Vector4(0.55f, 0.86f, 1.00f, 0.96f),
                IconBorderColor = new Vector4(0.82f, 0.96f, 1.00f, 1f),
                IconBorderOpacity = 1f,
                ClockBackgroundOpacity = 0.84f,
                ClockBackgroundColor = new Vector4(0.03f, 0.09f, 0.16f, 1f),
                BorderColor = new Vector4(0.38f, 0.78f, 1.00f, 1f),
                BorderOpacity = 0.95f,
                DisplayStyle = ClockDisplayStyle.SoftGlass,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            ClockPreset.DalamudDark => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.0f,
                ClockTextColor = new Vector4(0.91f, 0.88f, 1.00f, 1f),
                ClockShadowColor = new Vector4(0.03f, 0.02f, 0.05f, 0.95f),
                IconTextColor = new Vector4(0.90f, 0.86f, 1.00f, 1f),
                IconBackgroundColor = new Vector4(0.12f, 0.10f, 0.18f, 0.96f),
                IconBorderColor = new Vector4(0.46f, 0.38f, 0.74f, 1f),
                IconBorderOpacity = 1f,
                ClockBackgroundOpacity = 0.88f,
                ClockBackgroundColor = new Vector4(0.06f, 0.05f, 0.08f, 1f),
                BorderColor = new Vector4(0.36f, 0.30f, 0.58f, 1f),
                BorderOpacity = 0.96f,
                DisplayStyle = ClockDisplayStyle.Classic,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            ClockPreset.CleanWhite => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = false,
                ShowIconBorder = true,
                ClockTextScale = 2.0f,
                ClockTextColor = new Vector4(0.10f, 0.10f, 0.11f, 1f),
                ClockShadowColor = new Vector4(0f, 0f, 0f, 0f),
                IconTextColor = new Vector4(0.10f, 0.10f, 0.11f, 1f),
                IconBackgroundColor = new Vector4(1.00f, 1.00f, 1.00f, 0.96f),
                IconBorderColor = new Vector4(0.78f, 0.78f, 0.80f, 1f),
                IconBorderOpacity = 1f,
                ClockBackgroundOpacity = 0.86f,
                ClockBackgroundColor = new Vector4(0.95f, 0.95f, 0.96f, 1f),
                BorderColor = new Vector4(0.72f, 0.72f, 0.74f, 1f),
                BorderOpacity = 0.94f,
                DisplayStyle = ClockDisplayStyle.Minimal,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            ClockPreset.NeonPurple => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.1f,
                ClockTextColor = new Vector4(0.94f, 0.64f, 1.00f, 1f),
                ClockShadowColor = new Vector4(0.12f, 0.02f, 0.18f, 0.98f),
                IconTextColor = new Vector4(0.08f, 0.02f, 0.12f, 1f),
                IconBackgroundColor = new Vector4(0.91f, 0.43f, 1.00f, 0.96f),
                IconBorderColor = new Vector4(1.00f, 0.76f, 1.00f, 1f),
                IconBorderOpacity = 1f,
                ClockBackgroundOpacity = 0.86f,
                ClockBackgroundColor = new Vector4(0.08f, 0.02f, 0.12f, 1f),
                BorderColor = new Vector4(0.78f, 0.30f, 1.00f, 1f),
                BorderOpacity = 1f,
                DisplayStyle = ClockDisplayStyle.StrongShadow,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            ClockPreset.CasinoGold => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.18f,
                ClockTextColor = new Vector4(1.00f, 0.82f, 0.24f, 1f),
                ClockShadowColor = new Vector4(0.02f, 0.02f, 0.01f, 1f),
                IconTextColor = new Vector4(0.03f, 0.03f, 0.02f, 1f),
                IconBackgroundColor = new Vector4(0.90f, 0.68f, 0.22f, 0.98f),
                IconBorderColor = new Vector4(1.00f, 0.90f, 0.52f, 1f),
                IconBorderOpacity = 1f,
                ClockBackgroundOpacity = 0.91f,
                ClockBackgroundColor = new Vector4(0.03f, 0.03f, 0.02f, 1f),
                BorderColor = new Vector4(0.74f, 0.56f, 0.20f, 1f),
                BorderOpacity = 1f,
                DisplayStyle = ClockDisplayStyle.RetroPanel,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            ClockPreset.CompactTransparent => new ClockProfile
            {
                Name = name,
                ShowBorder = false,
                ShowIcon = false,
                ShowShadowText = true,
                ShowIconBorder = false,
                ClockTextScale = 1.55f,
                ClockTextColor = new Vector4(1f, 1f, 1f, 1f),
                ClockShadowColor = new Vector4(0f, 0f, 0f, 0.92f),
                IconTextColor = new Vector4(1f, 1f, 1f, 1f),
                IconBackgroundColor = new Vector4(0f, 0f, 0f, 0f),
                IconBorderColor = new Vector4(0f, 0f, 0f, 0f),
                IconBorderOpacity = 0f,
                ClockBackgroundOpacity = 0.0f,
                ClockBackgroundColor = new Vector4(0f, 0f, 0f, 0f),
                BorderColor = new Vector4(0f, 0f, 0f, 0f),
                BorderOpacity = 0f,
                DisplayStyle = ClockDisplayStyle.Minimal,
                LayoutMode = ClockLayoutMode.Horizontal
            },

            ClockPreset.RaidMinimal => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = false,
                ShowShadowText = true,
                ShowIconBorder = false,
                ClockTextScale = 1.7f,
                ClockTextColor = new Vector4(0.88f, 0.96f, 1.00f, 1f),
                ClockShadowColor = new Vector4(0f, 0f, 0f, 0.95f),
                IconTextColor = new Vector4(1f, 1f, 1f, 1f),
                IconBackgroundColor = new Vector4(0f, 0f, 0f, 0f),
                IconBorderColor = new Vector4(0f, 0f, 0f, 0f),
                IconBorderOpacity = 0f,
                ClockBackgroundOpacity = 0.45f,
                ClockBackgroundColor = new Vector4(0.02f, 0.03f, 0.04f, 1f),
                BorderColor = new Vector4(0.35f, 0.48f, 0.58f, 1f),
                BorderOpacity = 0.85f,
                DisplayStyle = ClockDisplayStyle.Minimal,
                LayoutMode = ClockLayoutMode.Horizontal
            },


            ClockPreset.Cartoon => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 1.95f,
                ClockTextColor = new Vector4(0.08f, 0.20f, 0.42f, 1f),
                ClockShadowColor = new Vector4(1f, 1f, 1f, 0.70f),
                IconTextColor = new Vector4(0.08f, 0.20f, 0.42f, 1f),
                IconBackgroundColor = new Vector4(1.00f, 0.78f, 0.20f, 1f),
                IconBorderColor = new Vector4(1f, 1f, 1f, 1f),
                IconBorderOpacity = 1f,
                ClockBackgroundOpacity = 1.0f,
                ClockBackgroundColor = new Vector4(0.56f, 0.86f, 0.94f, 1f),
                BorderColor = new Vector4(1.00f, 0.28f, 0.36f, 1f),
                BorderOpacity = 1f,
                DisplayStyle = ClockDisplayStyle.Cartoon,
                LayoutMode = ClockLayoutMode.Horizontal,
                TimeTextFont = ClockTimeTextFont.Ka1,
                NextAlarmOverlayShadowColor = new Vector4(1f, 1f, 1f, 0.70f),
                MaintenanceOverlayShadowColor = new Vector4(1f, 1f, 1f, 0.70f),
                LocalTimeShowBorder = false,
                LocalTimeShadowColor = new Vector4(1f, 1f, 1f, 0.55f),
                LocalTimeIconTextColor = new Vector4(0.08f, 0.20f, 0.42f, 1f),
                LocalTimeIconBackgroundColor = new Vector4(1.00f, 0.78f, 0.20f, 1f),
                LocalTimeIconBorderColor = new Vector4(1f, 1f, 1f, 1f),
                LocalTimeIconBorderOpacity = 1f
            },

            ClockPreset.Tech => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.12f,
                ClockTextColor = new Vector4(1.00f, 0.20f, 0.20f, 1f),
                ClockShadowColor = new Vector4(0.0784f, 0.1608f, 0.1608f, 1f),
                IconTextColor = new Vector4(1.00f, 0.20f, 0.20f, 1f),
                IconBackgroundColor = new Vector4(0.102f, 0.180f, 0.180f, 0.0f),
                IconBorderColor = new Vector4(0.180f, 0.290f, 0.302f, 0.0f),
                IconBorderOpacity = 0f,
                ClockBackgroundOpacity = 1.0f,
                ClockBackgroundColor = new Vector4(0.08f, 0.16f, 0.16f, 1f),
                BorderColor = new Vector4(0.15f, 0.27f, 0.28f, 1f),
                BorderOpacity = 1f,
                DisplayStyle = ClockDisplayStyle.Tech,
                LayoutMode = ClockLayoutMode.Horizontal,
                TimeTextFont = ClockTimeTextFont.Technology,
                NextAlarmOverlayShadowColor = new Vector4(0.00f, 0.00f, 0.00f, 1f),
                MaintenanceOverlayShadowColor = new Vector4(0.00f, 0.00f, 0.00f, 1f),
                LocalTimeShowBorder = false,
                LocalTimeShadowColor = new Vector4(0.00f, 0.00f, 0.00f, 0.85f),
                LocalTimeIconBackgroundColor = new Vector4(0.08f, 0.16f, 0.16f, 0.0f)
            },

            ClockPreset.Digital => new ClockProfile
            {
                Name = name,
                ShowBorder = true,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.45f,
                ClockTextColor = new Vector4(1.00f, 0.10f, 0.07f, 1f),
                ClockShadowColor = new Vector4(0.00f, 0.00f, 0.00f, 0.0f),
                IconTextColor = new Vector4(0.95f, 0.08f, 0.06f, 1f),
                IconBackgroundColor = new Vector4(0.0118f, 0.0118f, 0.0118f, 0.0f),
                IconBorderColor = new Vector4(0.72f, 0.72f, 0.70f, 1f),
                IconBorderOpacity = 0f,
                ClockBackgroundOpacity = 1.0f,
                ClockBackgroundColor = new Vector4(0.00f, 0.00f, 0.00f, 1f),
                BorderColor = new Vector4(0.76f, 0.76f, 0.74f, 1f),
                BorderOpacity = 1f,
                DisplayStyle = ClockDisplayStyle.Digital,
                LayoutMode = ClockLayoutMode.Horizontal,
                TimeTextFont = ClockTimeTextFont.Digital,
                NextAlarmOverlayShadowColor = new Vector4(0.00f, 0.00f, 0.00f, 1f),
                MaintenanceOverlayShadowColor = new Vector4(0.00f, 0.00f, 0.00f, 1f),
                LocalTimeShowBorder = false,
                LocalTimeShadowColor = new Vector4(0.00f, 0.00f, 0.00f, 0.949f),
                LocalTimeIconBackgroundColor = new Vector4(0.0118f, 0.0118f, 0.0118f, 0.0f)
            },

            ClockPreset.Countdown => new ClockProfile
            {
                Name = name,
                ShowBorder = false,
                ShowIcon = true,
                ShowShadowText = true,
                ShowIconBorder = true,
                ClockTextScale = 2.15f,
                ClockTextColor = new Vector4(0.96f, 0.96f, 0.94f, 1f),
                ClockShadowColor = new Vector4(0f, 0f, 0f, 0.95f),
                IconTextColor = new Vector4(0.96f, 0.96f, 0.94f, 1f),
                IconBackgroundColor = new Vector4(0.04f, 0.04f, 0.04f, 1f),
                IconBorderColor = new Vector4(0.30f, 0.30f, 0.30f, 1f),
                IconBorderOpacity = 1f,
                ClockBackgroundOpacity = 1.0f,
                ClockBackgroundColor = new Vector4(0.045f, 0.047f, 0.050f, 1f),
                BorderColor = new Vector4(0.537f, 0.537f, 0.537f, 1f),
                BorderOpacity = 1f,
                DisplayStyle = ClockDisplayStyle.Countdown,
                LayoutMode = ClockLayoutMode.Horizontal,
                TimeTextFont = ClockTimeTextFont.Countdown,
                NextAlarmOverlayDisplayStyle = ClockDisplayStyle.Countdown,
                MaintenanceOverlayDisplayStyle = ClockDisplayStyle.Countdown,
                NextAlarmOverlayShadowColor = new Vector4(0f, 0f, 0f, 0.95f),
                MaintenanceOverlayShadowColor = new Vector4(0f, 0f, 0f, 0.95f),
                LocalTimeDisplayStyle = ClockDisplayStyle.Countdown,
                LocalTimeShowBorder = false,
                LocalTimeShadowColor = new Vector4(0f, 0f, 0f, 0.95f),
                LocalTimeIconTextColor = new Vector4(0.96f, 0.96f, 0.94f, 1f),
                LocalTimeIconBackgroundColor = new Vector4(0.04f, 0.04f, 0.04f, 1f),
                LocalTimeIconBorderColor = new Vector4(0.30f, 0.30f, 0.30f, 1f),
                LocalTimeIconBorderOpacity = 1f
            },

            // Default branch is used by the Classic preset; keep it conservative
            // so it remains a safe fallback if an unknown preset value is loaded.
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
