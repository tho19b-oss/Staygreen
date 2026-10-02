using System;
using System.Collections.Generic;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    public class AutoStopScheduleEndTests
    {
        static Settings AtScheduleEnd(params ScheduleRule[] rules)
        {
            var s = new Settings { ScheduleEnabled = true, AutoStopEnabled = true, AutoStopTiming = StopTiming.ScheduleEnd };
            s.Rules.AddRange(rules);
            return s;
        }

        [Fact]
        public void OfficeHours_DueAtTheEndOfTheDay()
        {
            Settings s = AtScheduleEnd(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
            Assert.Equal(T.Mon(17), AutoStopPlanner.NextDue(s, T.Mon(10)));
            Assert.Equal(T.Mon(17), AutoStopPlanner.NextDue(s, T.Mon(7)));
        }

        [Fact]
        public void LunchBreak_DoesNotCountAsEndOfDay()
        {
            Settings s = AtScheduleEnd(ScheduleRule.Weekdays(T.Hm(8), T.Hm(12)), ScheduleRule.Weekdays(T.Hm(13), T.Hm(17)));
            Assert.Equal(T.Mon(17), AutoStopPlanner.NextDue(s, T.Mon(9)));
            Assert.Equal(T.Mon(17), AutoStopPlanner.NextDue(s, T.Mon(12, 30)));
        }

        [Fact]
        public void AfterTheEnd_NextDueIsTomorrow_AndOnFridayMonday()
        {
            Settings s = AtScheduleEnd(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
            Assert.Equal(T.Day(1, 17), AutoStopPlanner.NextDue(s, T.Mon(18)));
            Assert.Equal(T.Day(1, 17), AutoStopPlanner.NextDue(s, T.Mon(17)));      // genau zum Ende: heutiger Termin ist durch
            Assert.Equal(T.Day(7, 17), AutoStopPlanner.NextDue(s, T.Day(4, 18)));
        }

        [Fact]
        public void ShorterFriday_IsRespected()
        {
            var monThu = new ScheduleRule(new[] { true, true, true, true, false, false, false }, T.Hm(8), T.Hm(17));
            var friday = new ScheduleRule(new[] { false, false, false, false, true, false, false }, T.Hm(8), T.Hm(15));
            Settings s = AtScheduleEnd(monThu, friday);
            Assert.Equal(T.Day(4, 15), AutoStopPlanner.NextDue(s, T.Day(4, 9)));
            Assert.Equal(T.Day(3, 17), AutoStopPlanner.NextDue(s, T.Day(3, 9)));
        }

        [Fact]
        public void NightShift_EndsInTheMorning()
        {
            Settings s = AtScheduleEnd(new ScheduleRule(new[] { true, true, true, true, true, true, true }, T.Hm(22), T.Hm(6)));
            Assert.Equal(T.Day(1, 6), AutoStopPlanner.NextDue(s, T.Mon(12)));
            Assert.Equal(T.Day(1, 6), AutoStopPlanner.NextDue(s, T.Mon(23)));
            Assert.Equal(T.Day(1, 6), AutoStopPlanner.NextDue(s, T.Day(1, 3)));      // mitten in der Nacht: heute 06:00
        }

        [Fact]
        public void VacationDays_AreSkipped()
        {
            Settings s = AtScheduleEnd(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
            s.Exceptions.Add(new DateRange(T.Day(0), T.Day(2)));                     // Mo-Mi frei
            Assert.Equal(T.Day(3, 17), AutoStopPlanner.NextDue(s, T.Mon(10)));
        }

        [Fact]
        public void WithoutAnEffectiveSchedule_NothingIsDue()
        {
            var noSchedule = new Settings { AutoStopEnabled = true, AutoStopTiming = StopTiming.ScheduleEnd };
            Assert.Null(AutoStopPlanner.NextDue(noSchedule, T.Mon(10)));

            Settings off = AtScheduleEnd(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
            off.ScheduleEnabled = false;
            Assert.Null(AutoStopPlanner.NextDue(off, T.Mon(10)));

            Settings disabled = AtScheduleEnd(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
            disabled.AutoStopEnabled = false;
            Assert.Null(AutoStopPlanner.NextDue(disabled, T.Mon(10)));
        }

        [Fact]
        public void AroundTheClockSchedule_HasNoEnd()
        {
            Settings s = AtScheduleEnd(
                new ScheduleRule(new[] { true, true, true, true, true, true, true }, T.Hm(0), T.Hm(12)),
                new ScheduleRule(new[] { true, true, true, true, true, true, true }, T.Hm(12), T.Hm(0)));
            Assert.Null(AutoStopPlanner.NextDue(s, T.Mon(9)));
        }

        [Fact]
        public void StatusLine_ExplainsWhyThereIsNoDate()
        {
            Loc.Language = "de";
            var noSchedule = new Settings { AutoStopEnabled = true, AutoStopTiming = StopTiming.ScheduleEnd };
            Assert.Equal("Auto-Stopp: Zeitplan fehlt", StatusBuilder.AutoStopLine(noSchedule, T.Mon(10), null));

            Settings noEnd = AtScheduleEnd(
                new ScheduleRule(new[] { true, true, true, true, true, true, true }, T.Hm(0), T.Hm(12)),
                new ScheduleRule(new[] { true, true, true, true, true, true, true }, T.Hm(12), T.Hm(0)));
            Assert.Equal("Auto-Stopp: kein Zeitplan-Ende in Sicht", StatusBuilder.AutoStopLine(noEnd, T.Mon(10), null));
        }
    }

    public class AutoStopCoordinatorTests : IDisposable
    {
        sealed class Rig
        {
            public readonly Settings S;
            public readonly FakePrompt Prompt = new FakePrompt();
            public DateTime Clock = T.Mon(10);
            public readonly AutoStopCoordinator C;

            public Rig(Action<Settings> configure = null)
            {
                S = new Settings
                {
                    AutoStopEnabled = true,
                    AutoStopTiming = StopTiming.Daily,
                    AutoStopTime = T.Hm(17),
                    StopCloseTeams = true,
                    AutoStopWarnSeconds = 60,
                    AutoStopSnoozeMinutes = 15,
                };
                if (configure != null) configure(S);
                C = new AutoStopCoordinator(S, Prompt, () => Clock);
                C.Reschedule(Clock);
            }

            /// <summary>Tick zu einer bestimmten Uhrzeit (die Uhr zeigt dann auch diese Zeit).</summary>
            public AutoStopResult TickAt(DateTime now)
            {
                Clock = now;
                return C.Tick(now);
            }
        }

        public AutoStopCoordinatorTests()
        {
            Loc.Language = "de";
        }

        public void Dispose()
        {
            Loc.Language = "de";
        }

        [Fact]
        public void BeforeTheWarning_NothingHappens()
        {
            var r = new Rig();
            Assert.Equal(T.Mon(17), r.C.Due);
            Assert.Equal(AutoStopResult.None, r.TickAt(T.Mon(10)));
            Assert.Equal(AutoStopResult.None, r.TickAt(T.Mon(16, 58, 59)));
            Assert.Equal(0, r.Prompt.Asked);
        }

        [Fact]
        public void WarnsBeforeTheAppointment_AndExecutesWhenTheCountdownRunsOut()
        {
            var r = new Rig();
            r.Prompt.OnAsk = () => r.Clock = T.Mon(17);          // Der Dialog hat bis zum Termin gewartet.

            Assert.Equal(AutoStopResult.Execute, r.TickAt(T.Mon(16, 59)));
            Assert.Equal(1, r.Prompt.Asked);
            Assert.Equal(T.Mon(17), r.Prompt.AskedDue[0]);
            Assert.Equal("Teams beenden, StayGreen beenden", r.Prompt.AskedActions[0]);
            Assert.Equal(T.Day(1, 17), r.C.Due);                 // naechster Termin steht schon fest
        }

        [Fact]
        public void Cancel_SkipsTodayButNotTomorrow()
        {
            var r = new Rig();
            r.Prompt.Answers.Enqueue(AutoStopChoice.Cancel);

            Assert.Equal(AutoStopResult.Cancelled, r.TickAt(T.Mon(16, 59)));
            Assert.Equal(T.Day(1, 17), r.C.Due);
            Assert.Equal(AutoStopResult.None, r.TickAt(T.Mon(17)));
            Assert.Equal(1, r.Prompt.Asked);
        }

        [Fact]
        public void Snooze_ShiftsByTheConfiguredMinutes_AndWarnsAgainBeforeTheNewTime()
        {
            var r = new Rig();
            r.Prompt.Answers.Enqueue(AutoStopChoice.Snooze);

            Assert.Equal(AutoStopResult.Snoozed, r.TickAt(T.Mon(16, 59, 10)));
            Assert.Equal(T.Mon(17, 15), r.C.Due);
            Assert.Equal(T.Mon(17, 15), r.C.LastSnoozedTo);

            Assert.Equal(AutoStopResult.None, r.TickAt(T.Mon(17, 0, 30)));
            Assert.Equal(AutoStopResult.None, r.TickAt(T.Mon(17, 13, 59)));
            Assert.Equal(1, r.Prompt.Asked);

            r.Prompt.OnAsk = () => r.Clock = T.Mon(17, 15);
            Assert.Equal(AutoStopResult.Execute, r.TickAt(T.Mon(17, 14)));
            Assert.Equal(2, r.Prompt.Asked);
            Assert.Equal(T.Mon(17, 15), r.Prompt.AskedDue[1]);
            Assert.Equal(T.Day(1, 17), r.C.Due);
        }

        [Fact]
        public void Snooze_UsesTheLaterOfNowAndTheAppointment()
        {
            var r = new Rig();
            r.Prompt.Answers.Enqueue(AutoStopChoice.Snooze);
            r.Prompt.OnAsk = () => r.Clock = T.Mon(17, 0, 20);   // Der Nutzer hat erst nach dem Termin geklickt.
            r.TickAt(T.Mon(16, 59));
            Assert.Equal(T.Mon(17, 15, 20), r.C.Due);
        }

        [Fact]
        public void RunNow_Executes_AndTheSameAppointmentDoesNotFireAgain()
        {
            var r = new Rig();
            r.Prompt.Answers.Enqueue(AutoStopChoice.RunNow);

            Assert.Equal(AutoStopResult.Execute, r.TickAt(T.Mon(16, 59, 10)));
            Assert.Equal(T.Day(1, 17), r.C.Due);                 // nicht noch einmal heute 17:00
            Assert.Equal(AutoStopResult.None, r.TickAt(T.Mon(17)));
            Assert.Equal(AutoStopResult.None, r.TickAt(T.Mon(17, 0, 30)));
            Assert.Equal(1, r.Prompt.Asked);
        }

        [Fact]
        public void HarmlessPlan_NeedsNoWarning()
        {
            var r = new Rig(s => { s.StopCloseTeams = false; s.StopLock = false; s.StopShutdown = false; s.StopExitApp = true; });
            Assert.Equal(AutoStopResult.None, r.TickAt(T.Mon(16, 59)));
            Assert.Equal(AutoStopResult.Execute, r.TickAt(T.Mon(17)));
            Assert.Equal(0, r.Prompt.Asked);
        }

        [Fact]
        public void LongMissedAppointment_IsSkippedWithoutAWarning()
        {
            var r = new Rig();
            Assert.Equal(AutoStopResult.Missed, r.TickAt(T.Mon(17, 2, 1)));        // 121 s nach dem Termin
            Assert.Equal(0, r.Prompt.Asked);
            Assert.Equal(T.Day(1, 17), r.C.Due);
        }

        [Fact]
        public void LaptopOpenedJustAfterTheAppointment_GetsAFullCountdown()
        {
            var r = new Rig();
            r.Prompt.OnAsk = () => r.Clock = T.Mon(17, 1, 30);
            Assert.Equal(AutoStopResult.Execute, r.TickAt(T.Mon(17, 0, 30)));
            Assert.Equal(T.Mon(17, 1, 30), r.Prompt.AskedDue[0]);                  // jetzt + 60 s, nicht der abgelaufene Termin
        }

        [Fact]
        public void OneTimeAppointment_ConsumesItselfAndAsksForASave()
        {
            var r = new Rig(s => { s.AutoStopTiming = StopTiming.Once; s.AutoStopOnce = T.Mon(17); });
            Assert.Equal(T.Mon(17), r.C.Due);
            r.Prompt.OnAsk = () => r.Clock = T.Mon(17);

            Assert.Equal(AutoStopResult.Execute, r.TickAt(T.Mon(16, 59)));
            Assert.False(r.S.AutoStopEnabled);
            Assert.True(r.C.SettingsChanged);
            Assert.Null(r.C.Due);
        }

        [Fact]
        public void OneTimeAppointment_CancelledAlsoSwitchesOff()
        {
            var r = new Rig(s => { s.AutoStopTiming = StopTiming.Once; s.AutoStopOnce = T.Mon(17); });
            r.Prompt.Answers.Enqueue(AutoStopChoice.Cancel);
            Assert.Equal(AutoStopResult.Cancelled, r.TickAt(T.Mon(16, 59)));
            Assert.False(r.S.AutoStopEnabled);
            Assert.True(r.C.SettingsChanged);
        }

        [Fact]
        public void SnoozedAppointment_SurvivesAChangeOfUnrelatedSettings()
        {
            var r = new Rig();
            r.Prompt.Answers.Enqueue(AutoStopChoice.Snooze);
            r.TickAt(T.Mon(16, 59));
            Assert.Equal(T.Mon(17, 15), r.C.Due);

            r.Clock = T.Mon(17, 5);
            r.C.Reschedule(r.Clock);                              // z. B. Einstellung geaendert
            Assert.Equal(T.Mon(17, 15), r.C.Due);
        }

        [Fact]
        public void SwitchingTheAutoStopOff_ClearsTheAppointment()
        {
            var r = new Rig();
            r.S.AutoStopEnabled = false;
            r.C.Reschedule(r.Clock);
            Assert.Null(r.C.Due);
            Assert.Equal(AutoStopResult.None, r.TickAt(T.Mon(17)));
        }

        [Fact]
        public void ScheduleEndMode_UsesTheEndOfTheLastWindow()
        {
            var r = new Rig(s =>
            {
                s.AutoStopTiming = StopTiming.ScheduleEnd;
                s.ScheduleEnabled = true;
                s.Rules.Add(ScheduleRule.Weekdays(T.Hm(8), T.Hm(15)));
            });
            Assert.Equal(T.Mon(15), r.C.Due);
        }

        [Fact]
        public void ReentrantTick_WhileTheWarningIsOpen_DoesNothing()
        {
            var r = new Rig();
            AutoStopResult inner = AutoStopResult.Execute;
            r.Prompt.OnAsk = () =>
            {
                r.Clock = T.Mon(17);
                inner = r.C.Tick(T.Mon(17));                     // Der Takt der Oberflaeche laeuft weiter.
            };
            Assert.Equal(AutoStopResult.Execute, r.TickAt(T.Mon(16, 59)));
            Assert.Equal(AutoStopResult.None, inner);
            Assert.Equal(1, r.Prompt.Asked);
        }

        [Fact]
        public void WithoutAPrompt_TheCountdownCountsAsTimedOut()
        {
            var s = new Settings { AutoStopEnabled = true, AutoStopTime = T.Hm(17), StopLock = true };
            DateTime clock = T.Mon(10);
            var c = new AutoStopCoordinator(s, null, () => clock);
            c.Reschedule(clock);
            clock = T.Mon(16, 59);
            Assert.Equal(AutoStopResult.Execute, c.Tick(clock));
        }

        [Fact]
        public void Constructor_RejectsNullSettings()
        {
            Assert.Throws<ArgumentNullException>(() => new AutoStopCoordinator(null, new FakePrompt(), null));
        }
    }
}
