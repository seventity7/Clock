using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using ESTClock.Windows;

namespace ESTClock;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/est";
    private const string SettingsCommand = "/estsettings";
    private const string AlarmsCommand = "/estalarms";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IChatGui chatGui;
    private readonly IToastGui toastGui;

    private bool hasAutoStarted;
    private bool wantedMainWindowOpen;
    private DateTime lastReminderCheckUtc = DateTime.MinValue;

    private readonly HashSet<string> triggeredMaintenanceKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> recentlyTriggeredAlarmIds = new();

    public Configuration Configuration { get; private set; }

    public readonly WindowSystem WindowSystem = new("EST Clock");

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log,
        IClientState clientState,
        ICondition condition,
        IChatGui chatGui,
        IToastGui toastGui)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;
        this.clientState = clientState;
        this.condition = condition;
        this.chatGui = chatGui;
        this.toastGui = toastGui;

        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(pluginInterface);
        Configuration.EnsureInitialized();
        Configuration.Save();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage =
                "EST Clock commands: /est, /est help, /est timezone est|pst|utc|bst|jst|mst|acst, /est format 12|24, " +
                "/est colon default|always|hidden|slow|fast, /est layout horizontal|vertical, " +
                "/est preset classic|minimal|gold|retro, /est lock, /est unlock, " +
                "/est profile next|list|set <n>|add <name>|rename <name>|delete"
        });

        commandManager.AddHandler(SettingsCommand, new CommandInfo(OnSettingsCommand)
        {
            HelpMessage = "Open EST Clock settings/customizations"
        });

        commandManager.AddHandler(AlarmsCommand, new CommandInfo(OnAlarmsCommand)
        {
            HelpMessage = "Open EST Clock settings directly on the Alarms tab"
        });

        pluginInterface.UiBuilder.DisableCutsceneUiHide = !Configuration.HideDuringCutscenes;
        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        pluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        chatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        Configuration.Save();

        chatGui.ChatMessage -= OnChatMessage;

        pluginInterface.UiBuilder.Draw -= DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        commandManager.RemoveHandler(CommandName);
        commandManager.RemoveHandler(SettingsCommand);
        commandManager.RemoveHandler(AlarmsCommand);
    }

    private void DrawUI()
    {
        pluginInterface.UiBuilder.DisableCutsceneUiHide = !Configuration.HideDuringCutscenes;

        if (!hasAutoStarted && Configuration.AutoStart && clientState.IsLoggedIn)
        {
            hasAutoStarted = true;
            wantedMainWindowOpen = true;
        }

        CheckReminders();

        MainWindow.IsOpen = wantedMainWindowOpen && !ShouldHideClock();
        WindowSystem.Draw();
    }

    private bool ShouldHideClock()
    {
        if (!Configuration.HideDuringCutscenes)
            return false;

        if (pluginInterface.UiBuilder.CutsceneActive)
            return true;

        return condition[ConditionFlag.WatchingCutscene]
            || condition[ConditionFlag.WatchingCutscene78]
            || condition[ConditionFlag.OccupiedInCutSceneEvent];
    }

    private void CheckReminders()
    {
        var nowUtc = DateTime.UtcNow;

        if ((nowUtc - lastReminderCheckUtc).TotalSeconds < 1.0)
            return;

        lastReminderCheckUtc = nowUtc;

        CheckAllAlarms(nowUtc);
        CheckMaintenanceReminder(nowUtc);
    }

    private void CheckAllAlarms(DateTime nowUtc)
    {
        if (Configuration.Alarms == null || Configuration.Alarms.Count == 0)
            return;

        bool changed = false;

        foreach (var alarm in Configuration.Alarms)
        {
            if (!alarm.Enabled || alarm.HasTriggered)
                continue;

            if (!TimeZoneHelper.TryParseInZone(alarm.DateTimeText, alarm.TimeZone, out var alarmUtc))
                continue;

            if (nowUtc < alarmUtc || (nowUtc - alarmUtc).TotalSeconds > 60)
                continue;

            if (recentlyTriggeredAlarmIds.Contains(alarm.Id))
                continue;

            recentlyTriggeredAlarmIds.Add(alarm.Id);
            alarm.HasTriggered = true;
            changed = true;

            var triggerMessage = alarm.BuildTriggerMessage(Configuration.TimeFormat);
            SendAlarmOutput(triggerMessage);
        }

        if (changed)
            Configuration.Save();
    }

    private void CheckMaintenanceReminder(DateTime nowUtc)
    {
        if (!Configuration.MaintenanceReminderEnabled)
            return;

        if (string.IsNullOrWhiteSpace(Configuration.DetectedMaintenanceDateTimeText))
            return;

        if (!TimeZoneHelper.TryParseInZone(
                Configuration.DetectedMaintenanceDateTimeText,
                Configuration.SelectedTimeZone,
                out var maintenanceUtc))
        {
            return;
        }

        CheckMaintenanceLead(nowUtc, maintenanceUtc, TimeSpan.FromHours(24), Configuration.MaintenanceRemind24Hours);
        CheckMaintenanceLead(nowUtc, maintenanceUtc, TimeSpan.FromHours(1), Configuration.MaintenanceRemind1Hour);
        CheckMaintenanceLead(nowUtc, maintenanceUtc, TimeSpan.FromMinutes(15), Configuration.MaintenanceRemind15Minutes);
    }

    private void CheckMaintenanceLead(DateTime nowUtc, DateTime maintenanceUtc, TimeSpan lead, bool enabled)
    {
        if (!enabled)
            return;

        var targetMoment = maintenanceUtc - lead;
        if (nowUtc < targetMoment || (nowUtc - targetMoment).TotalSeconds > 60)
            return;

        var key = $"{maintenanceUtc:O}:{lead.TotalMinutes}";
        if (triggeredMaintenanceKeys.Contains(key))
            return;

        triggeredMaintenanceKeys.Add(key);

        var leadText = lead.TotalHours >= 1
            ? (Math.Abs(lead.TotalHours - 24) < 0.01 ? "24 hours" : $"{lead.TotalHours:0} hour")
            : $"{lead.TotalMinutes:0} minutes";

        var message = $"Scheduled maintenance starts in {leadText}.";
        chatGui.Print(message, "EST Clock");
        toastGui.ShowQuest(message, new QuestToastOptions
        {
            PlaySound = false
        });
    }

    private void OnChatMessage(
        XivChatType type,
        int timestamp,
        ref SeString sender,
        ref SeString message,
        ref bool isHandled)
    {
        try
        {
            var text = message.TextValue;
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (!LooksLikeMaintenanceSystemMessage(type, message, text))
                return;

            Configuration.LastDetectedMaintenanceMessage = text;
            Configuration.LastMaintenanceDetectionTimestampUtc = DateTime.UtcNow;

            if (TryExtractMaintenanceDateTime(text, out var maintenanceDateTimeText))
            {
                Configuration.DetectedMaintenanceDateTimeText = maintenanceDateTimeText;
                Configuration.HasDetectedMaintenanceTime = true;
            }
            else
            {
                Configuration.HasDetectedMaintenanceTime = false;
            }

            Configuration.Save();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed parsing maintenance system message.");
        }
    }

    private bool LooksLikeMaintenanceSystemMessage(XivChatType type, SeString message, string text)
    {
        var lowered = text.ToLowerInvariant();

        if (!lowered.Contains("maintenance"))
            return false;

        bool hasTimeWindow =
            Regex.IsMatch(text, @"from\s+[A-Z][a-z]{2}\.\s+\d{1,2},\s+\d{4}\s+\d{1,2}:\d{2}\s+[ap]\.m\.\s+to\s+[A-Z][a-z]{2}\.\s+\d{1,2},\s+\d{4}\s+\d{1,2}:\d{2}\s+[ap]\.m\.\s+\((?:PDT|PST|UTC|GMT|EST|BST|JST|MST|ACST)\)", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(text, @"from\s+.+?\s+to\s+.+?\((?:PDT|PST|UTC|GMT|EST|BST|JST|MST|ACST)\)", RegexOptions.IgnoreCase);

        if (!hasTimeWindow)
            return false;

        bool hasMaintenanceColorPayload = message.Payloads.OfType<UIForegroundPayload>().Any();

        bool likelySystemChannel =
            type == XivChatType.SystemMessage ||
            type == XivChatType.SystemError ||
            type == XivChatType.Notice;

        return hasMaintenanceColorPayload || likelySystemChannel;
    }

    private bool TryExtractMaintenanceDateTime(string text, out string dateTimeText)
    {
        dateTimeText = string.Empty;

        var zoneNow = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, Configuration.SelectedTimeZone);
        var currentYear = zoneNow.Year;

        var patterns = new[]
        {
            new Regex(@"\b(?<year>\d{4})[-/](?<month>\d{1,2})[-/](?<day>\d{1,2})\s+(?<hour>\d{1,2}):(?<minute>\d{2})\b", RegexOptions.IgnoreCase),
            new Regex(@"\b(?<month>\d{1,2})/(?<day>\d{1,2})/(?<year>\d{4})\s+(?<hour>\d{1,2}):(?<minute>\d{2})\b", RegexOptions.IgnoreCase),
            new Regex(@"\b(?<month>\d{1,2})/(?<day>\d{1,2})\s+(?<hour>\d{1,2}):(?<minute>\d{2})\b", RegexOptions.IgnoreCase),
            new Regex(@"\b(?<monthName>jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\.\s+(?<day>\d{1,2}),\s+(?<year>\d{4})\s+(?<hour>\d{1,2}):(?<minute>\d{2})\s+(?<ampm>a\.m\.|p\.m\.)", RegexOptions.IgnoreCase),
            new Regex(@"\b(?<monthName>jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s+(?<day>\d{1,2})(?:,\s*(?<year>\d{4}))?\s+(?<hour>\d{1,2}):(?<minute>\d{2})\b", RegexOptions.IgnoreCase),
        };

        foreach (var regex in patterns)
        {
            var match = regex.Match(text);
            if (!match.Success)
                continue;

            int year = currentYear;

            if (match.Groups["year"].Success && int.TryParse(match.Groups["year"].Value, out var parsedYear))
                year = parsedYear;

            int month;
            if (match.Groups["month"].Success)
            {
                month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
            }
            else if (match.Groups["monthName"].Success)
            {
                month = MonthNameToNumber(match.Groups["monthName"].Value);
            }
            else
            {
                continue;
            }

            if (!int.TryParse(match.Groups["day"].Value, out var day))
                continue;
            if (!int.TryParse(match.Groups["hour"].Value, out var hour))
                continue;
            if (!int.TryParse(match.Groups["minute"].Value, out var minute))
                continue;

            if (match.Groups["ampm"].Success)
            {
                var ampm = match.Groups["ampm"].Value.ToLowerInvariant();
                hour %= 12;
                if (ampm.Contains("p"))
                    hour += 12;
            }

            if (month < 1 || month > 12)
                continue;

            var maxDay = DateTime.DaysInMonth(year, month);
            if (day < 1 || day > maxDay)
                continue;

            var dt = new DateTime(year, month, day, hour, minute, 0);
            dateTimeText = dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static int MonthNameToNumber(string monthName)
    {
        return monthName[..3].ToLowerInvariant() switch
        {
            "jan" => 1,
            "feb" => 2,
            "mar" => 3,
            "apr" => 4,
            "may" => 5,
            "jun" => 6,
            "jul" => 7,
            "aug" => 8,
            "sep" => 9,
            "oct" => 10,
            "nov" => 11,
            _ => 12,
        };
    }

    private void OnCommand(string command, string args)
    {
        args = args?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(args))
        {
            ToggleMainUi();
            return;
        }

        var split = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sub = split[0].ToLowerInvariant();
        var rest = split.Length > 1 ? split[1] : string.Empty;

        switch (sub)
        {
            case "help":
                PrintHelp();
                return;

            case "toggle":
                ToggleMainUi();
                chatGui.Print($"Clock {(wantedMainWindowOpen ? "opened" : "hidden")}.", "EST Clock");
                return;

            case "settings":
                ToggleConfigUi();
                return;

            case "lock":
                Configuration.IsConfigWindowMovable = false;
                SaveAndNotify("Clock locked.");
                return;

            case "unlock":
                Configuration.IsConfigWindowMovable = true;
                SaveAndNotify("Clock unlocked.");
                return;

            case "timezone":
                HandleTimezoneCommand(rest);
                return;

            case "format":
                HandleFormatCommand(rest);
                return;

            case "colon":
                HandleColonCommand(rest);
                return;

            case "layout":
                HandleLayoutCommand(rest);
                return;

            case "preset":
                HandlePresetCommand(rest);
                return;

            case "profile":
                HandleProfileCommand(rest);
                return;

            default:
                PrintHelp();
                return;
        }
    }

    private void OnSettingsCommand(string command, string args)
    {
        ToggleConfigUi();
    }

    private void OnAlarmsCommand(string command, string args)
    {
        OpenConfigUiAtAlarms();
    }

    private void HandleTimezoneCommand(string rest)
    {
        if (!TryParseTimeZone(rest, out var zone))
        {
            chatGui.PrintError("Invalid timezone. Use est, pst, utc, bst, jst, mst or acst.", "EST Clock");
            return;
        }

        Configuration.SelectedTimeZone = zone;
        Configuration.Save();

        chatGui.Print($"Timezone set to {zone.ToShortText()}.", "EST Clock");
    }

    private void HandleFormatCommand(string rest)
    {
        rest = rest.Trim().ToLowerInvariant();

        switch (rest)
        {
            case "12":
            case "12h":
                Configuration.TimeFormat = ClockTimeFormat.TwelveHour;
                NormalizeAlarmEditorHourForNewFormat();
                SaveAndNotify("Time format set to 12h.");
                return;

            case "24":
            case "24h":
                Configuration.TimeFormat = ClockTimeFormat.TwentyFourHour;
                NormalizeAlarmEditorHourForNewFormat();
                SaveAndNotify("Time format set to 24h.");
                return;

            default:
                chatGui.PrintError("Invalid format. Use 12 or 24.", "EST Clock");
                return;
        }
    }

    private void NormalizeAlarmEditorHourForNewFormat()
    {
        if (Configuration.TimeFormat == ClockTimeFormat.TwelveHour)
        {
            int sourceHour24;

            if (Configuration.AlarmEditorHour >= 0 && Configuration.AlarmEditorHour <= 23)
            {
                sourceHour24 = Configuration.AlarmEditorHour;
            }
            else
            {
                var nowInZone = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, Configuration.SelectedTimeZone);
                sourceHour24 = nowInZone.Hour;
            }

            Configuration.AlarmEditorIsPm = sourceHour24 >= 12;
            var hour12 = sourceHour24 % 12;
            Configuration.AlarmEditorHour = hour12 == 0 ? 12 : hour12;
        }
        else
        {
            var selectedHour12 = Math.Clamp(Configuration.AlarmEditorHour, 1, 12);
            var hour24 = selectedHour12 % 12;
            if (Configuration.AlarmEditorIsPm)
                hour24 += 12;

            Configuration.AlarmEditorHour = Math.Clamp(hour24, 0, 23);
        }

        Configuration.Save();
    }

    private void HandleColonCommand(string rest)
    {
        rest = rest.Trim().ToLowerInvariant();

        Configuration.ColonAnimation = rest switch
        {
            "default" or "blink" => ColonAnimationMode.Blink,
            "always" => ColonAnimationMode.AlwaysVisible,
            "hidden" or "off" => ColonAnimationMode.Hidden,
            "slow" => ColonAnimationMode.SlowBlink,
            "fast" => ColonAnimationMode.FastBlink,
            _ => Configuration.ColonAnimation
        };

        if (rest is not ("default" or "blink" or "always" or "hidden" or "off" or "slow" or "fast"))
        {
            chatGui.PrintError("Invalid colon mode. Use default, always, hidden, slow or fast.", "EST Clock");
            return;
        }

        Configuration.Save();
        chatGui.Print($"Colon animation set to {Configuration.ColonAnimation}.", "EST Clock");
    }

    private void HandleLayoutCommand(string rest)
    {
        rest = rest.Trim().ToLowerInvariant();

        var profile = Configuration.GetActiveProfile();

        switch (rest)
        {
            case "horizontal":
                profile.LayoutMode = ClockLayoutMode.Horizontal;
                break;

            case "vertical":
                profile.LayoutMode = ClockLayoutMode.Vertical;
                break;

            default:
                chatGui.PrintError("Invalid layout. Use horizontal or vertical.", "EST Clock");
                return;
        }

        Configuration.Save();
        chatGui.Print($"Layout set to {profile.LayoutMode}.", "EST Clock");
    }

    private void HandlePresetCommand(string rest)
    {
        rest = rest.Trim().ToLowerInvariant();

        var preset = rest switch
        {
            "classic" => ClockPreset.Classic,
            "minimal" => ClockPreset.Minimal,
            "gold" => ClockPreset.GoldHud,
            "retro" => ClockPreset.RetroPanel,
            _ => ClockPreset.Classic
        };

        if (rest is not ("classic" or "minimal" or "gold" or "retro"))
        {
            chatGui.PrintError("Invalid preset. Use classic, minimal, gold or retro.", "EST Clock");
            return;
        }

        Configuration.PreviewPresetSelection = preset;
        Configuration.Save();
        chatGui.Print($"Preset selected: {preset}.", "EST Clock");
    }

    private void HandleProfileCommand(string rest)
    {
        var split = rest.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sub = split.Length > 0 ? split[0].ToLowerInvariant() : string.Empty;
        var value = split.Length > 1 ? split[1] : string.Empty;

        switch (sub)
        {
            case "next":
                Configuration.ActiveProfileIndex = (Configuration.ActiveProfileIndex + 1) % Configuration.Profiles.Count;
                Configuration.Save();
                chatGui.Print($"Active profile: {Configuration.GetActiveProfile().Name}", "EST Clock");
                return;

            case "list":
                var list = string.Join(", ", Configuration.Profiles.Select((p, i) => $"{i + 1}:{p.Name}"));
                chatGui.Print($"Profiles: {list}", "EST Clock");
                return;

            case "set":
                if (int.TryParse(value, out var idx) && idx >= 1 && idx <= Configuration.Profiles.Count)
                {
                    Configuration.ActiveProfileIndex = idx - 1;
                    Configuration.Save();
                    chatGui.Print($"Active profile: {Configuration.GetActiveProfile().Name}", "EST Clock");
                }
                else
                {
                    chatGui.PrintError("Invalid profile index.", "EST Clock");
                }

                return;

            case "add":
            {
                var name = string.IsNullOrWhiteSpace(value)
                    ? $"Profile {Configuration.Profiles.Count + 1}"
                    : value.Trim();

                Configuration.AddProfile(name);
                Configuration.Save();
                chatGui.Print($"Profile \"{Configuration.GetActiveProfile().Name}\" created.", "EST Clock");
                return;
            }

            case "rename":
                if (string.IsNullOrWhiteSpace(value))
                {
                    chatGui.PrintError("Provide a new profile name.", "EST Clock");
                    return;
                }

                Configuration.GetActiveProfile().Name = value.Trim();
                Configuration.Save();
                chatGui.Print($"Profile renamed to \"{Configuration.GetActiveProfile().Name}\".", "EST Clock");
                return;

            case "delete":
                if (Configuration.Profiles.Count <= 1)
                {
                    chatGui.PrintError("At least one profile must remain.", "EST Clock");
                    return;
                }

                var removed = Configuration.GetActiveProfile().Name;
                Configuration.DeleteActiveProfile();
                Configuration.Save();
                chatGui.Print($"Profile \"{removed}\" deleted.", "EST Clock");
                return;

            default:
                chatGui.PrintError("Use: /est profile next|list|set <n>|add <name>|rename <name>|delete", "EST Clock");
                return;
        }
    }

    private bool TryParseTimeZone(string input, out ClockTimeZone zone)
    {
        zone = Configuration.SelectedTimeZone;

        switch (input.Trim().ToLowerInvariant())
        {
            case "est":
                zone = ClockTimeZone.EST;
                return true;

            case "pst":
            case "pdt":
            case "pst/pdt":
            case "pacific":
                zone = ClockTimeZone.Pacific;
                return true;

            case "utc":
            case "gmt":
            case "utc/gmt":
            case "universal":
                zone = ClockTimeZone.Universal;
                return true;

            case "bst":
            case "british":
            case "britishsummer":
            case "british summer":
                zone = ClockTimeZone.BST;
                return true;

            case "jst":
            case "japan":
            case "tokyo":
                zone = ClockTimeZone.JST;
                return true;

            case "mst":
            case "mountain":
                zone = ClockTimeZone.MST;
                return true;

            case "acst":
            case "australiacentral":
            case "australia central":
                zone = ClockTimeZone.ACST;
                return true;

            default:
                return false;
        }
    }

    private void PrintHelp()
    {
        chatGui.Print("/est - toggle clock", "EST Clock");
        chatGui.Print("/est settings - open settings", "EST Clock");
        chatGui.Print("/estalarms - open settings on the alarms tab", "EST Clock");
        chatGui.Print("/est timezone est|pst|utc|bst|jst|mst|acst", "EST Clock");
        chatGui.Print("/est format 12|24", "EST Clock");
        chatGui.Print("/est colon default|always|hidden|slow|fast", "EST Clock");
        chatGui.Print("/est layout horizontal|vertical", "EST Clock");
        chatGui.Print("/est preset classic|minimal|gold|retro", "EST Clock");
        chatGui.Print("/est lock | /est unlock", "EST Clock");
        chatGui.Print("/est profile next|list|set <n>|add <name>|rename <name>|delete", "EST Clock");
    }

    private void SaveAndNotify(string message)
    {
        Configuration.Save();
        chatGui.Print(message, "EST Clock");
    }

    public void SendAlarmOutput(string message)
    {
        chatGui.Print(BuildColoredAlarmMessage(message));
        toastGui.ShowQuest(message, new QuestToastOptions
        {
            PlaySound = false
        });
    }

    public void TestAlarmOutput(string message)
    {
        SendAlarmOutput(message);
    }

    private SeString BuildColoredAlarmMessage(string message)
    {
        var builder = new SeStringBuilder();

        // 57 costuma ficar bem mais próximo de amarelo vivo do que vermelho/laranja.
        builder.Add(new UIForegroundPayload(559));
        builder.AddText("[ALARM]");
        builder.Add(new UIForegroundPayload(0));
        builder.AddText("  ");

        builder.Add(new UIForegroundPayload(45));
        builder.AddText(message);
        builder.Add(new UIForegroundPayload(0));

        return builder.BuiltString;
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();

    public void OpenConfigUiAtAlarms()
    {
        ConfigWindow.OpenToAlarmsTab();
    }

    public void ToggleMainUi()
    {
        wantedMainWindowOpen = !wantedMainWindowOpen;
        MainWindow.IsOpen = wantedMainWindowOpen && !ShouldHideClock();
    }
}