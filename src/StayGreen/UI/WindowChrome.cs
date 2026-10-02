using System;
using System.Windows.Forms;
using StayGreen.Platform;

namespace StayGreen.UI
{
    /// <summary>Kleine Anpassungen am Fensterrahmen, die WinForms selbst nicht kann.</summary>
    static class WindowChrome
    {
        /// <summary>Im dunklen Design bekommt auch die Titelleiste das dunkle Aussehen (Windows 10 ab 2004 und Windows 11).</summary>
        public static void ApplyTitleBar(Form form)
        {
            if (!PlatformInfo.IsWindows || form == null) return;
            try
            {
                int dark = Theme.IsDark ? 1 : 0;
                int result = NativeMethods.DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
                if (result != 0) result = NativeMethods.DwmSetWindowAttribute(form.Handle, 19, ref dark, sizeof(int));
                if (result != 0) return;   // Windows kennt das Attribut nicht (sehr alter Stand): nichts weiter zu tun
            }
            catch
            {
                // Aeltere Windows-Staende kennen das Attribut nicht: Titelleiste bleibt, wie sie ist.
            }
        }
    }
}
