using System;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    public class SettingsFeatureTests : IDisposable
    {
        public SettingsFeatureTests()
        {
            Loc.Language = "de";
        }

        public void Dispose()
        {
            Loc.Language = "de";
        }

        [Fact]
        public void NewDefaults_AreConservative()
        {
            var s = new Settings();
            Assert.Equal(ActivityKey.F15, s.InputKey);
            Assert.False(s.OnlyWhileTeamsRuns);
            Assert.Equal(60, s.AutoStopWarnSeconds);
            Assert.Equal(15, s.AutoStopSnoozeMinutes);
            Assert.Equal("auto", s.Theme);
            Assert.False(s.NoticeAccepted);
        }

        [Fact]
        public void NewFields_RoundTrip()
        {
            var s = new Settings
            {
                InputKey = ActivityKey.Shift,
                OnlyWhileTeamsRuns = true,
                AutoStopTiming = StopTiming.ScheduleEnd,
                AutoStopWarnSeconds = 90,
                AutoStopSnoozeMinutes = 20,
                Theme = "dark",
                NoticeAccepted = true,
            };

            Settings p = Settings.Parse(s.Serialize());

            Assert.Equal(ActivityKey.Shift, p.InputKey);
            Assert.True(p.OnlyWhileTeamsRuns);
            Assert.Equal(StopTiming.ScheduleEnd, p.AutoStopTiming);
            Assert.Equal(90, p.AutoStopWarnSeconds);
            Assert.Equal(20, p.AutoStopSnoozeMinutes);
            Assert.Equal("dark", p.Theme);
            Assert.True(p.NoticeAccepted);
        }

        [Fact]
        public void SettingsFileOfAnOlderVersion_StillLoads_WithDefaultsForNewKeys()
        {
            string old = "Version=1\r\nIntervalSeconds=45\r\nMode=Key\r\nScheduleEnabled=true\r\nRule=1111100;08:00;17:00\r\n"
                         + "AutoStopEnabled=true\r\nAutoStopTiming=Daily\r\nAutoStopTime=17:30\r\n";
            Settings s = Settings.Parse(old);
            Assert.Equal(45, s.IntervalSeconds);
            Assert.Equal(ActivityKey.F15, s.InputKey);
            Assert.Equal(60, s.AutoStopWarnSeconds);
            Assert.Single(s.Rules);
        }

        [Fact]
        public void SettingsFile_WithRemovedFeatures_StillLoads_AndForgetsThem()
        {
            // Bis 1.3.0 gab es das Sicherheitsnetz (MaxRuntimeHours) und Ausnahmetage (Exception). Der 24.12.2026 ist ein
            // Donnerstag: Der alte Urlaubseintrag darf das Zeitfenster nicht mehr unsichtbar abschalten.
            string old = "Version=1\r\nIntervalSeconds=45\r\nMaxRuntimeHours=8\r\nScheduleEnabled=true\r\n"
                         + "Rule=1111100;08:00;17:00\r\nException=2026-12-24;2026-12-31\r\n";
            Settings s = Settings.Parse(old);
            Assert.Equal(45, s.IntervalSeconds);
            Assert.Single(s.Rules);
            Assert.True(Scheduler.IsActive(new DateTime(2026, 12, 24, 10, 0, 0), s.Rules));

            string saved = s.Serialize();
            Assert.DoesNotContain("MaxRuntimeHours", saved);
            Assert.DoesNotContain("Exception", saved);
        }

        [Fact]
        public void Parse_IgnoresGarbageInNewKeys()
        {
            Settings s = Settings.Parse("InputKey=7\r\nInputKey=F99\r\nExtra=1\r\n"
                                        + "AutoStopWarnSeconds=\r\nTheme=bunt\r\nOnlyWhileTeamsRuns=vielleicht\r\n");
            Assert.Equal(ActivityKey.F15, s.InputKey);
            Assert.Equal(60, s.AutoStopWarnSeconds);
            Assert.Equal("auto", s.Theme);
            Assert.False(s.OnlyWhileTeamsRuns);
        }

        [Fact]
        public void Parse_AcceptsKeyNamesCaseInsensitively()
        {
            Assert.Equal(ActivityKey.F20, Settings.Parse("inputkey=f20").InputKey);
            Assert.Equal(ActivityKey.Shift, Settings.Parse("InputKey=SHIFT").InputKey);
        }

        [Fact]
        public void Normalize_ClampsTheNewValues()
        {
            var s = new Settings
            {
                AutoStopWarnSeconds = 1,
                AutoStopSnoozeMinutes = 9999,
                Theme = " DARK ",
                InputKey = (ActivityKey)99,
            };
            s.Normalize();
            Assert.Equal(Settings.MinWarnSeconds, s.AutoStopWarnSeconds);
            Assert.Equal(Settings.MaxSnoozeMinutes, s.AutoStopSnoozeMinutes);
            Assert.Equal("dark", s.Theme);
            Assert.Equal(ActivityKey.F15, s.InputKey);

            s.AutoStopWarnSeconds = 100000;
            s.Theme = null;
            s.Normalize();
            Assert.Equal(Settings.MaxWarnSeconds, s.AutoStopWarnSeconds);
            Assert.Equal("auto", s.Theme);
        }

        [Theory]
        [InlineData(5, false)]
        [InlineData(30, false)]
        [InlineData(239, false)]
        [InlineData(240, true)]
        [InlineData(300, true)]
        [InlineData(600, true)]
        public void IntervalRisk_StartsAtFourMinutes(int seconds, bool risky)
        {
            Assert.Equal(risky, Settings.IsIntervalRisky(seconds));
        }

        [Fact]
        public void ActivityKey_OrderMatchesTheFunctionKeys()
        {
            // Die Plattformschicht berechnet den Tastencode aus der Position (F13 = 0x7C).
            Assert.Equal(0, (int)ActivityKey.F13);
            Assert.Equal(2, (int)ActivityKey.F15);
            Assert.Equal(11, (int)ActivityKey.F24);
            Assert.Equal(12, (int)ActivityKey.Shift);
        }
    }

    public class RemoteCommandTests
    {
        [Theory]
        [InlineData(RemoteAction.Show, "show")]
        [InlineData(RemoteAction.Start, "start")]
        [InlineData(RemoteAction.Stop, "stop")]
        [InlineData(RemoteAction.Toggle, "toggle")]
        [InlineData(RemoteAction.Resume, "resume")]
        public void SimpleActions_RoundTrip(RemoteAction action, string line)
        {
            var c = new RemoteCommand(action);
            Assert.Equal(line, c.ToLine());
            RemoteCommand parsed;
            Assert.True(RemoteCommand.TryParse(line, out parsed));
            Assert.Equal(action, parsed.Action);
        }

        [Fact]
        public void Pause_RoundTripsWithMinutes()
        {
            var c = new RemoteCommand(RemoteAction.Pause, 45);
            Assert.Equal("pause 45", c.ToLine());
            RemoteCommand parsed;
            Assert.True(RemoteCommand.TryParse("pause 45", out parsed));
            Assert.Equal(RemoteAction.Pause, parsed.Action);
            Assert.Equal(45, parsed.Minutes);
        }

        [Theory]
        [InlineData("pause", 30)]
        [InlineData("PAUSE   7 ", 7)]
        [InlineData("\tpause\t90", 90)]
        [InlineData("pause 0", 30)]
        [InlineData("pause 99999", 1440)]
        public void Pause_IsParsedTolerantlyAndClamped(string line, int minutes)
        {
            RemoteCommand parsed;
            Assert.True(RemoteCommand.TryParse(line, out parsed));
            Assert.Equal(minutes, parsed.Minutes);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("fliegen")]
        [InlineData("pause x")]
        [InlineData("pause -5")]
        [InlineData("start jetzt")]
        [InlineData("pause 5 6")]
        public void Garbage_IsRejected(string line)
        {
            RemoteCommand parsed;
            Assert.False(RemoteCommand.TryParse(line, out parsed));
            Assert.Null(parsed);
        }

        [Fact]
        public void MinutesAreOnlyKeptForPause()
        {
            Assert.Equal(0, new RemoteCommand(RemoteAction.Start, 15).Minutes);
            Assert.Equal(30, new RemoteCommand(RemoteAction.Pause, 0).Minutes);
            Assert.Equal(1440, new RemoteCommand(RemoteAction.Pause, 1000000).Minutes);
        }
    }

    public class CommandLineRemoteTests
    {
        [Fact]
        public void NoCommand_MeansOnlyShowTheWindow()
        {
            CommandLine c = CommandLine.Parse(new[] { "--minimized", "--lang", "en" });
            Assert.Null(c.Remote);
            Assert.False(c.StartRequested);
            Assert.False(c.NoStartRequested);
        }

        [Fact]
        public void Start_IsAlsoARemoteCommand()
        {
            CommandLine c = CommandLine.Parse(new[] { "--start" });
            Assert.Equal(RemoteAction.Start, c.Remote.Action);
            Assert.True(c.StartRequested);
        }

        [Fact]
        public void Stop_StartsWithoutHolding_WhenNoInstanceRuns()
        {
            CommandLine c = CommandLine.Parse(new[] { "--stop" });
            Assert.Equal(RemoteAction.Stop, c.Remote.Action);
            Assert.True(c.NoStartRequested);
            Assert.False(c.StartRequested);
        }

        [Fact]
        public void Toggle_StartsHolding_WhenNoInstanceRuns()
        {
            CommandLine c = CommandLine.Parse(new[] { "--toggle" });
            Assert.Equal(RemoteAction.Toggle, c.Remote.Action);
            Assert.True(c.StartRequested);
        }

        [Fact]
        public void Resume_IsParsed()
        {
            Assert.Equal(RemoteAction.Resume, CommandLine.Parse(new[] { "--resume" }).Remote.Action);
        }

        [Theory]
        [InlineData("--pause", 30)]
        [InlineData("--pause=15", 15)]
        [InlineData("--PAUSE=120", 120)]
        public void Pause_Forms(string arg, int minutes)
        {
            CommandLine c = CommandLine.Parse(new[] { arg });
            Assert.Equal(RemoteAction.Pause, c.Remote.Action);
            Assert.Equal(minutes, c.Remote.Minutes);
            Assert.False(c.StartRequested);                 // eine Pause wirkt nur auf ein laufendes Aktivhalten
            Assert.False(c.NoStartRequested);
        }

        [Fact]
        public void Pause_TakesTheNextNumber_ButNotTheNextOption()
        {
            CommandLine a = CommandLine.Parse(new[] { "--pause", "45", "--minimized" });
            Assert.Equal(45, a.Remote.Minutes);
            Assert.True(a.Minimized);

            CommandLine b = CommandLine.Parse(new[] { "--pause", "--minimized" });
            Assert.Equal(30, b.Remote.Minutes);
            Assert.True(b.Minimized);

            CommandLine c = CommandLine.Parse(new[] { "--pause", "bald", "--start" });
            Assert.Equal(RemoteAction.Start, c.Remote.Action);      // "bald" ist keine Zahl und bleibt unbeachtet
        }

        [Fact]
        public void TheLastCommandWins()
        {
            Assert.Equal(RemoteAction.Stop, CommandLine.Parse(new[] { "--start", "--stop" }).Remote.Action);
            Assert.Equal(RemoteAction.Start, CommandLine.Parse(new[] { "--stop", "--start" }).Remote.Action);
        }

        [Theory]
        [InlineData("--theme", "dark", "dark")]
        [InlineData("--theme=LIGHT", null, "light")]
        [InlineData("--theme", "auto", "auto")]
        [InlineData("--theme", "bunt", null)]
        public void Theme_IsValidated(string first, string second, string expected)
        {
            string[] args = second == null ? new[] { first } : new[] { first, second };
            Assert.Equal(expected, CommandLine.Parse(args).Theme);
        }
    }
}
