using System;
using System.Globalization;

namespace StayGreen.Core
{
    /// <summary>
    /// Schreibt Start und Stopp (und Pausen, Auto-Stopp, Sperren usw.) in eine Textdatei, damit sich spaeter
    /// nachvollziehen laesst, wann StayGreen gelaufen ist und warum der Status zwischendurch gekippt sein koennte.
    /// Die Datei bleibt klein (Groessenbegrenzung mit einer Sicherung, siehe <see cref="LogFile"/>).
    /// </summary>
    public sealed class ProtocolLog
    {
        readonly Settings _settings;
        readonly Func<string> _defaultPath;

        public ProtocolLog(Settings settings, Func<string> defaultPath)
        {
            _settings = settings;
            _defaultPath = defaultPath;
        }

        /// <summary>Letzter Schreibfehler (z. B. Pfad nicht beschreibbar); null, wenn alles gut ging.</summary>
        public string LastError { get; private set; }

        public string CurrentPath
        {
            get
            {
                string p = _settings.LogPath;
                return string.IsNullOrWhiteSpace(p) ? _defaultPath() : p;
            }
        }

        public static string FormatLine(DateTime time, string kind, string message)
        {
            return time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                   + "  " + (kind ?? "").PadRight(9) + (message ?? "");
        }

        /// <summary>
        /// Prueft, ob der Protokollpfad beschreibbar ist (legt die Datei bei Bedarf leer an), und setzt <see cref="LastError"/>.
        /// So zeigt ein neu gewaehlter Pfad sofort, ob er funktioniert, statt erst beim naechsten Ereignis.
        /// </summary>
        public void Probe()
        {
            if (!_settings.LogEnabled) return;
            try
            {
                LogFile.Append(CurrentPath, "", LogFile.ProtocolMaxBytes);
                LastError = null;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }

        public void Write(DateTime time, string kind, string message)
        {
            if (!_settings.LogEnabled) return;
            try
            {
                LogFile.Append(CurrentPath, FormatLine(time, kind, message) + Environment.NewLine, LogFile.ProtocolMaxBytes);
                LastError = null;
            }
            catch (Exception ex)
            {
                // Ein nicht beschreibbares Protokoll darf das Aktivhalten nie stoeren; die Oberflaeche zeigt den Fehler an.
                LastError = ex.Message;
            }
        }
    }
}
