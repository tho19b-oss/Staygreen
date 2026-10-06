using System;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    /// <summary>"Eingabe testen": bestaetigt, dass Windows die Eingabe annimmt UND als Aktivitaet wertet.</summary>
    public class InputTestTests
    {
        /// <summary>
        /// Simuliertes Windows mit laufender Uhr. Wie in echt tickt die Uhr grob (16 ms, mit beliebiger Phase), eine
        /// Wartezeit kann zu kurz ausfallen, und der Nutzer hat den Leerlaufzaehler gerade selbst zurueckgesetzt
        /// (der Klick auf "Jetzt testen").
        /// </summary>
        sealed class SimulatedWindows : IInputBackend
        {
            static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(16);

            readonly TimeSpan _phase;
            readonly TimeSpan _earlyWake;
            TimeSpan _countsAt = TimeSpan.MaxValue;

            public TimeSpan Now = TimeSpan.FromSeconds(10);
            public readonly TimeSpan Start;
            public TimeSpan LastInput;
            public TimeSpan? SentAt;

            /// <summary>SendInput nimmt die Eingabe an.</summary>
            public bool Accept = true;

            /// <summary>Eine angenommene Eingabe setzt den Leerlaufzaehler zurueck.</summary>
            public bool Counts = true;

            /// <summary>So lange braucht Windows, bis die angenommene Eingabe zaehlt.</summary>
            public TimeSpan Delay = TimeSpan.Zero;

            public int Sent;
            public ActivityMode Mode;
            public int Pixels;
            public ActivityKey Key;

            public SimulatedWindows(int phaseMs = 0, int clickAgeMs = 0, int earlyWakeMs = 0)
            {
                _phase = TimeSpan.FromMilliseconds(phaseMs);
                _earlyWake = TimeSpan.FromMilliseconds(earlyWakeMs);
                Start = Now;
                LastInput = Now - TimeSpan.FromMilliseconds(clickAgeMs);
            }

            public TimeSpan Clock()
            {
                return Now;
            }

            public void Wait(TimeSpan duration)
            {
                Now += duration - _earlyWake;
            }

            public bool SendActivity(ActivityMode mode, int mousePixels, ActivityKey key)
            {
                if (!Accept) return false;
                Sent++;
                SentAt = Now;
                Mode = mode;
                Pixels = mousePixels;
                Key = key;
                if (Counts) _countsAt = Now + Delay;
                return true;
            }

            public TimeSpan GetIdleTime()
            {
                if (_countsAt <= Now)
                {
                    LastInput = _countsAt;
                    _countsAt = TimeSpan.MaxValue;
                }
                return Floor(Now) - Floor(LastInput);
            }

            public void SetKeepAwake(bool keepAwake)
            {
            }

            TimeSpan Floor(TimeSpan t)
            {
                return TimeSpan.FromTicks((t + _phase).Ticks / Tick.Ticks * Tick.Ticks);
            }
        }

        static InputTestResult Run(SimulatedWindows windows)
        {
            return InputTest.Run(windows, new Settings(), windows.Clock, windows.Wait);
        }

        [Theory]
        [InlineData(0, 0, 0, 0)]        // Phase des Zeitgebers, Alter des Klicks, zu frueh endende Wartezeit, Verzoegerung von Windows (je in ms)
        [InlineData(7, 3, 15, 5)]
        [InlineData(15, 0, 15, 0)]
        [InlineData(11, 20, 0, 100)]    // traege Maschine: die Eingabe zaehlt erst nach 100 ms
        public void InputThatCounts_IsConfirmed(int phase, int clickAge, int earlyWake, int delay)
        {
            var windows = new SimulatedWindows(phase, clickAge, earlyWake) { Delay = TimeSpan.FromMilliseconds(delay) };
            Assert.Equal(InputTestResult.Confirmed, Run(windows));
            Assert.Equal(1, windows.Sent);
        }

        [Theory]
        [InlineData(0, 0, 0)]           // Der Klick hat den Zaehler gerade zurueckgesetzt: Das darf eine wirkungslose Eingabe nicht verdecken.
        [InlineData(7, 3, 15)]
        [InlineData(15, 0, 15)]
        [InlineData(9, 40, 0)]
        public void AcceptedInputWithoutEffect_IsNotCounted(int phase, int clickAge, int earlyWake)
        {
            var windows = new SimulatedWindows(phase, clickAge, earlyWake) { Counts = false };
            Assert.Equal(InputTestResult.NotCounted, Run(windows));
            Assert.Equal(1, windows.Sent);
        }

        [Fact]
        public void RejectedInput_IsRejected()
        {
            var windows = new SimulatedWindows { Accept = false };
            Assert.Equal(InputTestResult.Rejected, Run(windows));
            Assert.Equal(0, windows.Sent);
        }

        [Fact]
        public void SendsWithTheCurrentSettings()
        {
            var windows = new SimulatedWindows();
            var settings = new Settings { Mode = ActivityMode.Both, MousePixels = 7, InputKey = ActivityKey.F20 };
            InputTest.Run(windows, settings, windows.Clock, windows.Wait);

            Assert.Equal(1, windows.Sent);
            Assert.Equal(ActivityMode.Both, windows.Mode);
            Assert.Equal(7, windows.Pixels);
            Assert.Equal(ActivityKey.F20, windows.Key);
        }

        [Fact]
        public void WaitsBeforeSending_SoTheClickCannotMaskTheResult()
        {
            var windows = new SimulatedWindows();
            Run(windows);
            Assert.Equal(windows.Start + InputTest.Settle, windows.SentAt.Value);
        }

        [Fact]
        public void MissingArguments_AreRejected()
        {
            var windows = new SimulatedWindows();
            Assert.Throws<ArgumentNullException>(() => InputTest.Run(null, new Settings(), windows.Clock, windows.Wait));
            Assert.Throws<ArgumentNullException>(() => InputTest.Run(windows, null, windows.Clock, windows.Wait));
            Assert.Throws<ArgumentNullException>(() => InputTest.Run(windows, new Settings(), null, windows.Wait));
            Assert.Throws<ArgumentNullException>(() => InputTest.Run(windows, new Settings(), windows.Clock, null));
        }
    }
}
