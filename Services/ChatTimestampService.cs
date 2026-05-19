using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

namespace Clock.Services;

public sealed class ChatTimestampService : IDisposable
{
    private static readonly Regex LeadingTimestampRegex = new(
        @"^\s*(?<stamp>(?:\[[0-2]?\d:[0-5]\d(?::[0-5]\d)?(?:\s?[AP]M)?\]\s?)|(?:[0-2]?\d:[0-5]\d(?::[0-5]\d)?(?:\s?[AP]M)?\s?))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Configuration configuration;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;

    public ChatTimestampService(Configuration configuration, IChatGui chatGui, IDataManager dataManager, IPluginLog log)
    {
        this.configuration = configuration;
        this.chatGui = chatGui;
        this.log = log;

        this.chatGui.CheckMessageHandled += OnCheckMessageHandled;
    }

    public void Dispose()
    {
        chatGui.CheckMessageHandled -= OnCheckMessageHandled;
    }

    private void OnCheckMessageHandled(IHandleableChatMessage message)
    {
        if (!configuration.ShowCustomTimestampInChat)
            return;

        try
        {
            configuration.SanitizeChatTimestampOptions();
            RemoveExistingTimestamp(message);
            AddTimestamp(message);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to add custom chat timestamp.");
        }
    }

    private static void RemoveExistingTimestamp(IHandleableChatMessage message)
    {
        RemoveLeadingTimestamp(message.Sender.Payloads);
        RemoveLeadingTimestamp(message.Message.Payloads);
    }

    private static void RemoveLeadingTimestamp(IList<Payload> payloads)
    {
        for (var i = 0; i < payloads.Count; i++)
        {
            if (IsTimestampFormattingPayload(payloads[i]))
                continue;

            if (payloads[i] is not TextPayload textPayload || string.IsNullOrEmpty(textPayload.Text))
                return;

            var match = LeadingTimestampRegex.Match(textPayload.Text);
            if (!match.Success)
                return;

            var afterStart = match.Index + match.Groups["stamp"].Value.Length;
            var after = textPayload.Text[afterStart..].TrimStart();

            while (i > 0 && IsTimestampFormattingPayload(payloads[i - 1]))
            {
                payloads.RemoveAt(i - 1);
                i--;
            }

            while (i + 1 < payloads.Count && IsTimestampFormattingPayload(payloads[i + 1]))
                payloads.RemoveAt(i + 1);

            if (string.IsNullOrEmpty(after))
                payloads.RemoveAt(i);
            else
                payloads[i] = new TextPayload(after);

            return;
        }
    }

    private static bool IsTimestampFormattingPayload(Payload payload)
    {
        return payload is UIForegroundPayload or RawPayload;
    }

    private void AddTimestamp(IHandleableChatMessage message)
    {
        var timestamp = BuildTimestamp();
        var insertIntoSender = !string.IsNullOrWhiteSpace(message.Sender.TextValue);
        var payloads = BuildTimestampPayloads(timestamp);

        if (insertIntoSender)
            message.Sender.Payloads.InsertRange(0, payloads);
        else
            message.Message.Payloads.InsertRange(0, payloads);
    }

    private List<Payload> BuildTimestampPayloads(string text)
    {
        if (configuration.ChatTimestampMatchChannelColor)
            return new List<Payload> { new TextPayload(text) };

        var color = ToBgraMacroColor(configuration.ChatTimestampColor);
        var macroString = $"<color(0x{color:X8})>{text}<color(stackcolor)>";
        return new SeStringBuilder()
            .AppendMacroString(macroString)
            .Build()
            .Payloads
            .ToList();
    }

    private string BuildTimestamp()
    {
        var time = GetTimestampTime();
        var format = configuration.TimeFormat switch
        {
            ClockTimeFormat.TwelveHour or ClockTimeFormat.TwelveHourSeconds => "h:mm",
            _ => "HH:mm"
        };

        if (configuration.ChatTimestampShowAmPm)
            format += " tt";

        return $"[{time.ToString(format, CultureInfo.InvariantCulture)}] ";
    }

    private DateTime GetTimestampTime()
    {
        var utcNow = DateTime.UtcNow;
        return string.IsNullOrWhiteSpace(configuration.ChatTimestampTimeZoneId)
            ? DateTime.Now
            : TimeZoneHelper.ConvertFromUtc(utcNow, configuration.ChatTimestampTimeZoneId);
    }

    private static uint ToBgraMacroColor(Vector4 color)
    {
        byte r = ToByte(color.X);
        byte g = ToByte(color.Y);
        byte b = ToByte(color.Z);
        byte a = ToByte(color.W <= 0f ? 1f : color.W);

        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
    }
}
