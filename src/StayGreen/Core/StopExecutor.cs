using System;
using System.Collections.Generic;

namespace StayGreen.Core
{
    /// <summary>Was beim Auto-Stopp zusaetzlich zum Beenden des Aktivhaltens passieren soll.</summary>
    public sealed class StopPlan
    {
        public bool CloseTeams { get; set; }
        public bool Lock { get; set; }
        public bool Shutdown { get; set; }
        public bool ExitApp { get; set; }

        /// <summary>
        /// Teams beenden, Sperren und Herunterfahren stoeren die Arbeit und brauchen deshalb eine Vorwarnung.
        /// Nur das Aktivhalten zu stoppen oder StayGreen zu beenden ist harmlos.
        /// </summary>
        public bool NeedsWarning
        {
            get { return CloseTeams || Lock || Shutdown; }
        }

        public static StopPlan FromSettings(Settings s)
        {
            return new StopPlan
            {
                CloseTeams = s.StopCloseTeams,
                Lock = s.StopLock,
                Shutdown = s.StopShutdown,
                ExitApp = s.StopExitApp,
            };
        }

        /// <summary>Lesbare Aufzaehlung der Aktionen ("Teams beenden, Windows sperren").</summary>
        public string Describe()
        {
            var parts = new List<string>();
            if (CloseTeams) parts.Add(Loc.T("plan.action.teams"));
            if (Lock) parts.Add(Loc.T("plan.action.lock"));
            if (Shutdown) parts.Add(Loc.T("plan.action.shutdown"));
            if (ExitApp) parts.Add(Loc.T("plan.action.exit"));
            return parts.Count == 0 ? Loc.T("plan.action.none") : string.Join(", ", parts);
        }
    }

    public sealed class StopOutcome
    {
        public bool TeamsClosed { get; set; }
        public bool ShutdownStarted { get; set; }
        public bool Locked { get; set; }
        public bool ExitRequested { get; set; }
    }

    /// <summary>
    /// Fuehrt die Auto-Stopp-Aktionen in fester, sicherer Reihenfolge aus. Die Vorwarnung (abbrechen, verschieben)
    /// gibt es schon vorher im <see cref="AutoStopCoordinator"/>; hier wird nur noch ausgefuehrt.
    /// </summary>
    public static class StopExecutor
    {
        /// <param name="plan">Gewuenschte Aktionen.</param>
        /// <param name="actions">Systemzugriff.</param>
        public static StopOutcome Execute(StopPlan plan, ISystemActions actions)
        {
            var outcome = new StopOutcome();

            // 1. Teams schliessen (danach zeigt der Status "Offline" statt "Abwesend").
            if (plan.CloseTeams)
            {
                actions.CloseTeams();
                outcome.TeamsClosed = true;
            }

            // 2. Herunterfahren (ohne Zwang, ungespeicherte Programme halten es auf).
            if (plan.Shutdown)
            {
                actions.Shutdown();
                outcome.ShutdownStarted = true;
            }

            // 3. Sperren: nur sinnvoll, wenn der Rechner nicht ohnehin herunterfaehrt.
            if (plan.Lock && !outcome.ShutdownStarted)
            {
                actions.LockWorkstation();
                outcome.Locked = true;
            }

            outcome.ExitRequested = plan.ExitApp;
            return outcome;
        }
    }
}
