using System;
using System.Collections.Generic;
using StayGreen.Core;

namespace StayGreen.Tests
{
    /// <summary>Simuliertes Betriebssystem: zaehlt Eingaben, Leerlauf ist frei einstellbar.</summary>
    sealed class FakeInput : IInputBackend
    {
        public int Sent;
        public ActivityMode LastMode;
        public int LastPixels;
        public ActivityKey LastKey;
        public TimeSpan Idle = TimeSpan.FromHours(1);
        public bool Accept = true;
        public readonly List<bool> AwakeCalls = new List<bool>();

        public bool Awake
        {
            get { return AwakeCalls.Count > 0 && AwakeCalls[AwakeCalls.Count - 1]; }
        }

        public bool SendActivity(ActivityMode mode, int mousePixels, ActivityKey key)
        {
            if (!Accept) return false;
            Sent++;
            LastMode = mode;
            LastPixels = mousePixels;
            LastKey = key;
            Idle = TimeSpan.Zero; // wie in echt: erzeugte Eingaben setzen den Leerlauf zurueck
            return true;
        }

        public TimeSpan GetIdleTime()
        {
            return Idle;
        }

        public void SetKeepAwake(bool keepAwake)
        {
            AwakeCalls.Add(keepAwake);
        }
    }

    sealed class FakeActions : ISystemActions
    {
        public readonly List<string> Calls = new List<string>();

        public void CloseTeams()
        {
            Calls.Add("teams");
        }

        public void LockWorkstation()
        {
            Calls.Add("lock");
        }

        public void Shutdown()
        {
            Calls.Add("shutdown");
        }
    }

    /// <summary>Teams laeuft oder nicht; zaehlt die Abfragen (der Takt darf nicht jede Sekunde nachsehen).</summary>
    sealed class FakeTeams : ITeamsProbe
    {
        public bool Running = true;
        public int Calls;

        public bool IsTeamsRunning()
        {
            Calls++;
            return Running;
        }
    }

    /// <summary>Spielt die Antworten des Nutzers auf die Vorwarnung nach und merkt sich, was gefragt wurde.</summary>
    sealed class FakePrompt : IAutoStopPrompt
    {
        public readonly Queue<AutoStopChoice> Answers = new Queue<AutoStopChoice>();
        public readonly List<DateTime> AskedDue = new List<DateTime>();
        public readonly List<string> AskedActions = new List<string>();

        /// <summary>Wird bei jeder Frage aufgerufen; damit laesst sich die Uhr voranstellen, als haette der Dialog gewartet.</summary>
        public Action OnAsk;

        public int Asked
        {
            get { return AskedDue.Count; }
        }

        public AutoStopChoice Ask(DateTime due, string actions)
        {
            AskedDue.Add(due);
            AskedActions.Add(actions);
            if (OnAsk != null) OnAsk();
            return Answers.Count > 0 ? Answers.Dequeue() : AutoStopChoice.Timeout;
        }
    }

    static class T
    {
        /// <summary>Montag, 5. Oktober 2026 (Wochentag wird geprueft, damit kein Test still falsch liegt).</summary>
        public static DateTime Mon(int hour = 0, int minute = 0, int second = 0)
        {
            var d = new DateTime(2026, 10, 5, hour, minute, second);
            if (d.DayOfWeek != DayOfWeek.Monday) throw new InvalidOperationException("Testdatum ist kein Montag");
            return d;
        }

        public static DateTime Day(int offsetFromMonday, int hour = 0, int minute = 0, int second = 0)
        {
            return Mon(hour, minute, second).AddDays(offsetFromMonday);
        }

        public static TimeSpan Hm(int h, int m = 0)
        {
            return new TimeSpan(h, m, 0);
        }
    }
}
