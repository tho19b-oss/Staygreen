using System;
using System.Globalization;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    public class AutoStopPlannerTests
    {
        static Settings Daily(int h, int m = 0)
        {
            return new Settings { AutoStopEnabled = true, AutoStopTiming = StopTiming.Daily, AutoStopTime = T.Hm(h, m) };
        }

        [Fact]
        public void Disabled_ReturnsNull()
        {
            var s = Daily(17);
            s.AutoStopEnabled = false;
            Assert.Null(AutoStopPlanner.NextDue(s, T.Mon(10)));
            Assert.Null(AutoStopPlanner.NextDue(null, T.Mon(10)));
        }

        [Fact]
        public void Daily_BeforeTime_IsToday()
        {
            Assert.Equal(T.Mon(17), AutoStopPlanner.NextDue(Daily(17), T.Mon(10)));
        }

        [Fact]
        public void Daily_AfterTime_IsTomorrow()
        {
            Assert.Equal(T.Day(1, 17), AutoStopPlanner.NextDue(Daily(17), T.Mon(18)));
        }

        [Fact]
        public void Daily_ExactlyAtTime_IsTomorrow()
        {
            // Direkt nach dem Ausloesen darf derselbe Termin nicht noch einmal faellig sein.
            Assert.Equal(T.Day(1, 17), AutoStopPlanner.NextDue(Daily(17), T.Mon(17)));
        }

        [Fact]
        public void Daily_JustBeforeMidnight_Works()
        {
            Assert.Equal(T.Day(1, 0, 5), AutoStopPlanner.NextDue(Daily(0, 5), T.Mon(23, 59)));
        }

        [Fact]
        public void Once_InFuture_IsThatMoment()
        {
            var s = new Settings { AutoStopEnabled = true, AutoStopTiming = StopTiming.Once, AutoStopOnce = T.Day(2, 9, 30) };
            Assert.Equal(T.Day(2, 9, 30), AutoStopPlanner.NextDue(s, T.Mon(10)));
        }

        [Fact]
        public void Once_InPastOrUnset_ReturnsNull()
        {
            var s = new Settings { AutoStopEnabled = true, AutoStopTiming = StopTiming.Once, AutoStopOnce = T.Mon(9) };
            Assert.Null(AutoStopPlanner.NextDue(s, T.Mon(10)));
            s.AutoStopOnce = DateTime.MinValue;
            Assert.Null(AutoStopPlanner.NextDue(s, T.Mon(10)));
        }

        [Fact]
        public void IsMissed_UsesTolerance()
        {
            DateTime due = T.Mon(17);
            Assert.False(AutoStopPlanner.IsMissed(due, due.AddSeconds(AutoStopPlanner.MissedToleranceSeconds)));
            Assert.True(AutoStopPlanner.IsMissed(due, due.AddSeconds(AutoStopPlanner.MissedToleranceSeconds + 1)));
            Assert.True(AutoStopPlanner.IsMissed(due, T.Day(1, 8)));  // Laptop am naechsten Morgen aufgeklappt
        }
    }

    public class SettingsTests
    {
        [Fact]
        public void Defaults_AreSensible()
        {
            var s = new Settings();
            Assert.Equal(ActivityMode.Both, s.Mode);
            Assert.Equal(30, s.IntervalSeconds);
            Assert.True(s.SmartIdle);
            Assert.True(s.KeepAwake);
            Assert.True(s.StartHoldingOnLaunch);
            Assert.False(s.StartWithWindows);   // Autostart nur auf ausdruecklichen Wunsch
            Assert.False(s.ScheduleEnabled);
            Assert.False(s.AutoStopEnabled);    // nie ungefragt herunterfahren
            Assert.False(s.StopShutdown);
            Assert.False(s.LogEnabled);
        }

        [Fact]
        public void RoundTrip_PreservesEverything()
        {
            var s = new Settings
            {
                Language = "en",
                Mode = ActivityMode.Mouse,
                IntervalSeconds = 45,
                MousePixels = 5,
                SmartIdle = false,
                KeepAwake = false,
                StartHoldingOnLaunch = false,
                StartWithWindows = true,
                StartMinimized = true,
                MinimizeToTray = false,
                HotkeyEnabled = false,
                HotkeyCtrl = false,
                HotkeyAlt = true,
                HotkeyShift = true,
                HotkeyWin = true,
                HotkeyKey = "F9",
                ScheduleEnabled = true,
                AutoStopEnabled = true,
                AutoStopTiming = StopTiming.Once,
                AutoStopTime = T.Hm(18, 15),
                AutoStopOnce = new DateTime(2026, 12, 24, 13, 30, 0),
                StopCloseTeams = true,
                StopLock = true,
                StopShutdown = true,
                StopExitApp = false,
                LogEnabled = true,
                LogPath = @"C:\Users\Max\Dokumente\sg log.txt",
            };
            s.Rules.Add(ScheduleRule.Weekdays(T.Hm(8), T.Hm(12)));
            s.Rules.Add(ScheduleRule.Weekdays(T.Hm(13), T.Hm(17, 30)));

            Settings p = Settings.Parse(s.Serialize());

            Assert.Equal(s.Language, p.Language);
            Assert.Equal(s.Mode, p.Mode);
            Assert.Equal(s.IntervalSeconds, p.IntervalSeconds);
            Assert.Equal(s.MousePixels, p.MousePixels);
            Assert.Equal(s.SmartIdle, p.SmartIdle);
            Assert.Equal(s.KeepAwake, p.KeepAwake);
            Assert.Equal(s.StartHoldingOnLaunch, p.StartHoldingOnLaunch);
            Assert.Equal(s.StartWithWindows, p.StartWithWindows);
            Assert.Equal(s.StartMinimized, p.StartMinimized);
            Assert.Equal(s.MinimizeToTray, p.MinimizeToTray);
            Assert.Equal(s.HotkeyEnabled, p.HotkeyEnabled);
            Assert.Equal(s.HotkeyCtrl, p.HotkeyCtrl);
            Assert.Equal(s.HotkeyAlt, p.HotkeyAlt);
            Assert.Equal(s.HotkeyShift, p.HotkeyShift);
            Assert.Equal(s.HotkeyWin, p.HotkeyWin);
            Assert.Equal(s.HotkeyKey, p.HotkeyKey);
            Assert.Equal(s.ScheduleEnabled, p.ScheduleEnabled);
            Assert.Equal(2, p.Rules.Count);
            Assert.Equal("1111100;08:00;12:00", p.Rules[0].Serialize());
            Assert.Equal("1111100;13:00;17:30", p.Rules[1].Serialize());
            Assert.Equal(s.AutoStopEnabled, p.AutoStopEnabled);
            Assert.Equal(s.AutoStopTiming, p.AutoStopTiming);
            Assert.Equal(s.AutoStopTime, p.AutoStopTime);
            Assert.Equal(s.AutoStopOnce, p.AutoStopOnce);
            Assert.Equal(s.StopCloseTeams, p.StopCloseTeams);
            Assert.Equal(s.StopLock, p.StopLock);
            Assert.Equal(s.StopShutdown, p.StopShutdown);
            Assert.Equal(s.StopExitApp, p.StopExitApp);
            Assert.Equal(s.LogEnabled, p.LogEnabled);
            Assert.Equal(s.LogPath, p.LogPath);
        }

        [Fact]
        public void Serialize_IsCultureIndependent()
        {
            var old = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var s = new Settings { AutoStopOnce = new DateTime(2026, 3, 4, 5, 6, 7), IntervalSeconds = 1234 };
                s.Normalize();
                string text = s.Serialize();
                Assert.Contains("AutoStopOnce=2026-03-04T05:06:07", text);
                Assert.Contains("IntervalSeconds=600", text);   // auf Maximum begrenzt, ohne Tausenderpunkt
            }
            finally
            {
                CultureInfo.CurrentCulture = old;
            }
        }

        [Fact]
        public void Parse_IgnoresGarbageAndKeepsDefaults()
        {
            string text = "foo\r\n=bar\r\n; Kommentar\r\n# noch einer\r\nIntervalSeconds=abc\r\nMode=7\r\nMode=-1\r\n"
                          + "Unknown=1\r\nSmartIdle=vielleicht\r\nRule=kaputt\r\nAutoStopOnce=gestern\r\nAutoStopTime=99:99\r\n";
            Settings s = Settings.Parse(text);
            var d = new Settings();
            Assert.Equal(d.IntervalSeconds, s.IntervalSeconds);
            Assert.Equal(d.Mode, s.Mode);
            Assert.Equal(d.SmartIdle, s.SmartIdle);
            Assert.Empty(s.Rules);
            Assert.Equal(DateTime.MinValue, s.AutoStopOnce);
            Assert.Equal(d.AutoStopTime, s.AutoStopTime);
        }

        [Fact]
        public void Parse_NullOrEmpty_GivesDefaults()
        {
            Assert.Equal(new Settings().IntervalSeconds, Settings.Parse(null).IntervalSeconds);
            Assert.Equal(new Settings().IntervalSeconds, Settings.Parse("").IntervalSeconds);
        }

        [Fact]
        public void Parse_AcceptsUnixAndWindowsLineEndings_AndCaseInsensitiveKeys()
        {
            Settings s = Settings.Parse("intervalseconds=77\nMODE=key\r\nsmartidle=0\n");
            Assert.Equal(77, s.IntervalSeconds);
            Assert.Equal(ActivityMode.Key, s.Mode);
            Assert.False(s.SmartIdle);
        }

        [Fact]
        public void Normalize_ClampsValues()
        {
            var s = new Settings { IntervalSeconds = 1, MousePixels = 99, Language = "fr", HotkeyKey = "%" };
            s.Normalize();
            Assert.Equal(Settings.MinInterval, s.IntervalSeconds);
            Assert.Equal(Settings.MaxMousePixels, s.MousePixels);
            Assert.Equal("auto", s.Language);
            Assert.Equal("G", s.HotkeyKey);

            s.IntervalSeconds = 100000;
            s.MousePixels = -3;
            s.Normalize();
            Assert.Equal(Settings.MaxInterval, s.IntervalSeconds);
            Assert.Equal(Settings.MinMousePixels, s.MousePixels);
        }

        [Fact]
        public void Normalize_NeverAllowsHotkeyWithoutModifier()
        {
            var s = new Settings { HotkeyCtrl = false, HotkeyAlt = false, HotkeyShift = false, HotkeyWin = false, HotkeyKey = "a" };
            s.Normalize();
            Assert.True(s.HotkeyCtrl && s.HotkeyAlt);
            Assert.Equal("A", s.HotkeyKey);
        }

        [Fact]
        public void Normalize_DropsUnusableRules()
        {
            var s = new Settings();
            s.Rules.Add(ScheduleRule.Weekdays(T.Hm(8), T.Hm(8)));
            s.Rules.Add(new ScheduleRule());
            s.Rules.Add(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
            s.Normalize();
            Assert.Single(s.Rules);
        }

        [Fact]
        public void ScheduleActive_NeedsEnabledAndUsableRule()
        {
            var s = new Settings { ScheduleEnabled = true };
            Assert.False(s.ScheduleActive);   // eingeschaltet, aber keine Fenster -> nicht aussperren
            s.Rules.Add(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
            Assert.True(s.ScheduleActive);
            s.ScheduleEnabled = false;
            Assert.False(s.ScheduleActive);
        }

        [Fact]
        public void HotkeyKeys_AreUniqueAndComplete()
        {
            Assert.Equal(26 + 24, Settings.HotkeyKeys.Length);   // A-Z und F1-F24
            Assert.Contains("F13", Settings.HotkeyKeys);
            Assert.Contains("F24", Settings.HotkeyKeys);
            Assert.Equal(Settings.HotkeyKeys.Length, new System.Collections.Generic.HashSet<string>(Settings.HotkeyKeys).Count);
        }
    }
}
