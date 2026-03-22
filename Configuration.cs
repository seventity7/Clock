using Dalamud.Configuration;
using System;
using System.Numerics;
using ESTClock;
using ESTClock.Windows;

namespace ESTClock;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable = true;
    public bool ClockTransparent = true;
    public float ClockTextScale = 2.0f;

    public Vector4 ClockTextColor = new Vector4(1, 1, 1, 1);
    public Vector4 ClockShadowColor = new Vector4(0, 0, 0, 0.8f);
    public float ClockBackgroundOpacity = 0.0f;
    public Vector4 ClockBackgroundColor = new Vector4(0, 0, 0, 1);

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}