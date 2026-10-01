using System;
using System.Globalization;

namespace StayGreen.Core
{
    public enum HolderState
    {
        /// <summary>Nicht gestartet.</summary>
        Stopped,

        /// <summary>Gestartet und gerade im Dienst (kein Zeitplan oder innerhalb eines Zeitfensters).</summary>
        Active,

        /// <summary>Gestartet, wartet aber auf das naechste Zeitfenster.</summary>
        WaitingForWindow,
    }

    /// <summary>
    /// Das Herzstueck: entscheidet bei jedem Takt, ob jetzt eine Eingabe erzeugt werden soll.
    /// Kennt weder Fenster noch Windows-API, nur <see cref="IInputBackend"/>. Die Zeit wird von aussen
    /// uebergeben, dadurch laesst sich alles ohne Warten testen.
    /// </summary>
    public sealed class HolderEngine
    {
        readonly IInputBackend _input;
        readonly Settings _settings;
        readonly Action<DateTime, string, string> _log;

        bool _running;
        bool _awakeApplied;
        DateTime _nextActivity;

        public HolderEngine(IInputBackend input, Settings settings, Action<DateTime, string, string> log)
        {
            if (input == null) throw new ArgumentNullException("input");
            if (settings == null) throw new ArgumentNullException("settings");
            _input = input;
            _settings = settings;
            _log = log;
            State = HolderState.Stopped;
        }

        public HolderState State { get; private set; }

        public bool Running
        {
            get { return _running; }
        }

        public DateTime? StartedAt { get; private set; }

        public DateTime? LastActivity { get; private set; }

        public int ActivityCount { get; private set; }

        /// <summary>Fehlgeschlagene Eingaben in Folge (z. B. weil die Sitzung gesperrt ist).</summary>
        public int ConsecutiveFailures { get; private set; }

        /// <summary>Intelligenter Modus: Der Nutzer arbeitet gerade selbst, es wird nichts erzeugt.</summary>
        public bool StandingBy { get; private set; }

        /// <summary>Zuletzt gemessene Leerlaufzeit (nur im intelligenten Modus gemessen).</summary>
        public TimeSpan LastIdle { get; private set; }

        /// <summary>Wann die naechste Eingabe geplant ist (nur sinnvoll im Zustand Active ohne Bereitschaft).</summary>
        public DateTime? NextActivityAt
        {
            get
            {
                if (!_running || State != HolderState.Active || StandingBy) return null;
                return _nextActivity;
            }
        }

        public TimeSpan Elapsed(DateTime now)
        {
            if (!_running || !StartedAt.HasValue) return TimeSpan.Zero;
            TimeSpan t = now - StartedAt.Value;
            return t < TimeSpan.Zero ? TimeSpan.Zero : t;
        }

        public void Start(DateTime now, string reason)
        {
            if (_running) return;
            _running = true;
            StartedAt = now;
            LastActivity = null;
            ActivityCount = 0;
            ConsecutiveFailures = 0;
            StandingBy = false;
            State = HolderState.Active;
            _nextActivity = now;
            Log(now, "START", reason);
            Tick(now); // Zustand sofort richtig setzen (z. B. "wartet auf Zeitfenster")
        }

        public void Stop(DateTime now, string reason)
        {
            if (!_running) return;
            TimeSpan runtime = Elapsed(now);
            _running = false;
            State = HolderState.Stopped;
            StandingBy = false;
            ApplyAwake(false);
            Log(now, "STOP", string.IsNullOrEmpty(reason)
                ? Loc.T("log.runtime", Format.Duration(runtime))
                : reason + " - " + Loc.T("log.runtime", Format.Duration(runtime)));
        }

        /// <summary>Wird einmal pro Sekunde aufgerufen.</summary>
        public void Tick(DateTime now)
        {
            if (!_running) return;

            // Ausserhalb der Zeitfenster: nichts tun und auch den PC nicht wach halten.
            if (_settings.ScheduleActive && !Scheduler.IsActive(now, _settings.Rules))
            {
                if (State != HolderState.WaitingForWindow)
                {
                    State = HolderState.WaitingForWindow;
                    StandingBy = false;
                    ApplyAwake(false);
                    Log(now, "PAUSE", Loc.T("log.pause_schedule"));
                }
                return;
            }

            if (State != HolderState.Active)
            {
                bool resumed = State == HolderState.WaitingForWindow;
                State = HolderState.Active;
                _nextActivity = now;
                if (resumed) Log(now, "RESUME", Loc.T("log.resume_schedule"));
            }

            ApplyAwake(_settings.KeepAwake);

            if (now < _nextActivity) return;

            int interval = _settings.IntervalSeconds;
            if (_settings.SmartIdle)
            {
                TimeSpan idle = _input.GetIdleTime();
                LastIdle = idle;
                if (idle < TimeSpan.FromSeconds(interval))
                {
                    // Der Nutzer ist selbst aktiv: nichts erzeugen, naechsten Takt wieder pruefen.
                    StandingBy = true;
                    ConsecutiveFailures = 0;
                    return;
                }
            }

            StandingBy = false;
            if (_input.SendActivity(_settings.Mode, _settings.MousePixels))
            {
                LastActivity = now;
                ActivityCount++;
                ConsecutiveFailures = 0;
                if (_settings.KeepAwake) _input.SetKeepAwake(true); // idempotent, sichert gegen Zuruecksetzen ab
            }
            else
            {
                ConsecutiveFailures++;
            }
            _nextActivity = now.AddSeconds(interval);
        }

        void ApplyAwake(bool on)
        {
            if (on == _awakeApplied) return;
            _awakeApplied = on;
            _input.SetKeepAwake(on);
        }

        void Log(DateTime now, string kind, string message)
        {
            if (_log != null) _log(now, kind, message);
        }
    }

    /// <summary>Kleine Formatierhelfer.</summary>
    public static class Format
    {
        /// <summary>hh:mm:ss, Stunden ggf. dreistellig (kein Tagesueberlauf).</summary>
        public static string Duration(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                (int)t.TotalHours, t.Minutes, t.Seconds);
        }
    }
}
