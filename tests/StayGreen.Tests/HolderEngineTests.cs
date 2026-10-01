using System;
using System.Collections.Generic;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    public class HolderEngineTests
    {
        sealed class Rig
        {
            public Settings Settings;
            public FakeInput Input = new FakeInput();
            public List<string> Log = new List<string>();
            public HolderEngine Engine;

            public Rig(Action<Settings> configure = null)
            {
                Settings = new Settings { SmartIdle = false, IntervalSeconds = 30, KeepAwake = true };
                if (configure != null) configure(Settings);
                Engine = new HolderEngine(Input, Settings, (t, kind, msg) => Log.Add(kind + ": " + msg));
            }

            public bool Logged(string kind)
            {
                return Log.Exists(l => l.StartsWith(kind + ":"));
            }
        }

        [Fact]
        public void Tick_WhenNotStarted_DoesNothing()
        {
            var r = new Rig();
            r.Engine.Tick(T.Mon(10));
            Assert.Equal(0, r.Input.Sent);
            Assert.Equal(HolderState.Stopped, r.Engine.State);
        }

        [Fact]
        public void Start_SendsImmediately_ThenEveryInterval()
        {
            var r = new Rig();
            DateTime t0 = T.Mon(10);
            r.Engine.Start(t0, "Test");
            Assert.Equal(1, r.Input.Sent);

            for (int s = 1; s < 30; s++) r.Engine.Tick(t0.AddSeconds(s));
            Assert.Equal(1, r.Input.Sent);

            r.Engine.Tick(t0.AddSeconds(30));
            Assert.Equal(2, r.Input.Sent);

            for (int s = 31; s < 60; s++) r.Engine.Tick(t0.AddSeconds(s));
            Assert.Equal(2, r.Input.Sent);

            r.Engine.Tick(t0.AddSeconds(60));
            Assert.Equal(3, r.Input.Sent);
            Assert.Equal(3, r.Engine.ActivityCount);
            Assert.Equal(t0.AddSeconds(60), r.Engine.LastActivity);
            Assert.Equal(t0.AddSeconds(90), r.Engine.NextActivityAt);
        }

        [Fact]
        public void Start_Twice_IsIgnored()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "A");
            r.Engine.Start(T.Mon(10, 0, 5), "B");
            Assert.Equal(1, r.Input.Sent);
            Assert.Single(r.Log.FindAll(l => l.StartsWith("START")));
        }

        [Fact]
        public void ModeAndPixels_ArePassedToTheBackend()
        {
            var r = new Rig(s => { s.Mode = ActivityMode.Mouse; s.MousePixels = 7; });
            r.Engine.Start(T.Mon(10), "Test");
            Assert.Equal(ActivityMode.Mouse, r.Input.LastMode);
            Assert.Equal(7, r.Input.LastPixels);
        }

        [Fact]
        public void IntervalChange_TakesEffectAfterNextActivity()
        {
            var r = new Rig();
            DateTime t0 = T.Mon(10);
            r.Engine.Start(t0, "Test");
            r.Settings.IntervalSeconds = 10;
            r.Engine.Tick(t0.AddSeconds(30));         // faellig nach altem Intervall
            Assert.Equal(2, r.Input.Sent);
            Assert.Equal(t0.AddSeconds(40), r.Engine.NextActivityAt);
        }

        [Fact]
        public void ClockMovedBackwards_ActsImmediatelyInsteadOfWaitingAnHour()
        {
            var r = new Rig();
            DateTime t0 = T.Mon(3, 0, 0);
            r.Engine.Start(t0, "Test");                 // sendet sofort, naechste Aktion t0 + 30 s
            DateTime back = t0.AddHours(-1);            // Winterzeit: Die Uhr springt eine Stunde zurueck
            r.Engine.Tick(back);
            Assert.Equal(2, r.Input.Sent);
            Assert.Equal(back.AddSeconds(30), r.Engine.NextActivityAt);
        }

        [Fact]
        public void IntervalShortened_TakesEffectImmediately()
        {
            var r = new Rig(s => s.IntervalSeconds = 600);
            DateTime t0 = T.Mon(10);
            r.Engine.Start(t0, "Test");                 // naechste Aktion erst in 600 s
            r.Settings.IntervalSeconds = 30;
            r.Engine.Tick(t0.AddSeconds(1));            // liegt weiter als ein Intervall voraus -> sofort handeln
            Assert.Equal(2, r.Input.Sent);
        }

        // ---- Intelligenter Modus ----

        [Fact]
        public void SmartIdle_StaysOutOfTheWayWhileUserIsActive()
        {
            var r = new Rig(s => s.SmartIdle = true);
            r.Input.Idle = TimeSpan.FromSeconds(5);
            DateTime t0 = T.Mon(10);
            r.Engine.Start(t0, "Test");

            for (int s = 0; s < 120; s++) r.Engine.Tick(t0.AddSeconds(s));
            Assert.Equal(0, r.Input.Sent);
            Assert.True(r.Engine.StandingBy);
            Assert.Null(r.Engine.NextActivityAt);
            Assert.Equal(HolderState.Active, r.Engine.State);
        }

        [Fact]
        public void SmartIdle_ActsAsSoonAsUserHasBeenIdleLongEnough()
        {
            var r = new Rig(s => s.SmartIdle = true);
            DateTime t0 = T.Mon(10);
            r.Input.Idle = TimeSpan.FromSeconds(29);
            r.Engine.Start(t0, "Test");
            Assert.Equal(0, r.Input.Sent);

            r.Input.Idle = TimeSpan.FromSeconds(30);
            r.Engine.Tick(t0.AddSeconds(1));
            Assert.Equal(1, r.Input.Sent);
            Assert.False(r.Engine.StandingBy);
        }

        [Fact]
        public void SmartIdle_AfterOwnInputWaitsForAnotherFullInterval()
        {
            var r = new Rig(s => s.SmartIdle = true);
            DateTime t0 = T.Mon(10);
            r.Engine.Start(t0, "Test");                 // Idle = 1 h -> sofort
            Assert.Equal(1, r.Input.Sent);
            Assert.Equal(TimeSpan.Zero, r.Input.Idle);  // eigene Eingabe setzt den Leerlauf zurueck

            r.Engine.Tick(t0.AddSeconds(30));           // nach 30 s Leerlauf wuerde es wieder greifen
            Assert.Equal(1, r.Input.Sent);              // hier aber noch 0 s Leerlauf im Fake
            Assert.True(r.Engine.StandingBy);

            r.Input.Idle = TimeSpan.FromSeconds(31);
            r.Engine.Tick(t0.AddSeconds(31));
            Assert.Equal(2, r.Input.Sent);
        }

        // ---- Wach halten ----

        [Fact]
        public void KeepAwake_IsRequestedWhileActive_AndReleasedOnStop()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "Test");
            Assert.True(r.Input.Awake);
            r.Engine.Stop(T.Mon(10, 5), "Test");
            Assert.False(r.Input.Awake);
        }

        [Fact]
        public void KeepAwake_Disabled_IsNeverRequested()
        {
            var r = new Rig(s => s.KeepAwake = false);
            r.Engine.Start(T.Mon(10), "Test");
            r.Engine.Tick(T.Mon(10, 1));
            r.Engine.Stop(T.Mon(10, 2), "Test");
            Assert.DoesNotContain(true, r.Input.AwakeCalls);
        }

        [Fact]
        public void KeepAwake_CanBeSwitchedOffWhileRunning()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "Test");
            Assert.True(r.Input.Awake);
            r.Settings.KeepAwake = false;
            r.Engine.Tick(T.Mon(10, 0, 1));
            Assert.False(r.Input.Awake);
        }

        // ---- Zeitplan ----

        static void OfficeHours(Settings s)
        {
            s.ScheduleEnabled = true;
            s.Rules.Add(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
        }

        [Fact]
        public void Schedule_OutsideWindow_WaitsAndDoesNotSendOrKeepAwake()
        {
            var r = new Rig(OfficeHours);
            r.Engine.Start(T.Mon(7), "Test");
            Assert.Equal(HolderState.WaitingForWindow, r.Engine.State);
            Assert.True(r.Engine.Running);
            Assert.Equal(0, r.Input.Sent);
            Assert.False(r.Input.Awake);
            Assert.True(r.Logged("PAUSE"));
        }

        [Fact]
        public void Schedule_EntersWindow_ResumesAndSends()
        {
            var r = new Rig(OfficeHours);
            r.Engine.Start(T.Mon(7, 59, 0), "Test");
            r.Engine.Tick(T.Mon(7, 59, 59));
            Assert.Equal(0, r.Input.Sent);

            r.Engine.Tick(T.Mon(8, 0, 0));
            Assert.Equal(HolderState.Active, r.Engine.State);
            Assert.Equal(1, r.Input.Sent);
            Assert.True(r.Input.Awake);
            Assert.True(r.Logged("RESUME"));
        }

        [Fact]
        public void Schedule_LeavesWindow_PausesAgain()
        {
            var r = new Rig(OfficeHours);
            r.Engine.Start(T.Mon(16, 59, 0), "Test");
            Assert.Equal(HolderState.Active, r.Engine.State);
            r.Engine.Tick(T.Mon(17, 0, 0));
            Assert.Equal(HolderState.WaitingForWindow, r.Engine.State);
            Assert.False(r.Input.Awake);
            Assert.True(r.Logged("PAUSE"));
        }

        [Fact]
        public void Schedule_StartInsideWindow_IsActiveRightAway()
        {
            var r = new Rig(OfficeHours);
            r.Engine.Start(T.Mon(10), "Test");
            Assert.Equal(HolderState.Active, r.Engine.State);
            Assert.Equal(1, r.Input.Sent);
            Assert.False(r.Logged("PAUSE"));
        }

        [Fact]
        public void Schedule_EnabledButEmpty_BehavesLikeAlwaysOn()
        {
            var r = new Rig(s => s.ScheduleEnabled = true);
            r.Engine.Start(T.Mon(3), "Test");
            Assert.Equal(HolderState.Active, r.Engine.State);
            Assert.Equal(1, r.Input.Sent);
        }

        [Fact]
        public void Schedule_RulesChangedWhileRunning_ApplyOnNextTick()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(12), "Test");
            Assert.Equal(HolderState.Active, r.Engine.State);

            OfficeHours(r.Settings);
            r.Settings.Rules.Clear();
            r.Settings.Rules.Add(ScheduleRule.Weekdays(T.Hm(14), T.Hm(15)));
            r.Engine.Tick(T.Mon(12, 0, 1));
            Assert.Equal(HolderState.WaitingForWindow, r.Engine.State);
        }

        // ---- Fehler ----

        [Fact]
        public void Failures_AreCountedAndResetOnSuccess()
        {
            var r = new Rig();
            r.Input.Accept = false;
            DateTime t0 = T.Mon(10);
            r.Engine.Start(t0, "Test");
            Assert.Equal(1, r.Engine.ConsecutiveFailures);
            r.Engine.Tick(t0.AddSeconds(30));
            Assert.Equal(2, r.Engine.ConsecutiveFailures);
            Assert.Equal(0, r.Engine.ActivityCount);

            r.Input.Accept = true;
            r.Engine.Tick(t0.AddSeconds(60));
            Assert.Equal(0, r.Engine.ConsecutiveFailures);
            Assert.Equal(1, r.Engine.ActivityCount);
        }

        // ---- Stoppen ----

        [Fact]
        public void Stop_LogsRuntimeAndResetsState()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10, 0, 0), "Test");
            r.Engine.Stop(T.Mon(10, 5, 30), "Manuell");
            Assert.False(r.Engine.Running);
            Assert.Equal(HolderState.Stopped, r.Engine.State);
            string stop = r.Log.Find(l => l.StartsWith("STOP"));
            Assert.NotNull(stop);
            Assert.Contains("00:05:30", stop);
            Assert.Contains("Manuell", stop);
        }

        [Fact]
        public void Stop_WhenNotRunning_IsNoOp()
        {
            var r = new Rig();
            r.Engine.Stop(T.Mon(10), "x");
            Assert.Empty(r.Log);
        }

        [Fact]
        public void Elapsed_CountsFromStart()
        {
            var r = new Rig();
            Assert.Equal(TimeSpan.Zero, r.Engine.Elapsed(T.Mon(10)));
            r.Engine.Start(T.Mon(10), "x");
            Assert.Equal(TimeSpan.FromMinutes(90), r.Engine.Elapsed(T.Mon(11, 30)));
            Assert.Equal(TimeSpan.Zero, r.Engine.Elapsed(T.Mon(9)));   // Uhr zurueckgestellt: nie negativ
        }

        [Fact]
        public void Restart_ResetsCounters()
        {
            var r = new Rig();
            r.Engine.Start(T.Mon(10), "x");
            r.Engine.Tick(T.Mon(10, 0, 30));
            Assert.Equal(2, r.Engine.ActivityCount);
            r.Engine.Stop(T.Mon(10, 1), "x");
            r.Engine.Start(T.Mon(11), "x");
            Assert.Equal(1, r.Engine.ActivityCount);
        }

        [Fact]
        public void Constructor_RejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new HolderEngine(null, new Settings(), null));
            Assert.Throws<ArgumentNullException>(() => new HolderEngine(new FakeInput(), null, null));
        }
    }

    public class StopExecutorTests
    {
        [Fact]
        public void Everything_Confirmed_ClosesTeamsAndShutsDownWithoutLocking()
        {
            var a = new FakeActions();
            var o = StopExecutor.Execute(
                new StopPlan { CloseTeams = true, Shutdown = true, Lock = true, ExitApp = true }, a, () => true);
            Assert.Equal(new[] { "teams", "shutdown" }, a.Calls.ToArray());
            Assert.True(o.TeamsClosed);
            Assert.True(o.ShutdownStarted);
            Assert.False(o.Locked);
            Assert.True(o.ExitRequested);
        }

        [Fact]
        public void ShutdownCancelled_StillLocks()
        {
            var a = new FakeActions();
            var o = StopExecutor.Execute(
                new StopPlan { CloseTeams = true, Shutdown = true, Lock = true }, a, () => false);
            Assert.Equal(new[] { "teams", "lock" }, a.Calls.ToArray());
            Assert.True(o.ShutdownCancelled);
            Assert.False(o.ShutdownStarted);
            Assert.True(o.Locked);
        }

        [Fact]
        public void NoConfirmCallback_ShutsDownDirectly()
        {
            var a = new FakeActions();
            StopExecutor.Execute(new StopPlan { Shutdown = true }, a, null);
            Assert.Equal(new[] { "shutdown" }, a.Calls.ToArray());
        }

        [Fact]
        public void EmptyPlan_DoesNothing()
        {
            var a = new FakeActions();
            var o = StopExecutor.Execute(new StopPlan(), a, () => true);
            Assert.Empty(a.Calls);
            Assert.False(o.ExitRequested);
        }

        [Fact]
        public void FromSettings_CopiesFlags()
        {
            var s = new Settings { StopCloseTeams = true, StopLock = true, StopShutdown = false, StopExitApp = false };
            var p = StopPlan.FromSettings(s);
            Assert.True(p.CloseTeams);
            Assert.True(p.Lock);
            Assert.False(p.Shutdown);
            Assert.False(p.ExitApp);
        }

        [Fact]
        public void ShutdownIsNeverTriggeredByAConfirmCallbackThatIsNotCalled()
        {
            bool asked = false;
            var a = new FakeActions();
            StopExecutor.Execute(new StopPlan { CloseTeams = true }, a, () => { asked = true; return true; });
            Assert.False(asked);
            Assert.DoesNotContain("shutdown", a.Calls);
        }
    }
}
