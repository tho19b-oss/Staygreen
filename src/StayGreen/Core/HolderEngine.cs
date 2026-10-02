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

        /// <summary>Vom Nutzer fuer eine Weile angehalten; laeuft danach von selbst weiter.</summary>
        Paused,

        /// <summary>Gestartet, aber Teams laeuft nicht (nur mit der Option "nur wenn Teams laeuft").</summary>
        WaitingForTeams,
    }

    /// <summary>
    /// Das Herzstueck: entscheidet bei jedem Takt, ob jetzt eine Eingabe erzeugt werden soll.
    /// Kennt weder Fenster noch Windows-API, nur <see cref="IInputBackend"/>. Die Zeit wird von aussen
    /// uebergeben, dadurch laesst sich alles ohne Warten testen.
    /// </summary>
    public sealed class HolderEngine
    {
        /// <summary>So viele fehlgeschlagene Eingaben in Folge gelten als "blockiert".</summary>
        public const int BlockedThreshold = 2;

        /// <summary>So oft wird (hoechstens) nachgesehen, ob Teams laeuft; das Auflisten der Prozesse kostet etwas.</summary>
        public static readonly TimeSpan TeamsCheckInterval = TimeSpan.FromSeconds(5);

        readonly IInputBackend _input;
        readonly Settings _settings;
        readonly Action<DateTime, string, string> _log;
        readonly ITeamsProbe _teams;

        bool _running;
        bool _awakeApplied;
        DateTime _nextActivity;
        DateTime? _pausedUntil;
        DateTime? _activeSince;
        DateTime _teamsCheckedAt = DateTime.MinValue;
        bool _teamsRunning = true;

        public HolderEngine(IInputBackend input, Settings settings, Action<DateTime, string, string> log,
            ITeamsProbe teams = null)
        {
            if (input == null) throw new ArgumentNullException("input");
            if (settings == null) throw new ArgumentNullException("settings");
            _input = input;
            _settings = settings;
            _log = log;
            _teams = teams;
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

        /// <summary>Wann die Pause endet (nur im Zustand <see cref="HolderState.Paused"/>).</summary>
        public DateTime? PausedUntil
        {
            get { return _running && State == HolderState.Paused ? _pausedUntil : null; }
        }

        /// <summary>True, wenn der letzte Stopp von der maximalen Laufzeit ausging (nicht vom Nutzer).</summary>
        public bool StoppedAutomatically { get; private set; }

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
            StoppedAutomatically = false;
            _pausedUntil = null;
            _activeSince = now;
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
            _pausedUntil = null;
            _activeSince = null;
            ApplyAwake(false);
            Log(now, "STOP", string.IsNullOrEmpty(reason)
                ? Loc.T("log.runtime", Format.Duration(runtime))
                : reason + " - " + Loc.T("log.runtime", Format.Duration(runtime)));
        }

        /// <summary>
        /// Haelt das Aktivhalten fuer die angegebene Dauer an (keine Eingabe, kein Wach-Halten); danach geht es von
        /// selbst weiter. Wirkt nur, solange gestartet ist.
        /// </summary>
        public void Pause(DateTime now, TimeSpan duration, string reason)
        {
            if (!_running) return;
            if (duration <= TimeSpan.Zero)
            {
                Resume(now);
                return;
            }
            _pausedUntil = now + duration;
            Log(now, "PAUSE", Loc.T("log.pause_manual", ScheduleRule.FormatTime(_pausedUntil.Value.TimeOfDay), reason));
            Tick(now);
        }

        /// <summary>Beendet eine Pause sofort.</summary>
        public void Resume(DateTime now)
        {
            if (!_running || !_pausedUntil.HasValue) return;
            _pausedUntil = null;
            Tick(now);
        }

        /// <summary>Wird einmal pro Sekunde aufgerufen.</summary>
        public void Tick(DateTime now)
        {
            if (!_running) return;

            // Sicherheitsnetz: nach der eingestellten Dauer am Stueck von selbst stoppen.
            if (_settings.MaxRuntimeHours > 0 && _activeSince.HasValue
                && now - _activeSince.Value >= TimeSpan.FromHours(_settings.MaxRuntimeHours))
            {
                StoppedAutomatically = true;
                Stop(now, Loc.T("reason.maxruntime", Format.Duration(now - _activeSince.Value)));
                return;
            }

            HolderState wanted = Decide(now);
            if (wanted != HolderState.Active)
            {
                if (State != wanted) EnterWaiting(wanted, now);
                return;
            }

            if (State != HolderState.Active)
            {
                HolderState from = State;
                State = HolderState.Active;
                _nextActivity = now;
                _activeSince = now;
                Log(now, "RESUME", Loc.T(ResumeKey(from)));
            }

            ApplyAwake(_settings.KeepAwake);

            int interval = _settings.IntervalSeconds;

            // Uhr zurueckgestellt (Winterzeit, manuelle Korrektur) oder Intervall verkuerzt: Der geplante Zeitpunkt
            // liegt dann weiter als ein Intervall in der Zukunft. Sofort handeln statt bis zu eine Stunde zu warten.
            if (_nextActivity > now.AddSeconds(interval + 1)) _nextActivity = now;

            if (now < _nextActivity) return;

            if (_settings.SmartIdle)
            {
                TimeSpan idle = _input.GetIdleTime();
                LastIdle = idle;
                if (idle < TimeSpan.FromSeconds(interval))
                {
                    // Der Nutzer ist selbst aktiv: nichts erzeugen, naechsten Takt wieder pruefen.
                    StandingBy = true;
                    NoteInputAccepted(now);
                    return;
                }
            }

            StandingBy = false;
            if (_input.SendActivity(_settings.Mode, _settings.MousePixels, _settings.InputKey))
            {
                LastActivity = now;
                ActivityCount++;
                NoteInputAccepted(now);
                if (_settings.KeepAwake) _input.SetKeepAwake(true); // idempotent, sichert gegen Zuruecksetzen ab
            }
            else
            {
                ConsecutiveFailures++;
                if (ConsecutiveFailures == BlockedThreshold) Log(now, "BLOCKED", Loc.T("log.blocked"));
            }
            _nextActivity = now.AddSeconds(interval);
        }

        /// <summary>Entscheidet, in welchem Zustand die Engine jetzt sein soll (Reihenfolge: Pause, Zeitplan, Teams).</summary>
        HolderState Decide(DateTime now)
        {
            if (_pausedUntil.HasValue)
            {
                if (now < _pausedUntil.Value) return HolderState.Paused;
                _pausedUntil = null;
            }

            // Ausserhalb der Zeitfenster (oder an einem Ausnahmetag): nichts tun und den PC nicht wach halten.
            if (_settings.ScheduleActive && !Scheduler.IsActive(now, _settings.Rules, _settings.Exceptions))
                return HolderState.WaitingForWindow;

            if (_settings.OnlyWhileTeamsRuns && !TeamsRunning(now))
                return HolderState.WaitingForTeams;

            return HolderState.Active;
        }

        void EnterWaiting(HolderState state, DateTime now)
        {
            State = state;
            StandingBy = false;
            _activeSince = null;
            ApplyAwake(false);

            // Die manuelle Pause wurde schon in Pause() protokolliert (mit Grund und Ende).
            if (state == HolderState.WaitingForWindow) Log(now, "PAUSE", Loc.T("log.pause_schedule"));
            else if (state == HolderState.WaitingForTeams) Log(now, "PAUSE", Loc.T("log.pause_teams"));
        }

        static string ResumeKey(HolderState from)
        {
            switch (from)
            {
                case HolderState.Paused: return "log.resume_pause";
                case HolderState.WaitingForTeams: return "log.resume_teams";
                default: return "log.resume_schedule";
            }
        }

        bool TeamsRunning(DateTime now)
        {
            if (_teams == null) return true;
            if (now - _teamsCheckedAt >= TeamsCheckInterval || now < _teamsCheckedAt)
            {
                _teamsRunning = _teams.IsTeamsRunning();
                _teamsCheckedAt = now;
            }
            return _teamsRunning;
        }

        /// <summary>Die Eingabe kam an (oder der Nutzer ist selbst aktiv): Fehlerzaehler zuruecksetzen.</summary>
        void NoteInputAccepted(DateTime now)
        {
            if (ConsecutiveFailures >= BlockedThreshold) Log(now, "UNBLOCKED", Loc.T("log.unblocked"));
            ConsecutiveFailures = 0;
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
