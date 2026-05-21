using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Clock.Services;

// code rewrited to avoid compatibility issues from the old code idea. Nothing hard-coded just native system.
// May 21 01:02 BRT - Reinforced/Edited part of the code
public sealed unsafe class ChatTimestampService : IDisposable
{
    private delegate byte* ApplyTextFormatDelegate(RaptureTextModule* raptureTextModule, uint addonTextId, int value);

    private readonly Configuration config;
    private readonly IPluginLog pluginLog;

    // Native timestamp formatter used by the game. The signature is a little odd,
    // but keeping it here makes it easier to compare with the client code later.
    [Signature("E9 ?? ?? ?? ?? 7D 20", DetourName = nameof(OnFormatText))]
    private Hook<ApplyTextFormatDelegate>? formatTextHook;

    private Utf8String* cachedTimestampText;

    public ChatTimestampService(Configuration configuration, IGameInteropProvider interopProvider, IPluginLog log)
    {
        config = configuration;
        pluginLog = log;

        interopProvider.InitializeFromAttributes(this);

        // Reuse the same native string instead of allocating one every time a chat line is formatted.
        cachedTimestampText = Utf8String.FromString(string.Empty);

        formatTextHook?.Enable();
    }

    public void Dispose()
    {
        formatTextHook?.Dispose();
        formatTextHook = null;
    }

    private byte* OnFormatText(RaptureTextModule* textModule, uint addonTextId, int unixTimestamp)
    {
        var isChatTimestamp = addonTextId is 7840 or 7841;

        if (!isChatTimestamp || cachedTimestampText == null || !config.ShowCustomTimestampInChat)
            return formatTextHook!.Original(textModule, addonTextId, unixTimestamp);

        try
        {
            // Make sure any values coming from the config UI are sane before touching native strings.
            config.SanitizeChatTimestampOptions();
            // Had to remove old option `Match Channel Colors` and leave just `Custom Color.

            var messageTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
            var timestampText = MakeTimestampText(messageTime);

            var payloadBuilder = new Lumina.Text.SeStringBuilder();

            // Slightly verbose on purpose; this makes the color wrapping easier to read at a glance.
            var shouldTintTimestamp = config.ChatTimestampUseCustomColor;
            if (shouldTintTimestamp)
            {
                var safeColor = ClampTimestampColor(config.ChatTimestampColor);
                payloadBuilder.PushColorRgba(safeColor);
            }

            payloadBuilder.Append(timestampText);

            if (shouldTintTimestamp)
                payloadBuilder.PopColor();

            cachedTimestampText->SetString(payloadBuilder.GetViewAsSpan());
            return cachedTimestampText->StringPtr;
        }
        catch (Exception ex)
        {
            // If anything goes wrong, fall back to the game's formatter rather than breaking chat.
            pluginLog.Warning(ex, "Could not apply the custom chat timestamp format.");
            return formatTextHook!.Original(textModule, addonTextId, unixTimestamp);
        }
    }

    private string MakeTimestampText(DateTimeOffset originalTime)
    {
        var adjustedTime = ConvertToConfiguredTimeZone(originalTime);
        var timestampFormat = GetChatTimestampFormat();

        return adjustedTime.ToString(timestampFormat, CultureInfo.InvariantCulture);
    }

    private DateTime ConvertToConfiguredTimeZone(DateTimeOffset originalTime)
    {
        if (string.IsNullOrWhiteSpace(config.ChatTimestampTimeZoneId))
            return originalTime.LocalDateTime;

        var selectedTimeZone = TimeZoneHelper.GetTimeZone(config.ChatTimestampTimeZoneId);
        var utcTime = originalTime.UtcDateTime;

        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, selectedTimeZone);
    }

    private string GetChatTimestampFormat()
    {
        var usingTwelveHourClock = TimeFormatHelper.UsesTwelveHourClock(config.TimeFormat);

        // C# format strings are picky, so keeping the pieces separate helps avoid silly mistakes again
        var hourPart = usingTwelveHourClock ? "hh" : "HH";
        var amPmPart = config.ChatTimestampShowAmPm ? " tt" : string.Empty;

        return $"[{hourPart}:mm{amPmPart}]";
    }

    private static Vector4 ClampTimestampColor(Vector4 rawColor)
    {
        var red = Math.Clamp(rawColor.X, 0f, 1f);
        var green = Math.Clamp(rawColor.Y, 0f, 1f);
        var blue = Math.Clamp(rawColor.Z, 0f, 1f);

        // Note for me: Alpha zero usually means "not configured" in this settings, so treat it as visible.
        var alpha = rawColor.W <= 0f ? 1f : rawColor.W;
        alpha = Math.Clamp(alpha, 0f, 1f);

        return new Vector4(red, green, blue, alpha);
    }
}
