using System;
using System.Runtime.InteropServices;
using System.Threading;
using StayGreen.Core;

namespace StayGreen.Platform
{
    /// <summary>
    /// Erzeugt echte Eingaben ueber SendInput. Anders als SetCursorPos setzt SendInput den Leerlauf-Zaehler
    /// von Windows zurueck, und genau den werten Teams, Bildschirmschoner und Sperrzeit aus.
    /// </summary>
    sealed class Win32Input : IInputBackend
    {
        static readonly int InputSize = Marshal.SizeOf(typeof(NativeMethods.INPUT));

        /// <summary>Letzter Win32-Fehlercode von SendInput (0 = kein Fehler); fuer Diagnose und Selbsttest.</summary>
        public int LastError { get; private set; }

        public bool SendActivity(ActivityMode mode, int mousePixels)
        {
            bool ok = true;
            if (mode == ActivityMode.Key || mode == ActivityMode.Both)
                ok &= SendKey(NativeMethods.VK_F15);
            if (mode == ActivityMode.Mouse || mode == ActivityMode.Both)
                ok &= NudgeMouse(mousePixels);
            return ok;
        }

        /// <summary>Drueckt Modifier + Taste und laesst alles wieder los (fuer den Hotkey-Selbsttest).</summary>
        public bool SendChord(ushort[] modifiers, ushort key)
        {
            var inputs = new System.Collections.Generic.List<NativeMethods.INPUT>();
            foreach (ushort m in modifiers) inputs.Add(KeyInput(m, Scan(m), 0));
            inputs.Add(KeyInput(key, Scan(key), 0));
            inputs.Add(KeyInput(key, Scan(key), NativeMethods.KEYEVENTF_KEYUP));
            for (int i = modifiers.Length - 1; i >= 0; i--)
                inputs.Add(KeyInput(modifiers[i], Scan(modifiers[i]), NativeMethods.KEYEVENTF_KEYUP));
            return Send(inputs.ToArray());
        }

        static ushort Scan(ushort virtualKey)
        {
            return (ushort)NativeMethods.MapVirtualKey(virtualKey, NativeMethods.MAPVK_VK_TO_VSC);
        }

        bool SendKey(ushort virtualKey)
        {
            ushort scan = (ushort)NativeMethods.MapVirtualKey(virtualKey, NativeMethods.MAPVK_VK_TO_VSC);
            var inputs = new[]
            {
                KeyInput(virtualKey, scan, 0),
                KeyInput(virtualKey, scan, NativeMethods.KEYEVENTF_KEYUP),
            };
            return Send(inputs);
        }

        /// <summary>
        /// Maus ein paar Pixel hin und sofort zurueck. Die Zeigerbeschleunigung von Windows kann dabei einen
        /// Pixel Restfehler hinterlassen; ohne Korrektur wuerde der Zeiger ueber Stunden wegwandern.
        /// Deshalb wird die Ausgangsposition danach wiederhergestellt.
        /// </summary>
        bool NudgeMouse(int pixels)
        {
            int d = Math.Max(1, pixels);
            NativeMethods.POINT before;
            bool haveBefore = NativeMethods.GetCursorPos(out before);

            bool ok = Send(new[] { MouseInput(d, 0), MouseInput(-d, 0) });

            if (haveBefore)
            {
                Thread.Sleep(15); // dem Eingabesystem Zeit geben, beide Bewegungen zu verarbeiten
                NativeMethods.POINT after;
                if (NativeMethods.GetCursorPos(out after) && (after.X != before.X || after.Y != before.Y))
                {
                    // Nur kleine Abweichungen zuruecksetzen. Ist der Zeiger weit weg, hat der Nutzer ihn
                    // gerade selbst bewegt, und das darf nicht rueckgaengig gemacht werden.
                    int limit = Math.Max(3, 2 * d);
                    if (Math.Abs(after.X - before.X) <= limit && Math.Abs(after.Y - before.Y) <= limit)
                        NativeMethods.SetCursorPos(before.X, before.Y);
                }
            }
            return ok;
        }

        bool Send(NativeMethods.INPUT[] inputs)
        {
            uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, InputSize);
            if (sent == (uint)inputs.Length)
            {
                LastError = 0;
                return true;
            }
            // 0 = abgelehnt (z. B. gesperrte Sitzung oder UIPI), Fehlercode steht fuer die Diagnose bereit.
            LastError = Marshal.GetLastWin32Error();
            return false;
        }

        static NativeMethods.INPUT KeyInput(ushort vk, ushort scan, uint flags)
        {
            var input = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
            input.U.ki = new NativeMethods.KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags };
            return input;
        }

        static NativeMethods.INPUT MouseInput(int dx, int dy)
        {
            var input = new NativeMethods.INPUT { type = NativeMethods.INPUT_MOUSE };
            input.U.mi = new NativeMethods.MOUSEINPUT { dx = dx, dy = dy, dwFlags = NativeMethods.MOUSEEVENTF_MOVE };
            return input;
        }

        public TimeSpan GetIdleTime()
        {
            var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.LASTINPUTINFO)) };
            if (!NativeMethods.GetLastInputInfo(ref info))
                return TimeSpan.FromDays(1); // unbekannt: lieber eingreifen als den Status verlieren

            uint idleMs = unchecked(NativeMethods.GetTickCount() - info.dwTime);
            if (idleMs > int.MaxValue) idleMs = 0; // Zeitstempel knapp nach unserer Messung
            return TimeSpan.FromMilliseconds(idleMs);
        }

        public void SetKeepAwake(bool keepAwake)
        {
            // ES_CONTINUOUS gilt pro Thread. Alle Aufrufe kommen vom UI-Thread (Timer), daher konsistent.
            uint flags = keepAwake
                ? NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED | NativeMethods.ES_DISPLAY_REQUIRED
                : NativeMethods.ES_CONTINUOUS;
            NativeMethods.SetThreadExecutionState(flags);
        }
    }

    /// <summary>
    /// Platzhalter fuer Nicht-Windows-Systeme (Entwicklung/Vorschau): tut nichts, simuliert nur einen Leerlauf.
    /// </summary>
    sealed class DemoInput : IInputBackend
    {
        DateTime _lastInput = DateTime.UtcNow.AddMinutes(-10);

        public bool SendActivity(ActivityMode mode, int mousePixels)
        {
            _lastInput = DateTime.UtcNow;
            return true;
        }

        public TimeSpan GetIdleTime()
        {
            return DateTime.UtcNow - _lastInput;
        }

        public void SetKeepAwake(bool keepAwake)
        {
        }
    }
}
