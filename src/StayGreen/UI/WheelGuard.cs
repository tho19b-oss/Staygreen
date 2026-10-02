using System;
using System.Windows.Forms;

namespace StayGreen.UI
{
    /// <summary>
    /// Verhindert, dass das Mausrad beim Scrollen versehentlich Einstellungen aendert. Windows reicht das Rad an das Feld
    /// unter dem Mauszeiger, und ein Auswahl-, Zahlen- oder Datumsfeld wuerde dann seinen Wert wechseln, statt die Seite
    /// weiterzurollen (Sprache und Intervall waeren so schnell verstellt). Hier scrollt bei einem Feld ohne Fokus die
    /// Seite; ein Feld, in das man geklickt hat, reagiert wie gewohnt auf das Rad. Eine geoeffnete Auswahlliste rollt
    /// ebenfalls selbst.
    /// </summary>
    sealed class WheelGuard : IMessageFilter
    {
        const int WM_MOUSEWHEEL = 0x020A;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;

            try
            {
                Control field = InputFieldOf(Control.FromChildHandle(m.HWnd));
                if (field == null || field.ContainsFocus) return false;

                var combo = field as ComboBox;
                if (combo != null && combo.DroppedDown) return false;

                ScrollHost host = HostOf(field);
                if (host == null) return false;

                // Hoeherwertiges Wort von wParam: Drehung in Vielfachen von 120 (positiv = vom Nutzer weg = nach oben).
                int delta = unchecked((short)((m.WParam.ToInt64() >> 16) & 0xFFFF));
                host.ScrollByWheel(delta);
                return true;
            }
            catch (Exception)
            {
                return false; // im Zweifel das Rad normal weiterreichen
            }
        }

        /// <summary>Das Eingabefeld, zu dem das Fenster gehoert (die Teile eines Zahlenfelds haben es als Eltern), sonst null.</summary>
        static Control InputFieldOf(Control control)
        {
            for (Control c = control; c != null; c = c.Parent)
            {
                if (c is ComboBox || c is NumericUpDown || c is DateTimePicker) return c;
                if (c is ScrollHost) return null;
            }
            return null;
        }

        static ScrollHost HostOf(Control control)
        {
            for (Control c = control; c != null; c = c.Parent)
            {
                var host = c as ScrollHost;
                if (host != null) return host;
            }
            return null;
        }
    }
}
