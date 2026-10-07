using System;
using System.Collections.Generic;
using System.Globalization;

namespace StayGreen.Core
{
    public enum StatusKind
    {
        Stopped,
        Active,
        StandingBy,
        WaitingForWindow,
        Paused,
        WaitingForTeams,
        Blocked,
        SessionLocked,
    }

    public sealed class StatusInfo
    {
        public StatusKind Kind { get; set; }

        /// <summary>Kurze Ueberschrift fuer die Statuskarte.</summary>
        public string Title { get; set; }

        /// <summary>Eine Zeile mit Details; leer, wenn die Ueberschrift genuegt (laeuft einfach, nichts zu erklaeren).</summary>
        public string Detail { get; set; }
    }

    /// <summary>Baut die Texte fuer Statuskarte, Tray und Planzeile (ohne Oberflaeche, daher testbar).</summary>
    public static class StatusBuilder
    {
        public static StatusInfo Build(HolderEngine engine, Settings settings, DateTime now, bool sessionLocked)
        {
            var info = new StatusInfo();

            if (!engine.Running)
            {
                info.Kind = StatusKind.Stopped;
                info.Title = Loc.T("status.stopped");
                info.Detail = Loc.T("status.stopped.detail");
            }
            else if (sessionLocked)
            {
                info.Kind = StatusKind.SessionLocked;
                info.Title = Loc.T("status.locked");
                info.Detail = Loc.T("status.locked.detail");
            }
            else if (engine.State == HolderState.Paused)
            {
                info.Kind = StatusKind.Paused;
                info.Title = Loc.T("status.paused");
                DateTime? until = engine.PausedUntil;
                info.Detail = until.HasValue
                    ? Loc.T("status.paused.detail", When(until.Value, now))
                    : Loc.T("status.paused.detail.open");
            }
            else if (engine.State == HolderState.WaitingForWindow)
            {
                info.Kind = StatusKind.WaitingForWindow;
                info.Title = Loc.T("status.waiting");
                DateTime? next = Scheduler.NextChange(now, settings.Rules);
                info.Detail = next.HasValue
                    ? Loc.T("status.waiting.detail", When(next.Value, now))
                    : Loc.T("status.waiting.detail.none");
            }
            else if (engine.State == HolderState.WaitingForTeams)
            {
                info.Kind = StatusKind.WaitingForTeams;
                info.Title = Loc.T("status.teams");
                info.Detail = Loc.T("status.teams.detail");
            }
            else if (engine.ConsecutiveFailures >= HolderEngine.BlockedThreshold)
            {
                info.Kind = StatusKind.Blocked;
                info.Title = Loc.T("status.blocked");
                info.Detail = Loc.T("status.blocked.detail");
            }
            else if (engine.StandingBy)
            {
                info.Kind = StatusKind.StandingBy;
                info.Title = Loc.T("status.standby");
                info.Detail = Loc.T("status.standby.detail", settings.IntervalSeconds);
            }
            else
            {
                // Bewusst ohne Details: Countdown, Aktionszaehler und Laufzeit wuerden jede Sekunde mitticken, ohne etwas zu
                // entscheiden. Faellt etwas aus, zeigt der Zustand "Eingaben werden abgelehnt" es von selbst.
                info.Kind = StatusKind.Active;
                info.Title = Loc.T("status.active");
                info.Detail = "";
            }
            return info;
        }

        /// <summary>
        /// Eine Zeile zu Zeitplan und Auto-Stopp (leer, wenn beides aus ist). Steht der Zeitplan schon in der
        /// Detailzeile (Zustand "wartet auf Zeitfenster"), laesst man ihn hier weg.
        /// </summary>
        public static string PlanLine(Settings settings, DateTime now, DateTime? autoStopDue, bool includeSchedule = true)
        {
            var parts = new List<string>();
            string schedule = includeSchedule ? ScheduleLine(settings, now) : null;
            if (schedule != null) parts.Add(schedule);
            string stop = AutoStopLine(settings, now, autoStopDue);
            if (stop != null) parts.Add(stop);
            return string.Join("   ·   ", parts);
        }

        /// <summary>"Zeitplan: aktiv bis 17:00" bzw. "Zeitplan: naechster Start Mo 08:00"; null ohne wirksamen Zeitplan.</summary>
        public static string ScheduleLine(Settings settings, DateTime now)
        {
            if (!settings.ScheduleActive) return null;
            bool inside = Scheduler.IsActive(now, settings.Rules);
            DateTime? next = Scheduler.NextChange(now, settings.Rules);
            if (next.HasValue)
                return Loc.T(inside ? "plan.schedule.until" : "plan.schedule.from", When(next.Value, now));
            return Loc.T(inside ? "plan.schedule.always" : "plan.schedule.never");
        }

        /// <summary>"Auto-Stopp: heute 17:00"; null, wenn der Auto-Stopp aus ist.</summary>
        public static string AutoStopLine(Settings settings, DateTime now, DateTime? due)
        {
            if (!settings.AutoStopEnabled) return null;
            if (due.HasValue) return Loc.T("plan.autostop", When(due.Value, now));

            if (settings.AutoStopTiming == StopTiming.ScheduleEnd)
                return Loc.T(settings.ScheduleActive ? "plan.autostop.noend" : "plan.autostop.noschedule");
            return Loc.T("plan.autostop.expired");
        }

        /// <summary>
        /// Bietet die Statuskarte Pausen-Knoepfe an? Nur dort, wo eine Pause etwas bewirkt: StayGreen haelt gerade aktiv oder
        /// steht bereit. Wartet es ohnehin (Zeitfenster, Teams), ist die Sitzung gesperrt, werden Eingaben abgelehnt oder ist
        /// es schon pausiert bzw. gestoppt, waeren die Knoepfe nur Ballast.
        /// </summary>
        public static bool OffersPause(StatusKind kind)
        {
            return kind == StatusKind.Active || kind == StatusKind.StandingBy;
        }

        /// <summary>"15 Minuten", "1 Stunde", "2 Stunden" fuer die Pausen-Auswahl (Tray-Menue und Fenster).</summary>
        public static string PauseLabel(int minutes)
        {
            if (minutes < 60 || minutes % 60 != 0) return Loc.T("tray.pause.minutes", minutes);
            int hours = minutes / 60;
            return hours == 1 ? Loc.T("tray.pause.hour") : Loc.T("tray.pause.hours", hours);
        }

        /// <summary>"heute 17:00", "morgen 08:00", "Mo 08:00" und "12.10. 08:00" (englisch: "Oct 12 08:00").</summary>
        public static string When(DateTime when, DateTime now)
        {
            string time = ScheduleRule.FormatTime(when.TimeOfDay);
            int days = (int)(when.Date - now.Date).TotalDays;
            if (days == 0) return Loc.T("when.today") + " " + time;
            if (days == 1) return Loc.T("when.tomorrow") + " " + time;
            if (days > 1 && days < 7) return Loc.DayShort(ScheduleRule.DayIndex(when.DayOfWeek)) + " " + time;
            string date = Loc.Language == "en"
                ? when.ToString("MMM d", CultureInfo.InvariantCulture)
                : when.ToString("dd.MM.", CultureInfo.InvariantCulture);
            return date + " " + time;
        }
    }

    /// <summary>Wochentage und Zeitfenster als lesbarer Text.</summary>
    public static class RuleFormatter
    {
        /// <summary>"Mo-Fr", "Mo, Mi, Fr", "taeglich" ... (Laeufe ab drei Tagen werden zusammengefasst).</summary>
        public static string Days(bool[] days)
        {
            int count = 0;
            foreach (bool d in days)
                if (d) count++;
            if (count == 7) return Loc.T("days.daily");
            if (count == 0) return Loc.T("days.none");

            var parts = new List<string>();
            int i = 0;
            while (i < 7)
            {
                if (!days[i])
                {
                    i++;
                    continue;
                }
                int j = i;
                while (j + 1 < 7 && days[j + 1]) j++;
                if (j - i + 1 >= 3)
                    parts.Add(Loc.DayShort(i) + "–" + Loc.DayShort(j));
                else
                    for (int k = i; k <= j; k++) parts.Add(Loc.DayShort(k));
                i = j + 1;
            }
            return string.Join(", ", parts);
        }

        public static string Describe(ScheduleRule rule)
        {
            string text = Days(rule.Days) + "   " + ScheduleRule.FormatTime(rule.Start) + " – " + ScheduleRule.FormatTime(rule.End);
            if (rule.CrossesMidnight) text += "  " + Loc.T("rule.overnight");
            return text;
        }
    }

    /// <summary>Zuordnung der Hotkey-Einstellung zu Windows-Tastencodes und Anzeigetext.</summary>
    public static class HotkeyInfo
    {
        public static bool TryGetVirtualKey(string key, out uint virtualKey)
        {
            virtualKey = 0;
            if (string.IsNullOrEmpty(key)) return false;
            if (key.Length == 1 && key[0] >= 'A' && key[0] <= 'Z')
            {
                virtualKey = key[0];
                return true;
            }
            int n;
            if (key[0] == 'F' && int.TryParse(key.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out n)
                && n >= 1 && n <= 24)
            {
                virtualKey = (uint)(0x70 + n - 1);   // VK_F1 = 0x70 ... VK_F24 = 0x87
                return true;
            }
            return false;
        }

        /// <summary>"Strg+Alt+G".</summary>
        public static string Describe(Settings s)
        {
            var parts = new List<string>();
            if (s.HotkeyCtrl) parts.Add(Loc.T("key.ctrl"));
            if (s.HotkeyAlt) parts.Add(Loc.T("key.alt"));
            if (s.HotkeyShift) parts.Add(Loc.T("key.shift"));
            if (s.HotkeyWin) parts.Add(Loc.T("key.win"));
            parts.Add(s.HotkeyKey);
            return string.Join("+", parts);
        }
    }
}
