using System;
using System.Collections.Generic;

namespace Clock;

public partial class Configuration
{
    public int AlarmEditorDay = 1;
    public int AlarmEditorHour = 1;
    public int AlarmEditorMinute = 0;
    public bool AlarmEditorIsPm = false;
    public string AlarmEditorMessage = "";
    public string AlarmEditorLastLocalDateText = "";
    public int AlarmSoundId = 8;

    public List<AlarmEntry> Alarms = new();

    public bool MaintenanceReminderEnabled = false;
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

    // Legacy fields kept only for old saved configs migration
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
