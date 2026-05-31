using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using Clock.Services;
using Clock.Windows;

namespace Clock;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/clock";
    private const string SettingsCommand = "/clocksettings";
    private const string AlarmsCommand = "/clockalarms";
    private const string DirectAlarmsCommand = "/alarms";

    public const int MinAlarmSoundEffectId = 1;
    public const int MaxAlarmSoundEffectId = 16;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IChatGui chatGui;
    private readonly IToastGui toastGui;
    private readonly LodestoneMaintenanceService maintenanceService = new();
    private readonly ChatTimestampService chatTimestampService;

    private bool hasAutoStarted;
    private bool wantedMainWindowOpen;
    private DateTime lastReminderCheckUtc = DateTime.MinValue;
    private DateTime lastMaintenanceRefreshStartUtc = DateTime.MinValue;
    private Task<LodestoneMaintenanceInfo?>? maintenanceRefreshTask;
    private bool maintenanceRefreshRequestedManually;

    private readonly HashSet<string> triggeredMaintenanceKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> recentlyTriggeredAlarmIds = new();

    public Configuration Configuration { get; private set; }

    public readonly WindowSystem WindowSystem = new("Clock");

    public IDalamudPluginInterface PluginInterface => pluginInterface;

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log,
        IClientState clientState,
        ICondition condition,
        IChatGui chatGui,
        IToastGui toastGui,
        IGameInteropProvider gameInteropProvider)
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

        chatTimestampService = new ChatTimestampService(Configuration, gameInteropProvider, log);

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage =
                "Clock commands: /clock, /clock help, /alarms, /clock timezone <TimeZoneInfo ID>, /clock format 12|24|12s|24s|weekday|date, " +
                "/clock colon default|always|hidden|slow|fast, /clock layout horizontal|vertical, " +
                "/clock preset classic|minimal|gold|retro, /clock lock, /clock unlock, " +
                "/clock profile next|list|set <n>|add <name>|rename <name>|delete"
        });

        commandManager.AddHandler(SettingsCommand, new CommandInfo(OnSettingsCommand)
        {
            HelpMessage = "Open Clock settings/customizations"
        });

        commandManager.AddHandler(AlarmsCommand, new CommandInfo(OnAlarmsCommand)
        {
            HelpMessage = "Open Clock settings directly on the Alarms tab"
        });

        commandManager.AddHandler(DirectAlarmsCommand, new CommandInfo(OnAlarmsCommand)
        {
            HelpMessage = "Open Clock settings directly on the Alarms tab"
        });

        pluginInterface.UiBuilder.DisableCutsceneUiHide = !Configuration.HideDuringCutscenes;
        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        pluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

    }


    public void RefreshChatTimestampSettings()
    {
        chatTimestampService.ApplyConfiguration();
    }

    public void Dispose()
    {
        Configuration.Save();

        chatTimestampService.Dispose();
        maintenanceService.Dispose();

        pluginInterface.UiBuilder.Draw -= DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        commandManager.RemoveHandler(CommandName);
        commandManager.RemoveHandler(SettingsCommand);
        commandManager.RemoveHandler(AlarmsCommand);
        commandManager.RemoveHandler(DirectAlarmsCommand);
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
        UpdateMaintenanceDetection(nowUtc);
        CheckMaintenanceReminder(nowUtc);
    }

    private void CheckAllAlarms(DateTime nowUtc)
    {
        if (Configuration.Alarms == null || Configuration.Alarms.Count == 0)
            return;

        bool changed = false;

        foreach (var alarm in Configuration.Alarms)
        {
            if (!alarm.Enabled)
                continue;

            var hasPendingSnooze = alarm.SnoozedUntilUtc > DateTime.MinValue && !alarm.SnoozeTriggered;
            if (alarm.HasTriggered && !hasPendingSnooze)
                continue;

            if (!AlarmConfigurationService.TryGetPendingTriggerUtc(alarm, out var alarmUtc))
                continue;

            if (nowUtc < alarmUtc)
            {
                recentlyTriggeredAlarmIds.Remove(alarm.Id);
                continue;
            }

            if ((nowUtc - alarmUtc).TotalSeconds > 60)
                continue;

            if (recentlyTriggeredAlarmIds.Contains(alarm.Id))
                continue;

            recentlyTriggeredAlarmIds.Add(alarm.Id);
            changed = true;

            var isSnoozeTrigger = hasPendingSnooze;
            var triggerMessage = alarm.BuildTriggerMessage(Configuration.TimeFormat, isSnoozeTrigger);
            SendAlarmOutput(triggerMessage);

            if (isSnoozeTrigger)
            {
                alarm.SnoozedUntilUtc = DateTime.MinValue;
                alarm.SnoozeTriggered = true;
                alarm.HasTriggered = true;
                continue;
            }

            alarm.HasTriggered = true;
            alarm.SnoozeTriggered = false;
            alarm.SnoozeCanceled = false;

            if (!AlarmConfigurationService.ScheduleSnooze(alarm, nowUtc))
                alarm.SnoozedUntilUtc = DateTime.MinValue;
        }

        if (changed)
            Configuration.Save();
    }

    private void UpdateMaintenanceDetection(DateTime nowUtc)
    {
        if (maintenanceRefreshTask != null)
        {
            if (!maintenanceRefreshTask.IsCompleted)
                return;

            var wasManual = maintenanceRefreshRequestedManually;
            try
            {
                var maintenance = maintenanceRefreshTask.Result;
                var result = ApplyMaintenanceRefreshResult(maintenance);
                if (wasManual)
                {
                    Configuration.LastMaintenanceCheckStatus = result;
                    Configuration.Save();
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Failed checking Lodestone maintenance news.");
                if (wasManual)
                {
                    Configuration.LastMaintenanceCheckStatus = $"Maintenance check failed: {ex.Message}";
                    Configuration.Save();
                }
            }
            finally
            {
                maintenanceRefreshTask = null;
                maintenanceRefreshRequestedManually = false;
            }
        }

        if (!Configuration.MaintenanceReminderEnabled)
            return;

        if ((nowUtc - lastMaintenanceRefreshStartUtc).TotalHours < 6)
            return;

        lastMaintenanceRefreshStartUtc = nowUtc;
        Configuration.LastMaintenanceDetectionTimestampUtc = nowUtc;
        Configuration.Save();

        maintenanceRefreshRequestedManually = false;
        maintenanceRefreshTask = maintenanceService.GetLatestMaintenanceAsync(
            Configuration.MaintenanceLanguage,
            Configuration.LastMaintenanceNewsUrl,
            Configuration.DetectedMaintenanceStartUtc);
    }

    private string ApplyMaintenanceRefreshResult(LodestoneMaintenanceInfo? maintenance)
    {
        Configuration.LastMaintenanceDetectionTimestampUtc = DateTime.UtcNow;

        if (maintenance == null)
        {
            Configuration.Save();
            return "No active or upcoming maintenance notice was found.";
        }

        bool changed =
            !string.Equals(Configuration.LastMaintenanceNewsUrl, maintenance.Url, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Configuration.LastMaintenanceNewsTitle, maintenance.Title, StringComparison.Ordinal) ||
            !string.Equals(Configuration.DetectedMaintenanceDateTimeText, maintenance.LocalStartText, StringComparison.Ordinal) ||
            !string.Equals(Configuration.DetectedMaintenanceTimeZoneText, maintenance.TimeZoneText, StringComparison.Ordinal) ||
            !string.Equals(Configuration.LastDetectedMaintenanceMessage, maintenance.BuildSummary(), StringComparison.Ordinal) ||
            Configuration.DetectedMaintenanceStartUtc != maintenance.StartUtc ||
            !Configuration.HasDetectedMaintenanceTime;

        Configuration.LastMaintenanceNewsTitle = maintenance.Title;
        Configuration.LastMaintenanceNewsUrl = maintenance.Url;
        Configuration.LastDetectedMaintenanceMessage = maintenance.BuildSummary();
        Configuration.DetectedMaintenanceDateTimeText = maintenance.LocalStartText;
        Configuration.DetectedMaintenanceTimeZoneText = maintenance.TimeZoneText;
        Configuration.DetectedMaintenanceStartUtc = maintenance.StartUtc;
        Configuration.HasDetectedMaintenanceTime = true;
        Configuration.Save();

        return changed
            ? $"Maintenance notice found: {maintenance.Title}"
            : string.Format(CultureInfo.InvariantCulture, "Maintenance checked. Latest detected maintenance is already current: {0}.", $"{maintenance.LocalStartText} {maintenance.TimeZoneText}");
    }

    private void CheckMaintenanceReminder(DateTime nowUtc)
    {
        if (!Configuration.MaintenanceReminderEnabled)
            return;

        DateTime maintenanceUtc;

        if (Configuration.DetectedMaintenanceStartUtc > DateTime.MinValue)
        {
            maintenanceUtc = DateTime.SpecifyKind(Configuration.DetectedMaintenanceStartUtc, DateTimeKind.Utc);
        }
        else if (!string.IsNullOrWhiteSpace(Configuration.DetectedMaintenanceDateTimeText) &&
                 TimeZoneHelper.TryParseInZone(
                     Configuration.DetectedMaintenanceDateTimeText,
                     Configuration.SelectedTimeZoneId,
                     out var legacyMaintenanceUtc))
        {
            maintenanceUtc = legacyMaintenanceUtc;
        }
        else
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
            ? (Math.Abs(lead.TotalHours - 24) < 0.01 ? T("24 hours") : T("1 hour"))
            : T("15 minutes");

        var zoneText = string.IsNullOrWhiteSpace(Configuration.DetectedMaintenanceTimeZoneText)
            ? TimeZoneHelper.ToShortText(Configuration.SelectedTimeZoneId)
            : Configuration.DetectedMaintenanceTimeZoneText;

        var whenText = $"{Configuration.DetectedMaintenanceDateTimeText} {zoneText}";
        var message = string.Format(CultureInfo.InvariantCulture, T("Scheduled maintenance starts in {0}. ({1})"), leadText, whenText);
        chatGui.Print(message, "Clock");
        toastGui.ShowQuest(message, new QuestToastOptions
        {
            PlaySound = false
        });
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
                chatGui.Print($"Clock {(wantedMainWindowOpen ? "opened" : "hidden")}.", "Clock");
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
        if (!TimeZoneHelper.TryResolveTimeZone(rest, out var timeZoneId))
        {
            chatGui.PrintError("Invalid timezone. Use a valid TimeZoneInfo ID like \"Eastern Standard Time\" or \"America/New_York\".", "Clock");
            return;
        }

        Configuration.SelectedTimeZoneId = timeZoneId;
        Configuration.Save();

        chatGui.Print($"Timezone set to {TimeZoneHelper.GetComboLabel(timeZoneId)}.", "Clock");
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

            case "12s":
            case "12sec":
            case "12seconds":
                Configuration.TimeFormat = ClockTimeFormat.TwelveHourSeconds;
                NormalizeAlarmEditorHourForNewFormat();
                SaveAndNotify("Time format set to 12h with seconds.");
                return;

            case "24s":
            case "24sec":
            case "24seconds":
                Configuration.TimeFormat = ClockTimeFormat.TwentyFourHourSeconds;
                NormalizeAlarmEditorHourForNewFormat();
                SaveAndNotify("Time format set to 24h with seconds.");
                return;

            case "weekday":
            case "day":
                Configuration.TimeFormat = ClockTimeFormat.WeekdayTwentyFourHour;
                NormalizeAlarmEditorHourForNewFormat();
                SaveAndNotify("Time format set to weekday + 24h.");
                return;

            case "date":
                Configuration.TimeFormat = ClockTimeFormat.DateTwentyFourHour;
                NormalizeAlarmEditorHourForNewFormat();
                SaveAndNotify("Time format set to date + 24h.");
                return;

            default:
                chatGui.PrintError("Invalid format. Use 12, 24, 12s, 24s, weekday or date.", "Clock");
                return;
        }
    }

    private void NormalizeAlarmEditorHourForNewFormat()
    {
        if (TimeFormatHelper.UsesTwelveHourClock(Configuration.TimeFormat))
        {
            int sourceHour24;

            if (Configuration.AlarmEditorHour >= 0 && Configuration.AlarmEditorHour <= 23)
            {
                sourceHour24 = Configuration.AlarmEditorHour;
            }
            else
            {
                var nowInZone = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, Configuration.SelectedTimeZoneId);
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
            chatGui.PrintError("Invalid colon mode. Use default, always, hidden, slow or fast.", "Clock");
            return;
        }

        Configuration.Save();
        chatGui.Print($"Colon animation set to {Configuration.ColonAnimation}.", "Clock");
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
                chatGui.PrintError("Invalid layout. Use horizontal or vertical.", "Clock");
                return;
        }

        Configuration.Save();
        chatGui.Print($"Layout set to {profile.LayoutMode}.", "Clock");
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
            chatGui.PrintError("Invalid preset. Use classic, minimal, gold or retro.", "Clock");
            return;
        }

        Configuration.PreviewPresetSelection = preset;
        Configuration.Save();
        chatGui.Print($"Preset selected: {preset}.", "Clock");
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
                chatGui.Print($"Active profile: {Configuration.GetActiveProfile().Name}", "Clock");
                return;

            case "list":
                var list = string.Join(", ", Configuration.Profiles.Select((p, i) => $"{i + 1}:{p.Name}"));
                chatGui.Print($"Profiles: {list}", "Clock");
                return;

            case "set":
                if (int.TryParse(value, out var idx) && idx >= 1 && idx <= Configuration.Profiles.Count)
                {
                    Configuration.ActiveProfileIndex = idx - 1;
                    Configuration.Save();
                    chatGui.Print($"Active profile: {Configuration.GetActiveProfile().Name}", "Clock");
                }
                else
                {
                    chatGui.PrintError("Invalid profile index.", "Clock");
                }

                return;

            case "add":
            {
                var name = string.IsNullOrWhiteSpace(value)
                    ? $"Profile {Configuration.Profiles.Count + 1}"
                    : value.Trim();

                Configuration.AddProfile(name);
                Configuration.Save();
                chatGui.Print($"Profile \"{Configuration.GetActiveProfile().Name}\" created.", "Clock");
                return;
            }

            case "rename":
                if (string.IsNullOrWhiteSpace(value))
                {
                    chatGui.PrintError("Provide a new profile name.", "Clock");
                    return;
                }

                Configuration.GetActiveProfile().Name = value.Trim();
                Configuration.Save();
                chatGui.Print($"Profile renamed to \"{Configuration.GetActiveProfile().Name}\".", "Clock");
                return;

            case "delete":
                if (Configuration.Profiles.Count <= 1)
                {
                    chatGui.PrintError("At least one profile must remain.", "Clock");
                    return;
                }

                var removed = Configuration.GetActiveProfile().Name;
                Configuration.DeleteActiveProfile();
                Configuration.Save();
                chatGui.Print($"Profile \"{removed}\" deleted.", "Clock");
                return;

            default:
                chatGui.PrintError("Use: /clock profile next|list|set <n>|add <name>|rename <name>|delete", "Clock");
                return;
        }
    }

    private void PrintHelp()
    {
        chatGui.Print("/clock - toggle clock", "Clock");
        chatGui.Print("/clock settings - open settings", "Clock");
        chatGui.Print("/clockalarms or /alarms - open settings on the alarms tab", "Clock");
        chatGui.Print("/clock timezone <TimeZoneInfo ID or alias>", "Clock");
        chatGui.Print("/clock format 12|24|12s|24s|weekday|date", "Clock");
        chatGui.Print("/clock colon default|always|hidden|slow|fast", "Clock");
        chatGui.Print("/clock layout horizontal|vertical", "Clock");
        chatGui.Print("/clock preset classic|minimal|gold|retro", "Clock");
        chatGui.Print("/clock lock | /clock unlock", "Clock");
        chatGui.Print("/clock profile next|list|set <n>|add <name>|rename <name>|delete", "Clock");
    }

    private void SaveAndNotify(string message)
    {
        Configuration.Save();
        chatGui.Print(message, "Clock");
    }

    public void SendAlarmOutput(string message)
    {
        chatGui.Print(BuildColoredAlarmMessage(message));
        toastGui.ShowQuest(message, new QuestToastOptions
        {
            PlaySound = false
        });
        PlayAlarmSoundEffect(Configuration.AlarmSoundId);
    }

    public void TestAlarmOutput(string message)
    {
        SendAlarmOutput(message);
    }

    public void PlaySelectedAlarmSoundOnly()
    {
        PlayAlarmSoundEffect(Configuration.AlarmSoundId);
    }

    private unsafe void PlayAlarmSoundEffect(int soundId)
    {
        try
        {
            UIGlobals.PlayChatSoundEffect((uint)Math.Clamp(soundId, MinAlarmSoundEffectId, MaxAlarmSoundEffectId));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to play Clock alarm sound effect.");
        }
    }

    private SeString BuildColoredAlarmMessage(string message)
    {
        var builder = new SeStringBuilder();

        builder.Add(new UIForegroundPayload(559));
        builder.AddText("[ALARM]");
        builder.Add(new UIForegroundPayload(0));
        builder.AddText("  ");

        builder.Add(new UIForegroundPayload(45));
        builder.AddText(message);
        builder.Add(new UIForegroundPayload(0));

        return builder.BuiltString;
    }

    public bool TryExportConfiguration(string path, out string error)
    {
        try
        {
            Configuration.EnsureInitialized();
            ConfigurationFileService.Export(Configuration, path);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to export Clock configuration.");
            error = ex.Message;
            return false;
        }
    }

    public bool TryImportConfiguration(string path, out string error)
    {
        try
        {
            var importedConfiguration = ConfigurationFileService.Import(path);
            importedConfiguration.Initialize(pluginInterface);
            importedConfiguration.EnsureInitialized();

            Configuration.CopyPublicStateFrom(importedConfiguration);
            Configuration.Initialize(pluginInterface);
            Configuration.EnsureInitialized();
            Configuration.Save();

            triggeredMaintenanceKeys.Clear();
            recentlyTriggeredAlarmIds.Clear();
            pluginInterface.UiBuilder.DisableCutsceneUiHide = !Configuration.HideDuringCutscenes;

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to import Clock configuration.");
            error = ex.Message;
            return false;
        }
    }

    private string T(string text)
    {
        return ClockLocalizationService.Translate(Configuration.UiLanguageCultureName, text);
    }

    public bool IsMaintenanceRefreshRunning => maintenanceRefreshTask is { IsCompleted: false };

    public bool RequestMaintenanceRefresh(bool forceRefresh)
    {
        if (maintenanceRefreshTask is { IsCompleted: false })
            return false;

        var nowUtc = DateTime.UtcNow;
        lastMaintenanceRefreshStartUtc = nowUtc;
        maintenanceRefreshRequestedManually = forceRefresh;
        Configuration.LastMaintenanceDetectionTimestampUtc = nowUtc;
        Configuration.LastMaintenanceCheckStatus = forceRefresh ? "Checking Lodestone maintenance notices..." : Configuration.LastMaintenanceCheckStatus;
        Configuration.Save();

        maintenanceRefreshTask = maintenanceService.GetLatestMaintenanceAsync(
            Configuration.MaintenanceLanguage,
            Configuration.LastMaintenanceNewsUrl,
            Configuration.DetectedMaintenanceStartUtc,
            forceRefresh);

        return true;
    }

    public void ClearRecentlyTriggeredAlarm(Guid alarmId)
    {
        recentlyTriggeredAlarmIds.Remove(alarmId);
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