using System;
using System.IO;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    public class StatusBuilderTests : IDisposable
    {
        public StatusBuilderTests()
        {
            Loc.Language = "de";
        }

        public void Dispose()
        {
            Loc.Language = "de";
        }

        static HolderEngine Engine(Settings s, FakeInput input)
        {
            return new HolderEngine(input, s, null);
        }

        [Fact]
        public void Stopped_ExplainsHowToStart()
        {
            var s = new Settings();
            StatusInfo info = StatusBuilder.Build(Engine(s, new FakeInput()), s, T.Mon(10), false);
            Assert.Equal(StatusKind.Stopped, info.Kind);
            Assert.Equal("Gestoppt", info.Title);
            Assert.Contains("Starten", info.Detail);
        }

        [Fact]
        public void Active_NeedsNoDetails_NoCountdownCounterOrRuntime()
        {
            var s = new Settings { SmartIdle = false, IntervalSeconds = 30 };
            var e = Engine(s, new FakeInput());
            e.Start(T.Mon(10), "x");
            StatusInfo info = StatusBuilder.Build(e, s, T.Mon(10, 0, 10), false);
            Assert.Equal(StatusKind.Active, info.Kind);
            Assert.Equal("Aktiv – dein Status bleibt grün", info.Title);
            Assert.Equal("", info.Detail);

            // Auch nach mehreren Aktionen und langer Laufzeit gibt es nichts, was mitzaehlt oder mitlaeuft.
            e.Tick(T.Mon(10, 0, 30));
            e.Tick(T.Mon(10, 1, 0));
            Assert.Equal("", StatusBuilder.Build(e, s, T.Mon(10, 1, 0), false).Detail);
            Assert.Equal("", StatusBuilder.Build(e, s, T.Mon(12, 0, 0), false).Detail);

            Loc.Language = "en";
            StatusInfo english = StatusBuilder.Build(e, s, T.Mon(10, 1, 0), false);
            Assert.Equal("Active – your status stays green", english.Title);
            Assert.Equal("", english.Detail);
        }

        [Fact]
        public void Smart_WhileUserIsActive_ShowsStandby_WithoutALiveIdleCounter()
        {
            var s = new Settings { SmartIdle = true, IntervalSeconds = 30 };
            var input = new FakeInput { Idle = TimeSpan.FromSeconds(7) };
            var e = Engine(s, input);
            e.Start(T.Mon(10), "x");
            StatusInfo info = StatusBuilder.Build(e, s, T.Mon(10), false);
            Assert.Equal(StatusKind.StandingBy, info.Kind);
            Assert.Equal("Greift ein, sobald du 30 s nichts tust.", info.Detail);

            // Der Leerlauf aendert sich von Sekunde zu Sekunde, der Text darf nicht mitticken.
            input.Idle = TimeSpan.FromSeconds(12);
            e.Tick(T.Mon(10, 0, 1));
            Assert.Equal(info.Detail, StatusBuilder.Build(e, s, T.Mon(10, 0, 1), false).Detail);

            Loc.Language = "en";
            Assert.Equal("Steps in once you are idle for 30 s.", StatusBuilder.Build(e, s, T.Mon(10, 0, 1), false).Detail);
        }

        [Theory]
        [InlineData(StatusKind.Active, true)]
        [InlineData(StatusKind.StandingBy, true)]
        [InlineData(StatusKind.Stopped, false)]
        [InlineData(StatusKind.Paused, false)]
        [InlineData(StatusKind.WaitingForWindow, false)]
        [InlineData(StatusKind.WaitingForTeams, false)]
        [InlineData(StatusKind.Blocked, false)]
        [InlineData(StatusKind.SessionLocked, false)]
        public void PauseButtons_AreOnlyOfferedWhereAPauseDoesSomething(StatusKind kind, bool offered)
        {
            Assert.Equal(offered, StatusBuilder.OffersPause(kind));
        }

        [Fact]
        public void Schedule_WaitingShowsNextStart()
        {
            var s = new Settings { ScheduleEnabled = true };
            s.Rules.Add(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
            var e = Engine(s, new FakeInput());
            e.Start(T.Mon(7), "x");
            StatusInfo info = StatusBuilder.Build(e, s, T.Mon(7), false);
            Assert.Equal(StatusKind.WaitingForWindow, info.Kind);
            Assert.Equal("Nächster Start: heute 08:00", info.Detail);
        }

        [Fact]
        public void TwoFailuresInARow_ShowBlocked_ButOneDoesNot()
        {
            var s = new Settings { SmartIdle = false, IntervalSeconds = 30 };
            var input = new FakeInput { Accept = false };
            var e = Engine(s, input);
            e.Start(T.Mon(10), "x");
            Assert.Equal(StatusKind.Active, StatusBuilder.Build(e, s, T.Mon(10), false).Kind);
            e.Tick(T.Mon(10, 0, 30));
            Assert.Equal(StatusKind.Blocked, StatusBuilder.Build(e, s, T.Mon(10, 0, 30), false).Kind);
        }

        [Fact]
        public void LockedSession_WinsOverEverythingWhileRunning()
        {
            var s = new Settings();
            var e = Engine(s, new FakeInput());
            e.Start(T.Mon(10), "x");
            Assert.Equal(StatusKind.SessionLocked, StatusBuilder.Build(e, s, T.Mon(10), true).Kind);

            var stopped = Engine(s, new FakeInput());
            Assert.Equal(StatusKind.Stopped, StatusBuilder.Build(stopped, s, T.Mon(10), true).Kind);
        }

        [Fact]
        public void English_IsUsedWhenSelected()
        {
            Loc.Language = "en";
            var s = new Settings();
            Assert.Equal("Stopped", StatusBuilder.Build(Engine(s, new FakeInput()), s, T.Mon(10), false).Title);
        }

        // ---- Planzeile / When ----

        [Fact]
        public void When_PicksTheShortestUnderstandableForm()
        {
            DateTime now = T.Mon(10);
            Assert.Equal("heute 17:00", StatusBuilder.When(T.Mon(17), now));
            Assert.Equal("morgen 08:00", StatusBuilder.When(T.Day(1, 8), now));
            Assert.Equal("Mi 08:00", StatusBuilder.When(T.Day(2, 8), now));
            Assert.Equal("So 09:30", StatusBuilder.When(T.Day(6, 9, 30), now));
            Assert.Equal("12.10. 08:00", StatusBuilder.When(T.Day(7, 8), now));
        }

        [Fact]
        public void When_UsesTheDateStyleOfTheSelectedLanguage()
        {
            DateTime now = T.Mon(10);
            Loc.Language = "en";
            Assert.Equal("today 17:00", StatusBuilder.When(T.Mon(17), now));
            Assert.Equal("Wed 08:00", StatusBuilder.When(T.Day(2, 8), now));
            Assert.Equal("Oct 12 08:00", StatusBuilder.When(T.Day(7, 8), now));
            Assert.Equal("Nov 2 08:00", StatusBuilder.When(T.Day(28, 8), now));
        }

        [Fact]
        public void PlanLine_CombinesScheduleAndAutoStop()
        {
            var s = new Settings { ScheduleEnabled = true, AutoStopEnabled = true };
            s.Rules.Add(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17)));
            string line = StatusBuilder.PlanLine(s, T.Mon(10), T.Mon(17));
            Assert.Equal("Zeitplan: aktiv bis heute 17:00   ·   Auto-Stopp: heute 17:00", line);
        }

        [Fact]
        public void PlanLine_IsEmptyWhenNothingIsPlanned()
        {
            Assert.Equal("", StatusBuilder.PlanLine(new Settings(), T.Mon(10), null));
        }

        [Fact]
        public void AutoStopLine_ExpiredOnceIsReported()
        {
            var s = new Settings { AutoStopEnabled = true };
            Assert.Contains("Vergangenheit", StatusBuilder.AutoStopLine(s, T.Mon(10), null));
            s.AutoStopEnabled = false;
            Assert.Null(StatusBuilder.AutoStopLine(s, T.Mon(10), null));
        }

        [Fact]
        public void ScheduleLine_NullWithoutActiveSchedule()
        {
            var s = new Settings { ScheduleEnabled = true };   // aktiviert, aber ohne Fenster
            Assert.Null(StatusBuilder.ScheduleLine(s, T.Mon(10)));
        }

        [Fact]
        public void ScheduleLine_AtNight_SaysTodayForTheEnd()
        {
            var s = new Settings { ScheduleEnabled = true };
            s.Rules.Add(new ScheduleRule(new[] { true, true, true, true, true, true, true }, T.Hm(22), T.Hm(6)));
            Assert.Equal("Zeitplan: aktiv bis heute 06:00", StatusBuilder.ScheduleLine(s, T.Day(1, 3)));
            Assert.Equal("Zeitplan: aktiv bis morgen 06:00", StatusBuilder.ScheduleLine(s, T.Day(1, 23)));
        }
    }

    public class RuleFormatterTests : IDisposable
    {
        public RuleFormatterTests()
        {
            Loc.Language = "de";
        }

        public void Dispose()
        {
            Loc.Language = "de";
        }

        static bool[] Days(string mask)
        {
            var d = new bool[7];
            for (int i = 0; i < 7; i++) d[i] = mask[i] == '1';
            return d;
        }

        [Theory]
        [InlineData("1111100", "Mo–Fr")]
        [InlineData("1111111", "täglich")]
        [InlineData("0000000", "kein Tag")]
        [InlineData("1010100", "Mo, Mi, Fr")]
        [InlineData("0000011", "Sa, So")]
        [InlineData("1100000", "Mo, Di")]
        [InlineData("0111000", "Di–Do")]
        [InlineData("1111001", "Mo–Do, So")]
        [InlineData("1001110", "Mo, Do–Sa")]
        public void Days_AreCompressed(string mask, string expected)
        {
            Assert.Equal(expected, RuleFormatter.Days(Days(mask)));
        }

        [Fact]
        public void Describe_ReadsAsSentence_WithNextDayHint()
        {
            Assert.Equal("Mo–Fr, 08:00 bis 17:00", RuleFormatter.Describe(ScheduleRule.Weekdays(T.Hm(8), T.Hm(17))));
            string night = RuleFormatter.Describe(new ScheduleRule(Days("0000100"), T.Hm(22), T.Hm(6)));
            Assert.Equal("Fr, 22:00 bis 06:00 am Folgetag", night);
            Assert.Equal("Täglich, 18:00 bis 24:00", RuleFormatter.Describe(new ScheduleRule(Days("1111111"), T.Hm(18), T.Hm(0))));
        }

        [Fact]
        public void Describe_English()
        {
            Loc.Language = "en";
            string night = RuleFormatter.Describe(new ScheduleRule(Days("0000100"), T.Hm(22), T.Hm(6)));
            Assert.Equal("Fri, 22:00 to 06:00 the next day", night);
            Assert.Equal("Daily, 08:00 to 17:00", RuleFormatter.Describe(new ScheduleRule(Days("1111111"), T.Hm(8), T.Hm(17))));
        }

        [Theory]
        [InlineData(8, 17, "08:00 – 17:00", false)]
        [InlineData(22, 6, "22:00 – 06:00", true)]
        [InlineData(18, 0, "18:00 – 24:00", false)]   // Ende um Mitternacht: kein Folgetag, sondern "24:00"
        [InlineData(0, 8, "00:00 – 08:00", false)]
        public void Times_AndNextDay(int start, int end, string expected, bool nextDay)
        {
            var rule = ScheduleRule.Weekdays(T.Hm(start), T.Hm(end));
            Assert.Equal(expected, RuleFormatter.Times(rule));
            Assert.Equal(nextDay, RuleFormatter.EndsNextDay(rule));
        }

        [Fact]
        public void DaysLabel_StartsWithCapital()
        {
            Assert.Equal("Täglich", RuleFormatter.DaysLabel(Days("1111111")));
            Assert.Equal("Mo–Fr", RuleFormatter.DaysLabel(Days("1111100")));
            Assert.Equal("Kein Tag", RuleFormatter.DaysLabel(Days("0000000")));
        }

        [Fact]
        public void Days_EnglishNames()
        {
            Loc.Language = "en";
            Assert.Equal("Mon–Fri", RuleFormatter.Days(Days("1111100")));
            Assert.Equal("daily", RuleFormatter.Days(Days("1111111")));
        }
    }

    public class HotkeyInfoTests : IDisposable
    {
        public HotkeyInfoTests()
        {
            Loc.Language = "de";
        }

        public void Dispose()
        {
            Loc.Language = "de";
        }

        [Theory]
        [InlineData("A", true, 0x41u)]
        [InlineData("G", true, 0x47u)]
        [InlineData("Z", true, 0x5Au)]
        [InlineData("F1", true, 0x70u)]
        [InlineData("F12", true, 0x7Bu)]
        [InlineData("F13", true, 0x7Cu)]
        [InlineData("F24", true, 0x87u)]
        [InlineData("F25", false, 0u)]
        [InlineData("F0", false, 0u)]
        [InlineData("1", false, 0u)]
        [InlineData("", false, 0u)]
        [InlineData(null, false, 0u)]
        [InlineData("a", false, 0u)]
        public void TryGetVirtualKey_MapsLettersAndFunctionKeys(string key, bool ok, uint expected)
        {
            uint vk;
            Assert.Equal(ok, HotkeyInfo.TryGetVirtualKey(key, out vk));
            if (ok) Assert.Equal(expected, vk);
        }

        [Fact]
        public void EveryAllowedKey_HasAVirtualKey()
        {
            foreach (string key in Settings.HotkeyKeys)
            {
                uint vk;
                Assert.True(HotkeyInfo.TryGetVirtualKey(key, out vk), key);
            }
        }

        [Fact]
        public void Describe_ListsModifiersInStableOrder()
        {
            var s = new Settings { HotkeyCtrl = true, HotkeyAlt = true, HotkeyShift = false, HotkeyWin = false, HotkeyKey = "G" };
            Assert.Equal("Strg+Alt+G", HotkeyInfo.Describe(s));
            s.HotkeyShift = true;
            s.HotkeyWin = true;
            s.HotkeyKey = "F9";
            Assert.Equal("Strg+Alt+Umschalt+Win+F9", HotkeyInfo.Describe(s));
            Loc.Language = "en";
            Assert.Equal("Ctrl+Alt+Shift+Win+F9", HotkeyInfo.Describe(s));
        }
    }

    public class CommandLineTests
    {
        [Fact]
        public void NoArguments_AllDefaults()
        {
            CommandLine c = CommandLine.Parse(new string[0]);
            Assert.False(c.Autostart || c.Minimized || c.Start || c.NoStart || c.Help);
            Assert.Null(c.SelfTestPath);
            Assert.Null(c.SettingsPath);
            Assert.Null(c.Language);
            Assert.NotNull(CommandLine.Parse(null));
        }

        [Fact]
        public void Flags_AreCaseInsensitive()
        {
            CommandLine c = CommandLine.Parse(new[] { "--START", "--Minimized", "--autostart", "--no-start", "-h" });
            Assert.True(c.Start);
            Assert.True(c.Minimized);
            Assert.True(c.Autostart);
            Assert.True(c.NoStart);
            Assert.True(c.Help);
        }

        [Fact]
        public void ValueOptions_AcceptSpaceAndEqualsForms()
        {
            CommandLine a = CommandLine.Parse(new[] { "--settings", @"D:\sg\my.ini", "--lang", "en", "--selftest", "out.txt" });
            Assert.Equal(@"D:\sg\my.ini", a.SettingsPath);
            Assert.Equal("en", a.Language);
            Assert.Equal("out.txt", a.SelfTestPath);
            Assert.False(a.SelfTestInteractive);     // Datei angegeben (CI): kein Fenster

            CommandLine b = CommandLine.Parse(new[] { "--settings=C:\\x y\\a.ini", "--lang=DE", "--selftest=r.txt" });
            Assert.Equal("C:\\x y\\a.ini", b.SettingsPath);
            Assert.Equal("de", b.Language);
            Assert.Equal("r.txt", b.SelfTestPath);
        }

        [Fact]
        public void SelfTest_WithoutPath_UsesTempFile_AndDoesNotSwallowNextFlag()
        {
            CommandLine c = CommandLine.Parse(new[] { "--selftest", "--start" });
            Assert.Equal(CommandLine.DefaultSelfTestPath, c.SelfTestPath);
            Assert.True(c.SelfTestInteractive);      // vom Nutzer gestartet: Ergebnis im Fenster zeigen
            Assert.True(c.Start);
            Assert.Equal(Path.Combine(Path.GetTempPath(), "staygreen-selftest.txt"), CommandLine.DefaultSelfTestPath);
        }

        // Auto-Stopp liegt auf der Seite "Zeitplan" (Index 1); die alten Namen und Nummern zeigen dorthin.
        [Theory]
        [InlineData("0", 0)]
        [InlineData("activity", 0)]
        [InlineData("Schedule", 1)]
        [InlineData("zeitplan", 1)]
        [InlineData("2", 1)]
        [InlineData("stop", 1)]
        [InlineData("auto-stopp", 1)]
        [InlineData("system", 2)]
        [InlineData("3", 2)]
        [InlineData("7", -1)]
        [InlineData("blah", -1)]
        public void Tab_AcceptsNamesAndNumbers(string text, int expected)
        {
            Assert.Equal(expected, CommandLine.Parse(new[] { "--tab", text }).Tab);
            Assert.Equal(expected, CommandLine.Parse(new[] { "--tab=" + text }).Tab);
        }

        [Fact]
        public void Tab_DefaultsToMinusOne()
        {
            Assert.Equal(-1, CommandLine.Parse(new string[0]).Tab);
            Assert.Equal(-1, CommandLine.Parse(new[] { "--tab" }).Tab);
        }

        [Fact]
        public void InvalidLanguage_IsIgnored()
        {
            Assert.Null(CommandLine.Parse(new[] { "--lang", "fr" }).Language);
        }

        [Fact]
        public void UnknownAndEmptyArguments_AreIgnored()
        {
            CommandLine c = CommandLine.Parse(new[] { "", "   ", "--whatever", "datei.txt", "--start" });
            Assert.True(c.Start);
        }
    }
}
