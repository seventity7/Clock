using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;

namespace ESTClock;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public bool IsConfigWindowMovable = true;
    public bool ClockTransparent = true;
    public float ClockTextScale = 2.0f;
    public bool AutoStart = false;

    public bool ShowBorder = true;
    public bool ShowIcon = true;
    public bool ShowShadowText = true;
    public bool ShowIconBorder = true;

    public Vector4 ClockTextColor = new Vector4(1, 1, 1, 1);
    public Vector4 ClockShadowColor = new Vector4(0, 0, 0, 0.8f);

    public Vector4 IconTextColor = new Vector4(0, 0, 0, 1);
    public Vector4 IconBackgroundColor = new Vector4(0.90f, 0.86f, 0.80f, 0.96f);
    public Vector4 IconBorderColor = new Vector4(0.98f, 0.96f, 0.92f, 1.0f);
    public float IconBorderOpacity = 1.0f;

    public float ClockBackgroundOpacity = 0.82f;
    public Vector4 ClockBackgroundColor = new Vector4(0.29f, 0.17f, 0.12f, 1.0f);

    public Vector4 BorderColor = new Vector4(0.47f, 0.31f, 0.22f, 0.95f);
    public float BorderOpacity = 0.95f;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
    }
}