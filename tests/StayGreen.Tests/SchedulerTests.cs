using System;
using System.Collections.Generic;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    public class ScheduleRuleTests
    {
        [Fact]
        public void DayIndex_MapsMondayToZeroAndSundayToSix()
        {
            Assert.Equal(0, ScheduleRule.DayIndex(DayOfWeek.Monday));
            Assert.Equal(4, ScheduleRule.DayIndex(DayOfWeek.Friday));
            Assert.Equal(5, ScheduleRule.DayIndex(DayOfWeek.Saturday));
            Assert.Equal(6, ScheduleRule.DayIndex(DayOfWeek.Sunday));
        }

        [Fact]
        public void Serialize_RoundTrips()
        {
            var rule = ScheduleRule.Weekdays(T.Hm(8, 30), T.Hm(17));
            Assert.Equal("1111100;08:30;17:00", rule.Serialize());

            ScheduleRule parsed;
            Assert.True(ScheduleRule.TryParse(rule.Serialize(), out parsed));
            Assert.Equal(rule.Days, parsed.Days);
            Assert.Equal(rule.Start, parsed.Start);
            Assert.Equal(rule.End, parsed.End);
        }

        [Theory]
        [InlineData("")]
        [InlineData("1111100;08:00")]
        [InlineData("111110;08:00;17:00")]
        [InlineData("1111102;08:00;17:00")]
        [InlineData("1111100;8;17:00")]
        [InlineData("1111100;25:00;17:00")]
        [InlineData("1111100;08:60;17:00")]
        [InlineData("1111100;aa:bb;17:00")]
        public void TryParse_RejectsGarbage(string text)
        {
            ScheduleRule parsed;
            Assert.False(ScheduleRule.TryParse(text, out parsed));
        }

        [Theory]
        [InlineData("8:00", true, 8, 0)]
        [InlineData("08:05", true, 8, 5)]
        [InlineData("23:59", true, 23, 59)]
        [InlineData("00:00", true, 0, 0)]
        [InlineData("24:00", false, 0, 0)]
        [InlineData("-1:00", false, 0, 0)]
        [InlineData("12", false, 0, 0)]
        public void TryParseTime_Works(string text, bool ok, int h, int m)
        {
            TimeSpan t;
            Assert.Equal(ok, ScheduleRule.TryParseTime(text, out t));
            if (ok) Assert.Equal(new TimeSpan(h, m, 0), t);
        }

        [Fact]
        public void IsUsable_RequiresDaysAndDifferentTimes()
        {
            Assert.False(new ScheduleRule().IsUsable);
            Assert.False(new ScheduleRule(new[] { true, false, false, false, false, false, false }, T.Hm(8), T.Hm(8)).IsUsable);
            Assert.True(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)).IsUsable);
        }
    }

    public class SchedulerTests
    {
        static List<ScheduleRule> Office()
        {
            return new List<ScheduleRule> { ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)) };
        }

        static List<ScheduleRule> Night()
        {
            return new List<ScheduleRule>
            {
                new ScheduleRule(new[] { true, true, true, true, true, true, true }, T.Hm(22), T.Hm(6)),
            };
        }

        [Theory]
        [InlineData(0, 7, 59, false)]
        [InlineData(0, 8, 0, true)]
        [InlineData(0, 12, 30, true)]
        [InlineData(0, 16, 59, true)]
        [InlineData(0, 17, 0, false)]
        [InlineData(4, 10, 0, true)]   // Freitag
        [InlineData(5, 10, 0, false)]  // Samstag
        [InlineData(6, 10, 0, false)]  // Sonntag
        public void IsActive_OfficeHours(int dayOffset, int hour, int minute, bool expected)
        {
            Assert.Equal(expected, Scheduler.IsActive(T.Day(dayOffset, hour, minute), Office()));
        }

        [Fact]
        public void IsActive_EndIsExclusiveEvenWithSeconds()
        {
            Assert.True(Scheduler.IsActive(T.Mon(16, 59, 59), Office()));
            Assert.False(Scheduler.IsActive(T.Mon(17, 0, 0), Office()));
        }

        [Fact]
        public void IsActive_NullOrEmptyIsFalse()
        {
            Assert.False(Scheduler.IsActive(T.Mon(10), null));
            Assert.False(Scheduler.IsActive(T.Mon(10), new List<ScheduleRule>()));
        }

        [Fact]
        public void IsActive_OvernightWindowSpansMidnight()
        {
            // Nachtschicht Freitag 22:00 bis Samstag 06:00
            var rules = new List<ScheduleRule>
            {
                new ScheduleRule(new[] { false, false, false, false, true, false, false }, T.Hm(22), T.Hm(6)),
            };
            Assert.False(Scheduler.IsActive(T.Day(4, 21, 59), rules));
            Assert.True(Scheduler.IsActive(T.Day(4, 22, 0), rules));
            Assert.True(Scheduler.IsActive(T.Day(4, 23, 59), rules));
            Assert.True(Scheduler.IsActive(T.Day(5, 0, 0), rules));
            Assert.True(Scheduler.IsActive(T.Day(5, 5, 59), rules));
            Assert.False(Scheduler.IsActive(T.Day(5, 6, 0), rules));
            Assert.False(Scheduler.IsActive(T.Day(5, 22, 30), rules));  // Samstag nicht markiert
            Assert.False(Scheduler.IsActive(T.Day(4, 5, 0), rules));    // Freitag frueh gehoert zu Donnerstag
        }

        [Fact]
        public void IsActive_WindowEndingAtMidnight()
        {
            var rules = new List<ScheduleRule> { ScheduleRule.Weekdays(T.Hm(18), T.Hm(0)) };
            Assert.True(Scheduler.IsActive(T.Mon(23, 59), rules));
            Assert.False(Scheduler.IsActive(T.Day(1, 0, 0), rules));  // Dienstag 00:00 ist vorbei
        }

        [Fact]
        public void IsActive_EqualStartAndEndNeverActive()
        {
            var rules = new List<ScheduleRule> { ScheduleRule.Weekdays(T.Hm(8), T.Hm(8)) };
            Assert.False(Scheduler.IsActive(T.Mon(8), rules));
            Assert.False(Scheduler.IsActive(T.Mon(12), rules));
        }

        [Fact]
        public void IsActive_LunchBreakWithTwoWindows()
        {
            var rules = new List<ScheduleRule>
            {
                ScheduleRule.Weekdays(T.Hm(8), T.Hm(12)),
                ScheduleRule.Weekdays(T.Hm(13), T.Hm(17)),
            };
            Assert.True(Scheduler.IsActive(T.Mon(11, 59), rules));
            Assert.False(Scheduler.IsActive(T.Mon(12, 30), rules));
            Assert.True(Scheduler.IsActive(T.Mon(13, 0), rules));
        }

        // ---- NextChange ----

        [Fact]
        public void NextChange_BeforeWindow_IsStart()
        {
            Assert.Equal(T.Mon(8), Scheduler.NextChange(T.Mon(7), Office()));
        }

        [Fact]
        public void NextChange_InsideWindow_IsEnd()
        {
            Assert.Equal(T.Mon(17), Scheduler.NextChange(T.Mon(10), Office()));
        }

        [Fact]
        public void NextChange_AfterFridayWindow_IsNextMonday()
        {
            Assert.Equal(T.Day(7, 8), Scheduler.NextChange(T.Day(4, 18), Office()));
        }

        [Fact]
        public void NextChange_OnWeekend_IsMondayMorning()
        {
            Assert.Equal(T.Day(7, 8), Scheduler.NextChange(T.Day(5, 12), Office()));
            Assert.Equal(T.Day(7, 8), Scheduler.NextChange(T.Day(6, 12), Office()));
        }

        [Fact]
        public void NextChange_AtExactStart_ReturnsEndNotSameInstant()
        {
            Assert.Equal(T.Mon(17), Scheduler.NextChange(T.Mon(8), Office()));
        }

        [Fact]
        public void NextChange_AdjacentWindowsAreMerged()
        {
            var rules = new List<ScheduleRule>
            {
                ScheduleRule.Weekdays(T.Hm(8), T.Hm(12)),
                ScheduleRule.Weekdays(T.Hm(12), T.Hm(17)),
            };
            Assert.Equal(T.Mon(17), Scheduler.NextChange(T.Mon(9), rules));
        }

        [Fact]
        public void NextChange_OverlappingWindowsAreMerged()
        {
            var rules = new List<ScheduleRule>
            {
                ScheduleRule.Weekdays(T.Hm(8), T.Hm(14)),
                ScheduleRule.Weekdays(T.Hm(12), T.Hm(17)),
            };
            Assert.Equal(T.Mon(17), Scheduler.NextChange(T.Mon(9), rules));
        }

        [Fact]
        public void NextChange_OvernightWindowEndsNextMorning()
        {
            var rules = new List<ScheduleRule>
            {
                new ScheduleRule(new[] { false, false, false, false, true, false, false }, T.Hm(22), T.Hm(6)),
            };
            Assert.Equal(T.Day(5, 6), Scheduler.NextChange(T.Day(4, 23), rules));
            Assert.Equal(T.Day(4, 22), Scheduler.NextChange(T.Mon(9), rules));
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
        public void NextChange_WindowOnceAWeek_IsFoundAlmostAWeekAhead()
        {
            // Nur montags 00:00-23:00: Am Montagabend liegt der naechste Start fast eine Woche entfernt.
            var monday = new List<ScheduleRule>
            {
                new ScheduleRule(new[] { true, false, false, false, false, false, false }, T.Hm(0), T.Hm(23)),
            };
            Assert.Equal(T.Day(7, 0), Scheduler.NextChange(T.Mon(23), monday));
            Assert.Equal(T.Day(7, 0), Scheduler.NextChange(T.Mon(23, 59, 59), monday));
            Assert.Equal(T.Day(7, 23), Scheduler.NextChange(T.Day(7, 0), monday));
        }

        [Fact]
        public void NextChange_NoUsableRules_IsNull()
        {
            Assert.Null(Scheduler.NextChange(T.Mon(9), null));
            Assert.Null(Scheduler.NextChange(T.Mon(9), new List<ScheduleRule>()));
            Assert.Null(Scheduler.NextChange(T.Mon(9), new List<ScheduleRule> { new ScheduleRule() }));
        }

        [Fact]
        public void NextChange_AroundTheClockNeverChanges()
        {
            // Zwei Fenster, die zusammen den ganzen Tag abdecken (00:00-12:00 und 12:00-00:00): kein Wechsel.
            var rules = new List<ScheduleRule>
            {
                new ScheduleRule(new[] { true, true, true, true, true, true, true }, T.Hm(0), T.Hm(12)),
                new ScheduleRule(new[] { true, true, true, true, true, true, true }, T.Hm(12), T.Hm(0)),
            };
            Assert.True(Scheduler.IsActive(T.Mon(12), rules));
            Assert.True(Scheduler.IsActive(T.Mon(23, 59), rules));
            Assert.Null(Scheduler.NextChange(T.Mon(9), rules));
        }
    }
}
