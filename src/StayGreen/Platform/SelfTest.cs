using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using StayGreen.Core;
using StayGreen.UI;

namespace StayGreen.Platform
{
    /// <summary>
    /// "StayGreen.exe --selftest [ergebnis.txt]": prueft auf dem echten Windows, ob Eingaben ankommen, der
    /// Leerlauf-Zaehler zurueckgesetzt wird, der Hotkey ausloest, der Autostart schreibbar ist usw.
    /// Wird von der CI genutzt und hilft bei der Fehlersuche auf dem Arbeitsrechner (ohne Dateiangabe wird das
    /// Ergebnis in einem Fenster angezeigt).
    /// </summary>
    static class SelfTest
    {
        const ushort VK_SHIFT = 0x10;
        const ushort VK_CONTROL = 0x11;
        const ushort VK_MENU = 0x12; // Alt
        const ushort VK_F12 = 0x7B;

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
                RunWindowsChecks(check);
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

        static void RunWindowsChecks(Action<string, bool, string> check)
        {
            var input = new Win32Input();

            // Erst eine Weile nichts tun, damit ein Leerlauf messbar ist.
            Thread.Sleep(3000);
            TimeSpan idleBefore = input.GetIdleTime();
            check("Leerlauf messbar", idleBefore >= TimeSpan.FromSeconds(2.5),
                "vorher " + idleBefore.TotalSeconds.ToString("0.0") + " s");

            bool key = input.SendActivity(ActivityMode.Key, 2, ActivityKey.F15);
            check("Tastendruck (F15) angenommen", key, "Win32-Fehler " + input.LastError);
            Thread.Sleep(150);
            TimeSpan afterKey = input.GetIdleTime();
            check("Leerlauf nach Tastendruck zurueckgesetzt", afterKey < TimeSpan.FromSeconds(2),
                "nachher " + afterKey.TotalSeconds.ToString("0.0") + " s");

            Thread.Sleep(3000);
            bool mouse = input.SendActivity(ActivityMode.Mouse, 2, ActivityKey.F15);
            check("Mausbewegung angenommen", mouse, "Win32-Fehler " + input.LastError);
            Thread.Sleep(150);
            TimeSpan afterMouse = input.GetIdleTime();
            check("Leerlauf nach Mausbewegung zurueckgesetzt", afterMouse < TimeSpan.FromSeconds(2),
                "nachher " + afterMouse.TotalSeconds.ToString("0.0") + " s");

            Thread.Sleep(3000);
            bool shift = input.SendActivity(ActivityMode.Key, 2, ActivityKey.Shift);
            check("Umschalt-Tipp angenommen", shift, "Win32-Fehler " + input.LastError);
            Thread.Sleep(150);
            TimeSpan afterShift = input.GetIdleTime();
            check("Leerlauf nach Umschalt-Tipp zurueckgesetzt", afterShift < TimeSpan.FromSeconds(2),
                "nachher " + afterShift.TotalSeconds.ToString("0.0") + " s");

            check("Tastencodes F13-F24", Win32Input.VirtualKey(ActivityKey.F13) == 0x7C
                                          && Win32Input.VirtualKey(ActivityKey.F15) == 0x7E
                                          && Win32Input.VirtualKey(ActivityKey.F24) == 0x87
                                          && Win32Input.VirtualKey(ActivityKey.Shift) == 0x10, null);

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

            CheckHotkey(check, input);
            CheckKeyboardLayouts(check);
            CheckCommandPipe(check);
            CheckAutostart(check);
            CheckCountdown(check);
        }

        /// <summary>
        /// Die Abfrage, ob ein Hotkey ein Zeichen tippen wuerde (AltGr), muss funktionieren: Ohne Umschaltzustand
        /// liefert eine Buchstabentaste immer ein Zeichen, und die Standardtaste Strg+Alt+G darf nichts melden.
        /// </summary>
        static void CheckKeyboardLayouts(Action<string, bool, string> check)
        {
            try
            {
                string plain = KeyboardLayouts.CharacterFor(0x41, KeyboardLayouts.CurrentLayout(), false, false, false);
                check("Tastaturbelegung abfragbar (ToUnicodeEx)", plain != null, "Taste A tippt \"" + plain + "\"");

                string typed = KeyboardLayouts.TypedCharacter(new Settings { HotkeyKey = "F9" });
                check("F-Tasten als Hotkey tippen nie ein Zeichen", typed == null, null);
            }
            catch (Exception ex)
            {
                check("Tastaturbelegung abfragbar", false, ex.Message);
            }
        }

        /// <summary>Der Befehlskanal zur laufenden Instanz: Zeile senden und empfangen (eigener Name, stoert keine echte Instanz).</summary>
        static void CheckCommandPipe(Action<string, bool, string> check)
        {
            try
            {
                string name = "StayGreen.SelfTest." + Guid.NewGuid().ToString("N");
                string received = null;
                using (var pipe = new CommandPipe(name))
                {
                    pipe.Listen(line => received = line);
                    bool sent = CommandPipe.Send("pause 5", name, 3000);
                    DateTime end = DateTime.UtcNow.AddSeconds(3);
                    while (received == null && DateTime.UtcNow < end) Thread.Sleep(20);
                    check("Befehlskanal sendet und empfaengt", sent && received == "pause 5",
                        "gesendet: " + sent + ", empfangen: \"" + received + "\"");
                }
            }
            catch (Exception ex)
            {
                check("Befehlskanal", false, ex.Message);
            }
        }

        /// <summary>Meldet eine seltene Kombination an, drueckt sie und prueft, dass die Nachricht ankommt.</summary>
        static void CheckHotkey(Action<string, bool, string> check, Win32Input input)
        {
            try
            {
                using (var hotkey = new HotkeyWindow())
                {
                    bool pressed = false;
                    hotkey.Pressed += () => pressed = true;

                    // Seltene Kombination, damit der Test nicht an belegten Tasten scheitert.
                    var hs = new Settings { HotkeyCtrl = true, HotkeyAlt = true, HotkeyShift = true, HotkeyKey = "F12" };
                    bool registered = hotkey.Register(hs);
                    check("Hotkey anmelden", registered, "Strg+Alt+Umschalt+F12");
                    if (!registered) return;

                    bool sent = input.SendChord(new[] { VK_CONTROL, VK_MENU, VK_SHIFT }, VK_F12);
                    DateTime end = DateTime.UtcNow.AddSeconds(3);
                    while (!pressed && DateTime.UtcNow < end)
                    {
                        Application.DoEvents(); // Nachrichten abarbeiten, WM_HOTKEY landet im Fenster
                        Thread.Sleep(20);
                    }
                    check("Hotkey wird ausgeloest", pressed, sent ? "Kombination gesendet" : "Senden fehlgeschlagen");
                }
            }
            catch (Exception ex)
            {
                check("Hotkey", false, ex.Message);
            }
        }

        /// <summary>Autostart-Eintrag unter eigenem Testnamen schreiben und wieder entfernen (der echte Eintrag bleibt unberuehrt).</summary>
        static void CheckAutostart(Action<string, bool, string> check)
        {
            const string testName = "StayGreen-SelfTest";
            string command = Autostart.ExpectedCommand;
            check("Autostart-Befehl gebildet", command.Contains("--autostart"), command);
            try
            {
                bool written = Autostart.Set(true, testName, command);
                check("Autostart schreiben (HKCU\\Run)", written && Autostart.CurrentCommand(testName) == command, null);
            }
            finally
            {
                bool removed = Autostart.Set(false, testName);
                check("Autostart entfernen", removed && !Autostart.IsEnabled(testName), null);
            }
        }

        /// <summary>Die Vorwarnung vor dem Auto-Stopp muss nach Ablauf selbststaendig schliessen (Ergebnis: Timeout).</summary>
        static void CheckCountdown(Action<string, bool, string> check)
        {
            try
            {
                using (var dialog = new CountdownForm(DateTime.Now.AddSeconds(1), () => DateTime.Now, "Test", 15))
                {
                    dialog.ShowDialog();
                    check("Vorwarnung laeuft ab", dialog.Choice == AutoStopChoice.Timeout, dialog.Choice.ToString());
                }
            }
            catch (Exception ex)
            {
                check("Countdown-Dialog", false, ex.Message);
            }
        }

        /// <summary>Zeigt das Ergebnis in einem Fenster (nur wenn der Nutzer den Selbsttest ohne Dateiangabe gestartet hat).</summary>
        public static void ShowReport(string path, bool ok)
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch { text = "Das Ergebnis konnte nicht gelesen werden: " + path; }
            MessageBox.Show(text + "\r\n\r\n" + path, "StayGreen Selbsttest",
                MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
    }
}
