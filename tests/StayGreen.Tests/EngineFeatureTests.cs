using System;
using System.Collections.Generic;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    public class EngineFeatureTests : IDisposable
    {
        sealed class Rig
        {
            public readonly Settings Settings;
            public readonly FakeInput Input = new FakeInput();
            public readonly FakeTeams Teams = new FakeTeams();
            public readonly List<string> Log = new List<string>();
            public readonly HolderEngine Engine;

            public Rig(Action<Settings> configure = null, bool withTeamsProbe = true)
            {
                Settings = new Settings { SmartIdle = false, IntervalSeconds = 30, KeepAwake = true };
                if (configure != null) configure(Settings);
                Engine = new HolderEngine(Input, Settings, (t, kind, msg) => Log.Add(kind + ": " + msg),
                    withTeamsProbe ? Teams : null);
            }

            public bool Logged(string kind)
            {
                return Log.Exists(l => l.StartsWith(kind + ":"));
            }

            public int Count(string kind)
            {
                return Log.FindAll(l => l.StartsWith(kind + ":")).Count;
            }
        }

        public EngineFeatureTests()
        {
            Loc.Language = "de";
        }

        public void Dispose()
        {
            Loc.Language = "de";
        }

        // ---- Pause ----

        [Fact]
        public void Pause_StopsInputAndKeepAwake_AndResumesByItself()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "Test");
            Assert.Equal(1, r.Input.Sent);

            r.Engine.Pause(T.Mon(10, 0, 5), TimeSpan.FromMinutes(30), "Tray-Menü");
            Assert.Equal(HolderState.Paused, r.Engine.State);
            Assert.Equal(T.Mon(10, 30, 5), r.Engine.PausedUntil);
            Assert.False(r.Input.Awake);

            for (int minute = 1; minute < 30; minute++) r.Engine.Tick(T.Mon(10, minute, 5));
            r.Engine.Tick(T.Mon(10, 30, 4));
            Assert.Equal(1, r.Input.Sent);
            Assert.Equal(HolderState.Paused, r.Engine.State);

            r.Engine.Tick(T.Mon(10, 30, 5));
            Assert.Equal(HolderState.Active, r.Engine.State);
            Assert.Equal(2, r.Input.Sent);
            Assert.True(r.Input.Awake);
            Assert.Null(r.Engine.PausedUntil);
            Assert.Contains("Pause beendet", r.Log.Find(l => l.StartsWith("RESUME")));
        }

        [Fact]
        public void Pause_IsLoggedWithEndAndReason()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Pause(T.Mon(10, 0, 5), TimeSpan.FromMinutes(30), "Tray-Menü");
            Assert.Equal("PAUSE: Pausiert bis 10:30 (Tray-Menü)", r.Log.Find(l => l.StartsWith("PAUSE")));
            Assert.Equal(1, r.Count("PAUSE"));
        }

        [Fact]
        public void Resume_EndsThePauseImmediately()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Pause(T.Mon(10, 0, 5), TimeSpan.FromHours(2), "x");
            r.Engine.Resume(T.Mon(10, 5));
            Assert.Equal(HolderState.Active, r.Engine.State);
            Assert.Equal(2, r.Input.Sent);
        }

        [Fact]
        public void Pause_WhenNotRunning_IsIgnored()
        {
            var r = new Rig();
            r.Engine.Pause(T.Mon(10), TimeSpan.FromMinutes(5), "x");
            Assert.Equal(HolderState.Stopped, r.Engine.State);
            Assert.Empty(r.Log);
            r.Engine.Resume(T.Mon(10));          // darf nichts tun
            Assert.Equal(HolderState.Stopped, r.Engine.State);
        }

        [Fact]
        public void Pause_WithZeroDuration_ActsLikeResume()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Pause(T.Mon(10, 0, 5), TimeSpan.FromHours(1), "x");
            r.Engine.Pause(T.Mon(10, 1), TimeSpan.Zero, "x");
            Assert.Equal(HolderState.Active, r.Engine.State);
        }

        [Fact]
        public void Stop_ClearsAPause_AndRestartIsNotPaused()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Pause(T.Mon(10, 0, 5), TimeSpan.FromHours(1), "x");
            r.Engine.Stop(T.Mon(10, 10), "x");
            Assert.Null(r.Engine.PausedUntil);
            r.Engine.Start(T.Mon(10, 11), "x");
            Assert.Equal(HolderState.Active, r.Engine.State);
        }

        [Fact]
        public void Pause_WinsOverTheSchedule_AndTheScheduleTakesOverAfterwards()
        {
            var r = new Rig(s =>
            {
                s.ScheduleEnabled = true;
                s.Rules.Add(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
            });
            r.Engine.Start(T.Mon(16, 50), "Test");
            r.Engine.Pause(T.Mon(16, 51), TimeSpan.FromMinutes(20), "x");
            Assert.Equal(HolderState.Paused, r.Engine.State);

            r.Engine.Tick(T.Mon(17, 11));            // Pause vorbei, Fenster aber auch
            Assert.Equal(HolderState.WaitingForWindow, r.Engine.State);
            Assert.True(r.Logged("PAUSE"));
        }

        // ---- Nur aktiv, solange Teams laeuft ----

        [Fact]
        public void Teams_NotRunning_WaitsWithoutInputOrKeepAwake_AndResumesWhenItStarts()
        {
            var r = new Rig(s => s.OnlyWhileTeamsRuns = true);
            r.Teams.Running = false;
            r.Engine.Start(T.Mon(10), "Test");
            Assert.Equal(HolderState.WaitingForTeams, r.Engine.State);
            Assert.Equal(0, r.Input.Sent);
            Assert.False(r.Input.Awake);
            Assert.Contains("Teams läuft nicht", r.Log.Find(l => l.StartsWith("PAUSE")));

            r.Teams.Running = true;
            r.Engine.Tick(T.Mon(10, 0, 3));              // noch innerhalb der Pruefpause: es bleibt beim alten Ergebnis
            Assert.Equal(HolderState.WaitingForTeams, r.Engine.State);
            r.Engine.Tick(T.Mon(10, 0, 5));
            Assert.Equal(HolderState.Active, r.Engine.State);
            Assert.Equal(1, r.Input.Sent);
            Assert.Contains("Teams läuft, aktiv", r.Log.Find(l => l.StartsWith("RESUME")));
        }

        [Fact]
        public void Teams_Check_IsThrottled()
        {
            var r = new Rig(s => s.OnlyWhileTeamsRuns = true);
            r.Engine.Start(T.Mon(10), "Test");
            for (int s = 1; s <= 20; s++) r.Engine.Tick(T.Mon(10, 0, s));
            Assert.InRange(r.Teams.Calls, 1, 5);          // hoechstens alle 5 Sekunden
        }

        [Fact]
        public void Teams_OptionOff_NeverLooksAtProcesses()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Tick(T.Mon(10, 1));
            Assert.Equal(0, r.Teams.Calls);
        }

        [Fact]
        public void Teams_WithoutAProbe_CountsAsRunning()
        {
            var r = new Rig(s => s.OnlyWhileTeamsRuns = true, withTeamsProbe: false);
            r.Engine.Start(T.Mon(10), "Test");
            Assert.Equal(HolderState.Active, r.Engine.State);
        }

        [Fact]
        public void Teams_OptionSwitchedOffWhileWaiting_ResumesAtOnce()
        {
            var r = new Rig(s => s.OnlyWhileTeamsRuns = true);
            r.Teams.Running = false;
            r.Engine.Start(T.Mon(10), "Test");
            Assert.Equal(HolderState.WaitingForTeams, r.Engine.State);
            r.Settings.OnlyWhileTeamsRuns = false;
            r.Engine.Tick(T.Mon(10, 0, 1));
            Assert.Equal(HolderState.Active, r.Engine.State);
        }

        // ---- Maximale Laufzeit ----

        [Fact]
        public void MaxRuntime_StopsAfterTheConfiguredHours()
        {
            var r = new Rig(s => s.MaxRuntimeHours = 2);
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Tick(T.Mon(11, 59, 59));
            Assert.True(r.Engine.Running);
            Assert.False(r.Engine.StoppedAutomatically);

            r.Engine.Tick(T.Mon(12));
            Assert.False(r.Engine.Running);
            Assert.True(r.Engine.StoppedAutomatically);
            Assert.False(r.Input.Awake);
            string stop = r.Log.Find(l => l.StartsWith("STOP"));
            Assert.Contains("Maximale Laufzeit erreicht (02:00:00)", stop);
        }

        [Fact]
        public void MaxRuntime_Zero_IsOff()
        {
            var r = new Rig(s => s.MaxRuntimeHours = 0);
            r.Engine.Start(T.Mon(0), "Test");
            r.Engine.Tick(T.Day(10, 12));
            Assert.True(r.Engine.Running);
        }

        [Fact]
        public void MaxRuntime_CountsOnlyActiveTime_NotWaitingForTheSchedule()
        {
            var r = new Rig(s =>
            {
                s.MaxRuntimeHours = 10;
                s.ScheduleEnabled = true;
                s.Rules.Add(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
            });
            r.Engine.Start(T.Mon(7), "Test");                 // wartet auf 08:00
            for (int hour = 8; hour < 24; hour++) r.Engine.Tick(T.Mon(hour));
            for (int day = 1; day <= 3; day++)
                for (int hour = 0; hour < 24; hour++) r.Engine.Tick(T.Day(day, hour));
            Assert.True(r.Engine.Running);                   // jeder Tag hat nur 9 aktive Stunden
            Assert.False(r.Engine.StoppedAutomatically);
        }

        [Fact]
        public void MaxRuntime_ResetsAfterAPause()
        {
            var r = new Rig(s => s.MaxRuntimeHours = 2);
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Pause(T.Mon(11, 30), TimeSpan.FromMinutes(30), "x");   // nach 1,5 h Pause
            r.Engine.Tick(T.Mon(12));                                       // wieder aktiv, Zaehler beginnt neu
            r.Engine.Tick(T.Mon(13, 59));
            Assert.True(r.Engine.Running);
            r.Engine.Tick(T.Mon(14));
            Assert.False(r.Engine.Running);
        }

        [Fact]
        public void MaxRuntime_StartClearsTheAutomaticFlag()
        {
            var r = new Rig(s => s.MaxRuntimeHours = 1);
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Tick(T.Mon(11));
            Assert.True(r.Engine.StoppedAutomatically);
            r.Settings.MaxRuntimeHours = 0;
            r.Engine.Start(T.Mon(11, 5), "Test");
            Assert.False(r.Engine.StoppedAutomatically);
        }

        // ---- Taste ----

        [Fact]
        public void InputKey_IsPassedToTheBackend()
        {
            var r = new Rig(s => s.InputKey = ActivityKey.Shift);
            r.Engine.Start(T.Mon(10), "Test");
            Assert.Equal(ActivityKey.Shift, r.Input.LastKey);
        }

        // ---- Blockiert ----

        [Fact]
        public void Blocked_IsLoggedOnce_AndTheRecoveryToo()
        {
            var r = new Rig();
            r.Input.Accept = false;
            DateTime t0 = T.Mon(10);
            r.Engine.Start(t0, "Test");                          // 1. Fehlschlag
            Assert.Equal(0, r.Count("BLOCKED"));
            r.Engine.Tick(t0.AddSeconds(30));                    // 2. Fehlschlag: jetzt gilt es als blockiert
            r.Engine.Tick(t0.AddSeconds(60));                    // 3.
            Assert.Equal(1, r.Count("BLOCKED"));

            r.Input.Accept = true;
            r.Engine.Tick(t0.AddSeconds(90));
            Assert.Equal(1, r.Count("UNBLOCKED"));
            r.Engine.Tick(t0.AddSeconds(120));
            Assert.Equal(1, r.Count("UNBLOCKED"));
            Assert.Equal(1, r.Count("BLOCKED"));
        }

        [Fact]
        public void Blocked_EndsAlsoWhenTheUserBecomesActiveHimself()
        {
            var r = new Rig(s => s.SmartIdle = true);
            r.Input.Accept = false;
            DateTime t0 = T.Mon(10);
            r.Engine.Start(t0, "Test");
            r.Engine.Tick(t0.AddSeconds(30));
            Assert.Equal(1, r.Count("BLOCKED"));

            r.Input.Idle = TimeSpan.FromSeconds(2);              // Nutzer tippt selbst (Sitzung wieder offen)
            r.Engine.Tick(t0.AddSeconds(60));
            Assert.Equal(0, r.Engine.ConsecutiveFailures);
            Assert.Equal(1, r.Count("UNBLOCKED"));
        }

        // ---- Status ----

        [Fact]
        public void Status_Paused_ShowsWhenItContinues()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Pause(T.Mon(10, 0, 5), TimeSpan.FromMinutes(30), "x");
            StatusInfo info = StatusBuilder.Build(r.Engine, r.Settings, T.Mon(10, 1), false);
            Assert.Equal(StatusKind.Paused, info.Kind);
            Assert.Equal("Pausiert", info.Title);
            Assert.Equal("Läuft automatisch weiter: heute 10:30", info.Detail);
        }

        [Fact]
        public void Status_WaitingForTeams_ExplainsWhat()
        {
            var r = new Rig(s => s.OnlyWhileTeamsRuns = true);
            r.Teams.Running = false;
            r.Engine.Start(T.Mon(10), "Test");
            StatusInfo info = StatusBuilder.Build(r.Engine, r.Settings, T.Mon(10), false);
            Assert.Equal(StatusKind.WaitingForTeams, info.Kind);
            Assert.Contains("Teams", info.Title);
        }

        [Fact]
        public void Status_LockedSession_StillWinsOverPause()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Pause(T.Mon(10, 0, 5), TimeSpan.FromMinutes(30), "x");
            Assert.Equal(StatusKind.SessionLocked, StatusBuilder.Build(r.Engine, r.Settings, T.Mon(10, 1), true).Kind);
        }

        [Theory]
        [InlineData(15, "15 Minuten")]
        [InlineData(30, "30 Minuten")]
        [InlineData(60, "1 Stunde")]
        [InlineData(120, "2 Stunden")]
        [InlineData(90, "90 Minuten")]
        public void PauseLabel_ReadsNaturally(int minutes, string expected)
        {
            Assert.Equal(expected, StatusBuilder.PauseLabel(minutes));
        }

        [Fact]
        public void PauseLabel_English()
        {
            Loc.Language = "en";
            Assert.Equal("1 hour", StatusBuilder.PauseLabel(60));
            Assert.Equal("2 hours", StatusBuilder.PauseLabel(120));
            Assert.Equal("30 minutes", StatusBuilder.PauseLabel(30));
        }

        [Fact]
        public void Status_English()
        {
            Loc.Language = "en";
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Pause(T.Mon(10, 0, 5), TimeSpan.FromMinutes(30), "x");
            StatusInfo info = StatusBuilder.Build(r.Engine, r.Settings, T.Mon(10, 1), false);
            Assert.Equal("Paused", info.Title);
            Assert.Equal("Resumes automatically: today 10:30", info.Detail);
        }
    }
}
