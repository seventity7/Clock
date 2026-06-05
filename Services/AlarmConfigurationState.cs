using System;
using System.Collections.Generic;
using System.Numerics;

namespace Clock;

public partial class Configuration
{
    public int AlarmEditorDay = 1;
    public int AlarmEditorHour = 1;
    public int AlarmEditorMinute = 0;
    public bool AlarmEditorIsPm = false;
    public string AlarmEditorMessage = "";
    public string AlarmEditorLastLocalDateText = "";
    public string AlarmEditorDateOverrideText = "";
    public int AlarmSoundId = 8;
    public bool AlarmEditorSnoozeEnabled = false;
    public int AlarmEditorSnoozeMinutes = 5;
    public AlarmRepeatMode AlarmEditorRepeatMode = AlarmRepeatMode.None;
    public bool ShowNextAlarmOnOverlay = false;
    public float NextAlarmOverlayTextScale = 0.72f;
    public float NextAlarmOverlayVerticalOffset = 0.0f;
    public ClockDisplayStyle NextAlarmOverlayDisplayStyle = ClockDisplayStyle.Classic;
    public bool NextAlarmOverlayShowShadowText = true;
    public Vector4 NextAlarmOverlayTextColor = new(1f, 1f, 1f, 1f);
    public Vector4 NextAlarmOverlayShadowColor = new(0f, 0f, 0f, 0.8f);
    public bool ShowResetTimersOnOverlay = false;
    public bool CommandSuggestionEnabled = true;

    public List<AlarmEntry> Alarms = new();

    public bool MaintenanceReminderEnabled = false;
    public bool ShowMaintenanceOnOverlay = false;
    public float MaintenanceOverlayTextScale = 0.72f;
    public float MaintenanceOverlayVerticalOffset = 0.0f;
    public ClockDisplayStyle MaintenanceOverlayDisplayStyle = ClockDisplayStyle.Classic;
    public bool MaintenanceOverlayShowShadowText = true;
    public Vector4 MaintenanceOverlayTextColor = new(1f, 1f, 1f, 1f);
    public Vector4 MaintenanceOverlayShadowColor = new(0f, 0f, 0f, 0.8f);
    public LodestoneMaintenanceLanguage MaintenanceLanguage = LodestoneMaintenanceLanguage.EnglishUs;
    public bool MaintenanceRemind24Hours = true;
    public bool MaintenanceRemind1Hour = true;
    public bool MaintenanceRemind15Minutes = true;
    public string LastDetectedMaintenanceMessage = "";
    public string DetectedMaintenanceDateTimeText = "";
    public string DetectedMaintenanceTimeZoneText = "";
    public DateTime DetectedMaintenanceStartUtc = DateTime.MinValue;
    public string LastMaintenanceNewsTitle = "";
    public string LastMaintenanceNewsUrl = "";
    public bool HasDetectedMaintenanceTime = false;
    public DateTime LastMaintenanceDetectionTimestampUtc = DateTime.MinValue;
    public string LastMaintenanceCheckStatus = "";

    public bool CustomAlarmEnabled = false;
    public bool CustomAlarmKeepAfterTrigger = false;
    public bool CustomAlarmSoundEnabled = true;
    public int CustomAlarmSoundEffect = 1;
    public string CustomAlarmDateTimeText = "";
    public string CustomAlarmMessage = "Custom reminder";
    public int CustomAlarmDay = 1;
    public int CustomAlarmHour = 1;
    public int CustomAlarmMinute = 0;
}
