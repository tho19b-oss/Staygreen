using System;

namespace StayGreen.Core
{
    /// <summary>Was beim Auto-Stopp zusaetzlich zum Beenden des Aktivhaltens passieren soll.</summary>
    public sealed class StopPlan
    {
        public bool CloseTeams { get; set; }
        public bool Lock { get; set; }
        public bool Shutdown { get; set; }
        public bool ExitApp { get; set; }

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
    }

    public sealed class StopOutcome
    {
        public bool TeamsClosed { get; set; }
        public bool ShutdownStarted { get; set; }
        public bool ShutdownCancelled { get; set; }
        public bool Locked { get; set; }
        public bool ExitRequested { get; set; }
    }

    /// <summary>Fuehrt die Auto-Stopp-Aktionen in fester, sicherer Reihenfolge aus.</summary>
    public static class StopExecutor
    {
        /// <param name="plan">Gewuenschte Aktionen.</param>
        /// <param name="actions">Systemzugriff.</param>
        /// <param name="confirmShutdown">
        /// Zeigt den abbrechbaren Countdown. True = herunterfahren, False = abgebrochen. Null = ohne Rueckfrage.
        /// </param>
        public static StopOutcome Execute(StopPlan plan, ISystemActions actions, Func<bool> confirmShutdown)
        {
            var outcome = new StopOutcome();

            // 1. Teams schliessen (danach zeigt der Status "Offline" statt "Abwesend").
            if (plan.CloseTeams)
            {
                actions.CloseTeams();
                outcome.TeamsClosed = true;
            }

            // 2. Herunterfahren, mit Countdown und Abbruchmoeglichkeit.
            if (plan.Shutdown)
            {
                if (confirmShutdown == null || confirmShutdown())
                {
                    actions.Shutdown();
                    outcome.ShutdownStarted = true;
                }
                else
                {
                    outcome.ShutdownCancelled = true;
                }
            }

            // 3. Sperren: nur sinnvoll, wenn der Rechner nicht ohnehin herunterfaehrt.
            //    Wurde das Herunterfahren abgebrochen, wird trotzdem gesperrt (sicherer Standard).
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
