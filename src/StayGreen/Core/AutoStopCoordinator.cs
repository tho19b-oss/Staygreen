using System;

namespace StayGreen.Core
{
    /// <summary>Was der Nutzer in der Vorwarnung gewaehlt hat.</summary>
    public enum AutoStopChoice
    {
        /// <summary>Nichts angeklickt, der Countdown ist abgelaufen: ausfuehren.</summary>
        Timeout,

        /// <summary>"Jetzt ausfuehren".</summary>
        RunNow,

        /// <summary>Um die eingestellten Minuten verschieben.</summary>
        Snooze,

        /// <summary>Diesen Termin ausfallen lassen.</summary>
        Cancel,
    }

    /// <summary>Zeigt die Vorwarnung. Kehrt zurueck, wenn der Nutzer gewaehlt hat oder der Countdown abgelaufen ist.</summary>
    public interface IAutoStopPrompt
    {
        /// <param name="due">Zeitpunkt, zu dem der Auto-Stopp ausgefuehrt wird.</param>
        /// <param name="actions">Lesbare Liste der Aktionen (fuer den Dialogtext).</param>
        AutoStopChoice Ask(DateTime due, string actions);
    }

    public enum AutoStopResult
    {
        /// <summary>Nichts zu tun.</summary>
        None,

        /// <summary>Jetzt ausfuehren (der naechste Termin ist schon festgelegt).</summary>
        Execute,

        /// <summary>Der Termin lag zu lange zurueck (Standby) und wurde uebersprungen.</summary>
        Missed,

        /// <summary>Der Nutzer hat in der Vorwarnung abgebrochen.</summary>
        Cancelled,

        /// <summary>Der Nutzer hat in der Vorwarnung verschoben.</summary>
        Snoozed,
    }

    /// <summary>
    /// Der Ablauf des Auto-Stopps ohne Oberflaeche: Termin merken, kurz vorher warnen, auf die Wahl des Nutzers
    /// reagieren (abbrechen, verschieben, sofort), verpasste Termine nicht nachholen. Die Ausfuehrung selbst
    /// (Aktivhalten stoppen, <see cref="StopExecutor"/>) macht der Aufrufer, wenn <see cref="AutoStopResult.Execute"/>
    /// zurueckkommt. Der naechste Termin ist dann bereits festgelegt, ein erneuter Takt loest also nichts doppelt aus.
    /// </summary>
    public sealed class AutoStopCoordinator
    {
        readonly Settings _settings;
        readonly IAutoStopPrompt _prompt;
        readonly Func<DateTime> _clock;

        DateTime? _due;
        DateTime? _snoozedTo;
        bool _prompted;
        bool _busy;

        public AutoStopCoordinator(Settings settings, IAutoStopPrompt prompt, Func<DateTime> clock)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _settings = settings;
            _prompt = prompt;
            _clock = clock ?? (() => DateTime.Now);
        }

        /// <summary>Naechster Termin (null, wenn keiner geplant ist).</summary>
        public DateTime? Due
        {
            get { return _due; }
        }

        /// <summary>
        /// True, wenn der letzte <see cref="Tick"/> einen einmaligen Termin verbraucht und den Auto-Stopp in den
        /// Einstellungen ausgeschaltet hat. Der Aufrufer sollte dann speichern und die Oberflaeche aktualisieren.
        /// </summary>
        public bool SettingsChanged { get; private set; }

        /// <summary>Der Termin, auf den zuletzt verschoben wurde (fuer das Protokoll).</summary>
        public DateTime? LastSnoozedTo { get; private set; }

        /// <summary>Plant neu (nach geaenderten Einstellungen). Ein verschobener Termin bleibt erhalten.</summary>
        public void Reschedule(DateTime now)
        {
            if (_snoozedTo.HasValue && _snoozedTo.Value > now && _settings.AutoStopEnabled)
            {
                _due = _snoozedTo;
                return;
            }
            _snoozedTo = null;
            _due = AutoStopPlanner.NextDue(_settings, now);
            _prompted = false;
        }

        /// <summary>Einmal pro Sekunde aufrufen. Kann kurz blockieren, solange die Vorwarnung offen ist.</summary>
        public AutoStopResult Tick(DateTime now)
        {
            // Waehrend die Vorwarnung offen ist, laeuft der Takt der Oberflaeche weiter: nicht noch einmal eintreten.
            if (_busy) return AutoStopResult.None;
            _busy = true;
            try
            {
                return TickCore(now);
            }
            finally
            {
                _busy = false;
            }
        }

        AutoStopResult TickCore(DateTime now)
        {
            SettingsChanged = false;
            if (!_due.HasValue) return AutoStopResult.None;

            DateTime due = _due.Value;
            StopPlan plan = StopPlan.FromSettings(_settings);
            bool run = false;
            DateTime when = now;

            // Vorwarnung: kurz vor dem Termin, aber nicht, wenn er schon laenger verpasst ist.
            if (plan.NeedsWarning && !_prompted
                && now >= due.AddSeconds(-_settings.AutoStopWarnSeconds)
                && now < due.AddSeconds(AutoStopPlanner.MissedToleranceSeconds))
            {
                _prompted = true;

                // Knapp verpasst (z. B. Laptop kurz nach dem Termin aufgeklappt): trotzdem einen vollen Countdown geben.
                DateTime shownDue = now > due ? now.AddSeconds(_settings.AutoStopWarnSeconds) : due;
                AutoStopChoice choice = _prompt == null ? AutoStopChoice.Timeout : _prompt.Ask(shownDue, plan.Describe());
                when = _clock();
                if (when < now) when = now;

                if (choice == AutoStopChoice.Cancel) return Complete(when, due, AutoStopResult.Cancelled);

                if (choice == AutoStopChoice.Snooze)
                {
                    DateTime from = when > due ? when : due;
                    _snoozedTo = from.AddMinutes(_settings.AutoStopSnoozeMinutes);
                    LastSnoozedTo = _snoozedTo;
                    _due = _snoozedTo;
                    _prompted = false;
                    return AutoStopResult.Snoozed;
                }

                run = true;   // Timeout oder "Jetzt ausfuehren"
            }

            if (!run)
            {
                if (now < due) return AutoStopResult.None;
                if (AutoStopPlanner.IsMissed(due, now)) return Complete(now, due, AutoStopResult.Missed);
            }

            return Complete(when, due, AutoStopResult.Execute);
        }

        /// <summary>
        /// Schliesst den Termin ab und plant den naechsten. Ein einmaliger Termin schaltet sich selbst aus.
        /// Neu geplant wird ab dem spaeteren von "jetzt" und dem Termin: Wurde vorzeitig ausgefuehrt, darf
        /// derselbe Termin nicht noch einmal faellig werden.
        /// </summary>
        AutoStopResult Complete(DateTime now, DateTime due, AutoStopResult result)
        {
            if (_settings.AutoStopTiming == StopTiming.Once)
            {
                _settings.AutoStopEnabled = false;
                SettingsChanged = true;
            }
            _snoozedTo = null;
            Reschedule(now > due ? now : due);
            return result;
        }
    }
}
