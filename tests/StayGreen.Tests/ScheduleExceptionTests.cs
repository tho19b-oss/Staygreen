using System;
using System.Collections.Generic;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    public class DateRangeTests
    {
        static DateTime D(int month, int day, int year = 2026)
        {
            return new DateTime(year, month, day);
        }

        [Fact]
        public void Contains_IncludesBothEndsAndIgnoresTime()
        {
            var r = new DateRange(D(12, 24), D(12, 26));
            Assert.True(r.Contains(D(12, 24)));
            Assert.True(r.Contains(new DateTime(2026, 12, 26, 23, 59, 59)));
            Assert.False(r.Contains(D(12, 23)));
            Assert.False(r.Contains(D(12, 27)));
            Assert.Equal(3, r.DayCount);
        }

        [Fact]
        public void Serialize_RoundTrips()
        {
            var r = new DateRange(D(12, 24), D(1, 2, 2027));
            Assert.Equal("2026-12-24;2027-01-02", r.Serialize());
            DateRange parsed;
            Assert.True(DateRange.TryParse(r.Serialize(), out parsed));
            Assert.Equal(r.From, parsed.From);
            Assert.Equal(r.To, parsed.To);
        }

        [Theory]
        [InlineData("")]
        [InlineData("2026-12-24")]
        [InlineData("2026-12-24;gestern")]
        [InlineData("24.12.2026;26.12.2026")]
        [InlineData("2026-13-01;2026-13-02")]
        [InlineData("2026-12-24;2026-12-26;2026-12-27")]
        public void TryParse_RejectsGarbage(string text)
        {
            DateRange parsed;
            Assert.False(DateRange.TryParse(text, out parsed));
        }

        [Fact]
        public void Normalize_SwapsSortsAndMerges()
        {
            var list = new List<DateRange>
            {
                new DateRange(D(12, 31), D(12, 27)),          // vertauschte Grenzen
                new DateRange(D(12, 24), D(12, 26)),          // grenzt direkt an 27.
                new DateRange(D(3, 1), D(3, 2)),
                new DateRange(D(3, 2), D(3, 5)),              // ueberlappt
                new DateRange(),                              // ungueltig (MinValue)
                null,
            };
            DateRange.Normalize(list);
            Assert.Equal(2, list.Count);
            Assert.Equal("2026-03-01;2026-03-05", list[0].Serialize());
            Assert.Equal("2026-12-24;2026-12-31", list[1].Serialize());
        }

        [Fact]
        public void Normalize_CapsTheNumberOfEntries()
        {
            var list = new List<DateRange>();
            for (int i = 0; i < Settings.MaxExceptions + 20; i++)
                list.Add(new DateRange(new DateTime(2026, 1, 1).AddDays(i * 3), new DateTime(2026, 1, 1).AddDays(i * 3)));
            DateRange.Normalize(list);
            Assert.Equal(Settings.MaxExceptions, list.Count);
        }

        [Fact]
        public void Exclude_AddsADayAndMergesWithNeighbours()
        {
            var list = new List<DateRange> { new DateRange(D(12, 24), D(12, 25)), new DateRange(D(12, 27), D(12, 28)) };
            DateRange.Exclude(list, D(12, 26));
            Assert.Single(list);
            Assert.Equal("2026-12-24;2026-12-28", list[0].Serialize());
        }

        [Fact]
        public void Include_RemovesADay_SplittingTheRange()
        {
            var list = new List<DateRange> { new DateRange(D(12, 24), D(12, 28)) };
            DateRange.Include(list, D(12, 26));
            Assert.Equal(2, list.Count);
            Assert.Equal("2026-12-24;2026-12-25", list[0].Serialize());
            Assert.Equal("2026-12-27;2026-12-28", list[1].Serialize());

            DateRange.Include(list, D(12, 24));          // Rand: Zeitraum wird kuerzer
            Assert.Equal("2026-12-25;2026-12-25", list[0].Serialize());
            DateRange.Include(list, D(12, 25));          // letzter Tag: Zeitraum verschwindet
            Assert.Single(list);
            DateRange.Include(list, D(1, 1));            // nicht enthalten: nichts passiert
            Assert.Single(list);
        }

        [Fact]
        public void Prune_KeepsOneDayOfBuffer()
        {
            var list = new List<DateRange>
            {
                new DateRange(D(10, 1), D(10, 3)),     // lange vorbei
                new DateRange(D(10, 4), D(10, 4)),     // gestern (heute = 5.): bleibt wegen Fenstern ueber Mitternacht
                new DateRange(D(10, 5), D(10, 9)),
            };
            DateRange.Prune(list, D(10, 5));
            Assert.Equal(2, list.Count);
            Assert.Equal(D(10, 4), list[0].From);
        }

        [Fact]
        public void SettingsHelpers_AreConsistent()
        {
            var s = new Settings();
            s.Exceptions.Add(new DateRange(D(12, 24), D(12, 26)));
            Assert.True(s.IsExcluded(new DateTime(2026, 12, 25, 12, 0, 0)));
            Assert.False(s.IsExcluded(D(12, 27)));
        }
    }

    public class ScheduleExceptionTests : IDisposable
    {
        static readonly bool[] AllDays = { true, true, true, true, true, true, true };

        public ScheduleExceptionTests()
        {
            Loc.Language = "de";
        }

        public void Dispose()
        {
            Loc.Language = "de";
        }

        static List<ScheduleRule> Office()
        {
            return new List<ScheduleRule> { ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)) };
        }

        static List<ScheduleRule> Night()
        {
            return new List<ScheduleRule> { new ScheduleRule(AllDays, T.Hm(22), T.Hm(6)) };
        }

        // ---- Der Fehler aus der Review: Ende eines Fensters ueber Mitternacht, das am Vorabend begann ----

        [Fact]
        public void NextChange_InsideOvernightWindowAfterMidnight_IsTheEndTodayNotTomorrow()
        {
            // Dienstag 03:00, taeglich 22:00-06:00: Das Fenster endet heute um 06:00 (frueher: erst morgen).
            Assert.Equal(T.Day(1, 6), Scheduler.NextChange(T.Day(1, 3), Night()));
        }

        [Fact]
        public void NextChange_OvernightWindowOfASingleDay_EndsSameMorningNotNextWeek()
        {
            var friday = new List<ScheduleRule>
            {
                new ScheduleRule(new[] { false, false, false, false, true, false, false }, T.Hm(22), T.Hm(6)),
            };
            Assert.Equal(T.Day(5, 6), Scheduler.NextChange(T.Day(5, 3), friday));   // Samstag 03:00 -> Samstag 06:00
            Assert.Equal(T.Day(5, 6), Scheduler.NextChange(T.Day(4, 23), friday));  // Freitag 23:00 -> Samstag 06:00
        }

        [Fact]
        public void ScheduleLine_AtNight_SaysTodayForTheEnd()
        {
            var s = new Settings { ScheduleEnabled = true };
            s.Rules.AddRange(Night());
            Assert.Equal("Zeitplan: aktiv bis heute 06:00", StatusBuilder.ScheduleLine(s, T.Day(1, 3)));
            Assert.Equal("Zeitplan: aktiv bis morgen 06:00", StatusBuilder.ScheduleLine(s, T.Day(1, 23)));
        }

        // ---- Ausnahmetage ----

        [Fact]
        public void IsActive_ExcludedDay_IsInactive()
        {
            var ex = new List<DateRange> { new DateRange(T.Mon(), T.Mon()) };
            Assert.False(Scheduler.IsActive(T.Mon(10), Office(), ex));
            Assert.True(Scheduler.IsActive(T.Day(1, 10), Office(), ex));
            Assert.True(Scheduler.IsActive(T.Mon(10), Office(), null));
        }

        [Fact]
        public void NextChange_SkipsExcludedDays()
        {
            var ex = new List<DateRange> { new DateRange(T.Day(0), T.Day(1)) };      // Montag und Dienstag
            Assert.Equal(T.Day(2, 8), Scheduler.NextChange(T.Mon(7), Office(), ex));  // -> Mittwoch 08:00
        }

        [Fact]
        public void NextChange_FindsTheEndOfALongVacation()
        {
            // Drei Wochen Urlaub: Der naechste Start liegt weit ausserhalb der ersten Suchetappe.
            var ex = new List<DateRange> { new DateRange(T.Day(0), T.Day(20)) };
            Assert.Equal(T.Day(21, 8), Scheduler.NextChange(T.Mon(10), Office(), ex));
        }

        [Fact]
        public void NextChange_NothingForAYear_IsNull()
        {
            var ex = new List<DateRange> { new DateRange(T.Day(0), T.Day(500)) };
            Assert.Null(Scheduler.NextChange(T.Mon(10), Office(), ex));
        }

        [Fact]
        public void Overnight_StartingOnAnExcludedDay_IsSuppressedCompletely()
        {
            var ex = new List<DateRange> { new DateRange(T.Day(1), T.Day(1)) };      // Dienstag
            List<ScheduleRule> night = Night();
            Assert.True(Scheduler.IsActive(T.Mon(23), night, ex));          // Montagabend, nicht betroffen
            Assert.True(Scheduler.IsActive(T.Day(1, 3), night, ex));        // Dienstag 03:00 gehoert zum Montag-Fenster
            Assert.False(Scheduler.IsActive(T.Day(1, 23), night, ex));      // Dienstagabend entfaellt
            Assert.False(Scheduler.IsActive(T.Day(2, 3), night, ex));       // Mittwoch 03:00 gehoert zum Dienstag-Fenster
            Assert.True(Scheduler.IsActive(T.Day(2, 23), night, ex));
        }

        [Fact]
        public void NextChange_RespectsExceptionsForOvernightEnds()
        {
            var ex = new List<DateRange> { new DateRange(T.Day(1), T.Day(1)) };
            // Montag 23:00 aktiv -> endet Dienstag 06:00; danach beginnt erst am Mittwoch 22:00 wieder eines.
            Assert.Equal(T.Day(1, 6), Scheduler.NextChange(T.Mon(23), Night(), ex));
            Assert.Equal(T.Day(2, 22), Scheduler.NextChange(T.Day(1, 7), Night(), ex));
        }

        [Fact]
        public void Engine_WaitsOnAnExceptionDay_AndResumesTheNextDay()
        {
            var s = new Settings { SmartIdle = false, ScheduleEnabled = true };
            s.Rules.AddRange(Office());
            s.Exceptions.Add(new DateRange(T.Mon(), T.Mon()));
            var input = new FakeInput();
            var engine = new HolderEngine(input, s, null);

            engine.Start(T.Mon(10), "Test");
            Assert.Equal(HolderState.WaitingForWindow, engine.State);
            Assert.Equal(0, input.Sent);

            engine.Tick(T.Day(1, 8));
            Assert.Equal(HolderState.Active, engine.State);
            Assert.Equal(1, input.Sent);
        }

        [Fact]
        public void StatusLine_ShowsTheDayAfterTheVacation()
        {
            var s = new Settings { ScheduleEnabled = true };
            s.Rules.AddRange(Office());
            s.Exceptions.Add(new DateRange(T.Mon(), T.Day(3)));       // Mo-Do frei
            Assert.Equal("Zeitplan: nächster Start Fr 08:00", StatusBuilder.ScheduleLine(s, T.Mon(10)));
        }
    }
}
