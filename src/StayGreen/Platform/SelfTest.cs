using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using StayGreen.Core;

namespace StayGreen.Platform
{
    /// <summary>
    /// "StayGreen.exe --selftest ergebnis.txt": prueft auf dem echten Windows, ob Eingaben ankommen,
    /// der Leerlauf-Zaehler zurueckgesetzt wird und die Strukturgroessen stimmen. Wird von der CI genutzt
    /// und hilft bei der Fehlersuche auf dem Arbeitsrechner.
    /// </summary>
    static class SelfTest
    {
        public static int Run(string outputPath)
        {
            var lines = new List<string>();
            bool allOk = true;

            Action<string, bool, string> check = (name, ok, detail) =>
            {
                if (!ok) allOk = false;
                lines.Add((ok ? "OK    " : "FEHLER") + "  " + name + (string.IsNullOrEmpty(detail) ? "" : "  (" + detail + ")"));
            };

            lines.Add("StayGreen Selbsttest " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add("Windows: " + PlatformInfo.IsWindows + ", 64 Bit Prozess: " + (IntPtr.Size == 8)
                      + ", OS: " + Environment.OSVersion.VersionString + ", Runtime: " + Environment.Version);

            int inputSize = Marshal.SizeOf(typeof(NativeMethods.INPUT));
            check("sizeof(INPUT)", inputSize == NativeMethods.ExpectedInputSize,
                inputSize + " Byte, erwartet " + NativeMethods.ExpectedInputSize);

            // Einstellungen: Speichern und Laden ergibt dasselbe.
            var s = new Settings { IntervalSeconds = 77, Mode = ActivityMode.Key };
            s.Rules.Add(ScheduleRule.Weekdays(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)));
            Settings back = Settings.Parse(s.Serialize());
            check("Einstellungen Roundtrip", back.IntervalSeconds == 77 && back.Mode == ActivityMode.Key && back.Rules.Count == 1, null);

            if (PlatformInfo.IsWindows)
            {
                var input = new Win32Input();

                // Erst eine Weile nichts tun, damit ein Leerlauf messbar ist.
                Thread.Sleep(3000);
                TimeSpan idleBefore = input.GetIdleTime();
                check("Leerlauf messbar", idleBefore >= TimeSpan.FromSeconds(2.5),
                    "vorher " + idleBefore.TotalSeconds.ToString("0.0") + " s");

                bool key = input.SendActivity(ActivityMode.Key, 2);
                check("Tastendruck (F15) angenommen", key, "Win32-Fehler " + input.LastError);
                Thread.Sleep(150);
                TimeSpan afterKey = input.GetIdleTime();
                check("Leerlauf nach Tastendruck zurueckgesetzt", afterKey < TimeSpan.FromSeconds(2),
                    "nachher " + afterKey.TotalSeconds.ToString("0.0") + " s");

                Thread.Sleep(3000);
                bool mouse = input.SendActivity(ActivityMode.Mouse, 2);
                check("Mausbewegung angenommen", mouse, "Win32-Fehler " + input.LastError);
                Thread.Sleep(150);
                TimeSpan afterMouse = input.GetIdleTime();
                check("Leerlauf nach Mausbewegung zurueckgesetzt", afterMouse < TimeSpan.FromSeconds(2),
                    "nachher " + afterMouse.TotalSeconds.ToString("0.0") + " s");

                try
                {
                    input.SetKeepAwake(true);
                    input.SetKeepAwake(false);
                    check("Wach halten ein/aus", true, null);
                }
                catch (Exception ex)
                {
                    check("Wach halten ein/aus", false, ex.Message);
                }

                try
                {
                    using (var hotkey = new HotkeyWindow())
                    {
                        // Seltene Kombination, damit der Test nicht an belegten Tasten scheitert.
                        var hs = new Settings { HotkeyCtrl = true, HotkeyAlt = true, HotkeyShift = true, HotkeyKey = "F12" };
                        bool ok = hotkey.Register(hs);
                        check("Hotkey anmelden", ok, "Strg+Alt+Umschalt+F12");
                    }
                }
                catch (Exception ex)
                {
                    check("Hotkey anmelden", false, ex.Message);
                }

                string cmd = Autostart.ExpectedCommand;
                check("Autostart-Befehl gebildet", cmd.Contains("--autostart"), cmd);
            }
            else
            {
                lines.Add("(Kein Windows: Eingabetests uebersprungen)");
            }

            lines.Add(allOk ? "ERGEBNIS: ALLES OK" : "ERGEBNIS: FEHLER");
            try
            {
                File.WriteAllLines(outputPath, lines.ToArray(), new UTF8Encoding(false));
            }
            catch
            {
                return 2;
            }
            return allOk ? 0 : 1;
        }
    }
}
