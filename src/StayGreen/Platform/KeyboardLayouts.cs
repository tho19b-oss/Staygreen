using System;
using System.Text;
using StayGreen.Core;

namespace StayGreen.Platform
{
    /// <summary>
    /// Findet Hotkeys, die auf der Tastatur des Nutzers ein Zeichen tippen wuerden. Auf Tastaturen mit AltGr
    /// (deutsch, franzoesisch, polnisch ...) ist Strg+Alt dasselbe wie AltGr: Ein globaler Hotkey Strg+Alt+Q wuerde
    /// auf der deutschen Tastatur das "@" abfangen, Strg+Alt+E das "Euro"-Zeichen.
    /// </summary>
    static class KeyboardLayouts
    {
        const byte Down = 0x80;

        /// <summary>Bit 2: Der Tastaturzustand (Totzeichen) soll nicht veraendert werden (Windows 10, Version 1607 und neuer).</summary>
        const uint DoNotChangeKeyboardState = 4;

        /// <summary>
        /// Das Zeichen, das die Tastenkombination auf einer installierten Tastaturbelegung tippt, sonst null.
        /// Gibt es keinen Grund zur Sorge (keine Strg+Alt-Kombination, F-Taste, Fehler beim Abfragen), kommt null.
        /// </summary>
        public static string TypedCharacter(Settings s)
        {
            if (!PlatformInfo.IsWindows || s == null || !s.HotkeyCtrl || !s.HotkeyAlt) return null;

            uint vk;
            if (!HotkeyInfo.TryGetVirtualKey(s.HotkeyKey, out vk)) return null;
            if (vk >= 0x70) return null;                                  // F-Tasten tippen nie ein Zeichen

            try
            {
                foreach (IntPtr layout in InstalledLayouts())
                {
                    string typed = CharacterFor(vk, layout, true, true, s.HotkeyShift);
                    if (typed != null) return typed;
                }
            }
            catch
            {
                // Nicht abfragbar: lieber keinen falschen Alarm schlagen.
            }
            return null;
        }

        /// <summary>
        /// Das Zeichen, das eine Taste mit dem angegebenen Umschaltzustand in der Belegung <paramref name="layout"/> tippt;
        /// null, wenn keines (Steuerzeichen oder nichts). Ein Totzeichen (Akzent) liefert "?".
        /// </summary>
        public static string CharacterFor(uint virtualKey, IntPtr layout, bool ctrl, bool alt, bool shift)
        {
            uint scan = NativeMethods.MapVirtualKeyEx(virtualKey, NativeMethods.MAPVK_VK_TO_VSC, layout);

            var state = new byte[256];
            if (ctrl)
            {
                state[NativeMethods.VK_CONTROL] = Down;
                state[NativeMethods.VK_LCONTROL] = Down;
            }
            if (alt)
            {
                state[NativeMethods.VK_MENU] = Down;
                state[NativeMethods.VK_RMENU] = Down;
            }
            if (shift) state[NativeMethods.VK_SHIFT] = Down;

            var buffer = new StringBuilder(8);
            int count = NativeMethods.ToUnicodeEx(virtualKey, scan, state, buffer, buffer.Capacity, DoNotChangeKeyboardState, layout);

            if (count < 0) return "?";                                    // Totzeichen
            if (count == 0 || buffer.Length == 0) return null;
            char c = buffer[0];
            return char.IsControl(c) ? null : c.ToString();
        }

        /// <summary>Alle installierten Tastaturbelegungen (wer zwischen zwei Sprachen wechselt, soll nirgends Zeichen verlieren).</summary>
        static IntPtr[] InstalledLayouts()
        {
            int count = NativeMethods.GetKeyboardLayoutList(0, null);
            if (count <= 0) return new[] { NativeMethods.GetKeyboardLayout(0) };
            var layouts = new IntPtr[count];
            int got = NativeMethods.GetKeyboardLayoutList(count, layouts);
            if (got <= 0) return new[] { NativeMethods.GetKeyboardLayout(0) };
            if (got < count) Array.Resize(ref layouts, got);
            return layouts;
        }

        /// <summary>Aktuelle Belegung des aufrufenden Threads (fuer den Selbsttest).</summary>
        public static IntPtr CurrentLayout()
        {
            return NativeMethods.GetKeyboardLayout(0);
        }
    }
}
