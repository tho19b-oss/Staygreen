using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StayGreen.Core
{
    /// <summary>Wie Aktivitaet erzeugt wird.</summary>
    public enum ActivityMode
    {
        /// <summary>Unauffaelliger Tastendruck (siehe <see cref="ActivityKey"/>).</summary>
        Key,

        /// <summary>Winzige Mausbewegung hin und sofort zurueck.</summary>
        Mouse,

        /// <summary>Taste und Maus.</summary>
        Both,
    }

    /// <summary>
    /// Welche Taste StayGreen drueckt. F13 bis F24 gibt es auf kaum einer Tastatur; manche Programme (Terminals,
    /// Makro-Werkzeuge) reagieren trotzdem darauf. Die Umschalttaste aendert in fast allen Programmen nichts.
    /// Die Reihenfolge ist die Reihenfolge in der Auswahlliste.
    /// </summary>
    public enum ActivityKey
    {
        F13,
        F14,
        F15,
        F16,
        F17,
        F18,
        F19,
        F20,
        F21,
        F22,
        F23,
        F24,
        Shift,
    }

    public enum StopTiming
    {
        /// <summary>Jeden Tag zur gleichen Uhrzeit.</summary>
        Daily,

        /// <summary>Einmalig zu einem bestimmten Datum und einer Uhrzeit.</summary>
        Once,

        /// <summary>Am Ende des letzten Zeitfensters eines Tages (siehe <see cref="AutoStopPlanner"/>).</summary>
        ScheduleEnd,
    }

    /// <summary>
    /// Alle Einstellungen. Gespeichert als einfache, von Hand editierbare Textdatei (Schluessel=Wert),
    /// bewusst ohne JSON-Bibliothek, damit die EXE eine einzelne Datei ohne Abhaengigkeiten bleibt.
    /// </summary>
    public sealed class Settings
    {
        public const int MinInterval = 5;
        public const int MaxInterval = 600;
        public const int MinMousePixels = 1;
        public const int MaxMousePixels = 20;

        /// <summary>
        /// Teams setzt nach etwa 5 Minuten ohne Eingabe auf "Abwesend". Ab diesem Abstand (in Sekunden) greift das
        /// Aktivhalten nicht mehr sicher, die Oberflaeche warnt davor.
        /// </summary>
        public const int IntervalWarnSeconds = 240;

        public const int MinWarnSeconds = 10;
        public const int MaxWarnSeconds = 300;
        public const int MinSnoozeMinutes = 1;
        public const int MaxSnoozeMinutes = 120;
        public const int MaxRuntimeHoursLimit = 48;
        public const int MaxExceptions = 100;

        /// <summary>Tasten, die als globaler Hotkey erlaubt sind: A bis Z und F1 bis F24.</summary>
        public static readonly string[] HotkeyKeys = BuildHotkeyKeys();

        static string[] BuildHotkeyKeys()
        {
            var keys = new List<string>();
            for (char c = 'A'; c <= 'Z'; c++) keys.Add(c.ToString());
            for (int i = 1; i <= 24; i++) keys.Add("F" + i.ToString(CultureInfo.InvariantCulture));
            return keys.ToArray();
        }

        // ---- Aktivitaet ----
        public ActivityMode Mode { get; set; } = ActivityMode.Both;
        public ActivityKey InputKey { get; set; } = ActivityKey.F15;
        public int IntervalSeconds { get; set; } = 30;
        public int MousePixels { get; set; } = 2;

        /// <summary>Nur eingreifen, wenn der Nutzer selbst gerade nichts tut.</summary>
        public bool SmartIdle { get; set; } = true;

        /// <summary>PC und Bildschirm wach halten (kein Standby, kein Bildschirmschoner).</summary>
        public bool KeepAwake { get; set; } = true;

        /// <summary>Nur aktiv halten, solange Teams laeuft (sonst Zustand "wartet auf Teams").</summary>
        public bool OnlyWhileTeamsRuns { get; set; }

        /// <summary>Sicherheitsnetz: nach so vielen Stunden am Stueck automatisch stoppen. 0 = aus.</summary>
        public int MaxRuntimeHours { get; set; }

        // ---- Start und Fenster ----
        public bool StartHoldingOnLaunch { get; set; } = true;
        public bool StartWithWindows { get; set; }
        public bool StartMinimized { get; set; }
        public bool MinimizeToTray { get; set; } = true;

        /// <summary>auto, de oder en.</summary>
        public string Language { get; set; } = "auto";

        /// <summary>auto (folgt Windows), light oder dark.</summary>
        public string Theme { get; set; } = "auto";

        /// <summary>Der Hinweis zur Nutzung (Erststart) wurde bestaetigt.</summary>
        public bool NoticeAccepted { get; set; }

        // ---- Hotkey ----
        public bool HotkeyEnabled { get; set; } = true;
        public bool HotkeyCtrl { get; set; } = true;
        public bool HotkeyAlt { get; set; } = true;
        public bool HotkeyShift { get; set; }
        public bool HotkeyWin { get; set; }
        public string HotkeyKey { get; set; } = "G";

        // ---- Zeitplan ----
        public bool ScheduleEnabled { get; set; }
        public List<ScheduleRule> Rules { get; } = new List<ScheduleRule>();

        /// <summary>Ausnahmetage (Urlaub, Feiertage): An diesen Tagen beginnt kein Zeitfenster.</summary>
        public List<DateRange> Exceptions { get; } = new List<DateRange>();

        // ---- Auto-Stopp ----
        public bool AutoStopEnabled { get; set; }
        public StopTiming AutoStopTiming { get; set; } = StopTiming.Daily;
        public TimeSpan AutoStopTime { get; set; } = new TimeSpan(17, 0, 0);
        public DateTime AutoStopOnce { get; set; } = DateTime.MinValue;
        public bool StopCloseTeams { get; set; }
        public bool StopLock { get; set; }
        public bool StopShutdown { get; set; }
        public bool StopExitApp { get; set; } = true;

        /// <summary>So viele Sekunden vor dem Auto-Stopp erscheint die Vorwarnung (bei Teams beenden, Sperren, Herunterfahren).</summary>
        public int AutoStopWarnSeconds { get; set; } = 60;

        /// <summary>Um so viele Minuten laesst sich der Auto-Stopp in der Vorwarnung verschieben.</summary>
        public int AutoStopSnoozeMinutes { get; set; } = 15;

        // ---- Protokoll ----
        public bool LogEnabled { get; set; }

        /// <summary>Leer = Standardpfad (neben den Einstellungen).</summary>
        public string LogPath { get; set; } = "";

        /// <summary>Zeitplan an und mindestens ein brauchbares Fenster vorhanden.</summary>
        public bool ScheduleActive
        {
            get
            {
                if (!ScheduleEnabled) return false;
                foreach (ScheduleRule rule in Rules)
                    if (rule.IsUsable) return true;
                return false;
            }
        }

        public bool HasStopActions
        {
            get { return StopCloseTeams || StopLock || StopShutdown || StopExitApp; }
        }

        /// <summary>Gilt dieser Tag als Ausnahmetag?</summary>
        public bool IsExcluded(DateTime day)
        {
            return DateRange.ContainsDay(Exceptions, day);
        }

        /// <summary>True, wenn ein Intervall so lang ist, dass Teams trotz Aktivhalten "Abwesend" anzeigen kann.</summary>
        public static bool IsIntervalRisky(int seconds)
        {
            return seconds >= IntervalWarnSeconds;
        }

        /// <summary>Bringt alle Werte in den erlaubten Bereich (nach dem Laden und nach Benutzereingaben).</summary>
        public void Normalize()
        {
            IntervalSeconds = Clamp(IntervalSeconds, MinInterval, MaxInterval);
            MousePixels = Clamp(MousePixels, MinMousePixels, MaxMousePixels);
            MaxRuntimeHours = Clamp(MaxRuntimeHours, 0, MaxRuntimeHoursLimit);
            AutoStopWarnSeconds = Clamp(AutoStopWarnSeconds, MinWarnSeconds, MaxWarnSeconds);
            AutoStopSnoozeMinutes = Clamp(AutoStopSnoozeMinutes, MinSnoozeMinutes, MaxSnoozeMinutes);
            if (!Enum.IsDefined(typeof(ActivityKey), InputKey)) InputKey = ActivityKey.F15;

            if (Language != "de" && Language != "en") Language = "auto";

            string theme = (Theme ?? "").Trim().ToLowerInvariant();
            Theme = theme == "light" || theme == "dark" ? theme : "auto";

            string key = (HotkeyKey ?? "").Trim().ToUpperInvariant();
            HotkeyKey = Array.IndexOf(HotkeyKeys, key) >= 0 ? key : "G";
            // Ein globaler Hotkey ganz ohne Modifier wuerde eine normale Taste im ganzen System kapern.
            if (!HotkeyCtrl && !HotkeyAlt && !HotkeyShift && !HotkeyWin)
            {
                HotkeyCtrl = true;
                HotkeyAlt = true;
            }

            if (AutoStopTime < TimeSpan.Zero || AutoStopTime >= TimeSpan.FromHours(24))
                AutoStopTime = new TimeSpan(17, 0, 0);

            Rules.RemoveAll(r => r == null || !r.IsUsable);
            DateRange.Normalize(Exceptions);
            LogPath = (LogPath ?? "").Trim();
        }

        static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        // ------------------------------------------------------------------ Datei-Format

        public string Serialize()
        {
            var sb = new StringBuilder();
            Line(sb, "; StayGreen-Einstellungen. Nur bei geschlossenem Programm von Hand aendern.");
            Line(sb, "Version=1");
            Add(sb, "Language", Language);
            Add(sb, "Theme", Theme);
            Add(sb, "Mode", Mode.ToString());
            Add(sb, "InputKey", InputKey.ToString());
            Add(sb, "IntervalSeconds", IntervalSeconds);
            Add(sb, "MousePixels", MousePixels);
            Add(sb, "SmartIdle", SmartIdle);
            Add(sb, "KeepAwake", KeepAwake);
            Add(sb, "OnlyWhileTeamsRuns", OnlyWhileTeamsRuns);
            Add(sb, "MaxRuntimeHours", MaxRuntimeHours);

            Add(sb, "StartHoldingOnLaunch", StartHoldingOnLaunch);
            Add(sb, "StartWithWindows", StartWithWindows);
            Add(sb, "StartMinimized", StartMinimized);
            Add(sb, "MinimizeToTray", MinimizeToTray);
            Add(sb, "NoticeAccepted", NoticeAccepted);

            Add(sb, "HotkeyEnabled", HotkeyEnabled);
            Add(sb, "HotkeyCtrl", HotkeyCtrl);
            Add(sb, "HotkeyAlt", HotkeyAlt);
            Add(sb, "HotkeyShift", HotkeyShift);
            Add(sb, "HotkeyWin", HotkeyWin);
            Add(sb, "HotkeyKey", HotkeyKey);

            Add(sb, "ScheduleEnabled", ScheduleEnabled);
            foreach (ScheduleRule rule in Rules)
                Add(sb, "Rule", rule.Serialize());
            foreach (DateRange range in Exceptions)
                Add(sb, "Exception", range.Serialize());

            Add(sb, "AutoStopEnabled", AutoStopEnabled);
            Add(sb, "AutoStopTiming", AutoStopTiming.ToString());
            Add(sb, "AutoStopTime", ScheduleRule.FormatTime(AutoStopTime));
            Add(sb, "AutoStopOnce", AutoStopOnce == DateTime.MinValue
                ? ""
                : AutoStopOnce.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
            Add(sb, "StopCloseTeams", StopCloseTeams);
            Add(sb, "StopLock", StopLock);
            Add(sb, "StopShutdown", StopShutdown);
            Add(sb, "StopExitApp", StopExitApp);
            Add(sb, "AutoStopWarnSeconds", AutoStopWarnSeconds);
            Add(sb, "AutoStopSnoozeMinutes", AutoStopSnoozeMinutes);

            Add(sb, "LogEnabled", LogEnabled);
            Add(sb, "LogPath", LogPath);
            return sb.ToString();
        }

        /// <summary>Liest Einstellungen tolerant: unbekannte Schluessel und kaputte Werte werden ignoriert.</summary>
        public static Settings Parse(string text)
        {
            var s = new Settings();
            if (!string.IsNullOrEmpty(text))
            {
                foreach (string raw in text.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    s.Apply(line.Substring(0, eq).Trim().ToLowerInvariant(), line.Substring(eq + 1).Trim());
                }
            }
            s.Normalize();
            return s;
        }

        void Apply(string key, string value)
        {
            bool b;
            int i;
            switch (key)
            {
                case "language": Language = value.ToLowerInvariant(); break;
                case "theme": Theme = value.ToLowerInvariant(); break;
                case "mode":
                    ActivityMode mode;
                    if (TryEnum(value, out mode)) Mode = mode;
                    break;
                case "inputkey":
                    ActivityKey inputKey;
                    if (TryEnum(value, out inputKey)) InputKey = inputKey;
                    break;
                case "intervalseconds": if (TryInt(value, out i)) IntervalSeconds = i; break;
                case "mousepixels": if (TryInt(value, out i)) MousePixels = i; break;
                case "smartidle": if (TryBool(value, out b)) SmartIdle = b; break;
                case "keepawake": if (TryBool(value, out b)) KeepAwake = b; break;
                case "onlywhileteamsruns": if (TryBool(value, out b)) OnlyWhileTeamsRuns = b; break;
                case "maxruntimehours": if (TryInt(value, out i)) MaxRuntimeHours = i; break;

                case "startholdingonlaunch": if (TryBool(value, out b)) StartHoldingOnLaunch = b; break;
                case "startwithwindows": if (TryBool(value, out b)) StartWithWindows = b; break;
                case "startminimized": if (TryBool(value, out b)) StartMinimized = b; break;
                case "minimizetotray": if (TryBool(value, out b)) MinimizeToTray = b; break;
                case "noticeaccepted": if (TryBool(value, out b)) NoticeAccepted = b; break;

                case "hotkeyenabled": if (TryBool(value, out b)) HotkeyEnabled = b; break;
                case "hotkeyctrl": if (TryBool(value, out b)) HotkeyCtrl = b; break;
                case "hotkeyalt": if (TryBool(value, out b)) HotkeyAlt = b; break;
                case "hotkeyshift": if (TryBool(value, out b)) HotkeyShift = b; break;
                case "hotkeywin": if (TryBool(value, out b)) HotkeyWin = b; break;
                case "hotkeykey": HotkeyKey = value; break;

                case "scheduleenabled": if (TryBool(value, out b)) ScheduleEnabled = b; break;
                case "rule":
                    ScheduleRule rule;
                    if (ScheduleRule.TryParse(value, out rule)) Rules.Add(rule);
                    break;
                case "exception":
                    DateRange range;
                    if (DateRange.TryParse(value, out range)) Exceptions.Add(range);
                    break;

                case "autostopenabled": if (TryBool(value, out b)) AutoStopEnabled = b; break;
                case "autostoptiming":
                    StopTiming timing;
                    if (TryEnum(value, out timing)) AutoStopTiming = timing;
                    break;
                case "autostoptime":
                    TimeSpan time;
                    if (ScheduleRule.TryParseTime(value, out time)) AutoStopTime = time;
                    break;
                case "autostoponce":
                    DateTime once;
                    if (DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out once))
                        AutoStopOnce = once;
                    break;
                case "stopcloseteams": if (TryBool(value, out b)) StopCloseTeams = b; break;
                case "stoplock": if (TryBool(value, out b)) StopLock = b; break;
                case "stopshutdown": if (TryBool(value, out b)) StopShutdown = b; break;
                case "stopexitapp": if (TryBool(value, out b)) StopExitApp = b; break;
                case "autostopwarnseconds": if (TryInt(value, out i)) AutoStopWarnSeconds = i; break;
                case "autostopsnoozeminutes": if (TryInt(value, out i)) AutoStopSnoozeMinutes = i; break;

                case "logenabled": if (TryBool(value, out b)) LogEnabled = b; break;
                case "logpath": LogPath = value; break;
            }
        }

        static bool TryBool(string value, out bool result)
        {
            string v = value.Trim().ToLowerInvariant();
            if (v == "true" || v == "1" || v == "yes" || v == "ja") { result = true; return true; }
            if (v == "false" || v == "0" || v == "no" || v == "nein") { result = false; return true; }
            result = false;
            return false;
        }

        static bool TryInt(string value, out int result)
        {
            return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        static bool TryEnum<T>(string value, out T result) where T : struct
        {
            // Enum.TryParse akzeptiert auch Zahlen ("7"); nur echte Namen zulassen.
            result = default(T);
            string v = value.Trim();
            if (v.Length == 0 || char.IsDigit(v[0]) || v[0] == '-') return false;
            return Enum.TryParse(v, true, out result) && Enum.IsDefined(typeof(T), result);
        }

        static void Line(StringBuilder sb, string text)
        {
            sb.Append(text).Append("\r\n");
        }

        static void Add(StringBuilder sb, string key, string value)
        {
            Line(sb, key + "=" + value);
        }

        static void Add(StringBuilder sb, string key, int value)
        {
            Add(sb, key, value.ToString(CultureInfo.InvariantCulture));
        }

        static void Add(StringBuilder sb, string key, bool value)
        {
            Add(sb, key, value ? "true" : "false");
        }
    }
}
