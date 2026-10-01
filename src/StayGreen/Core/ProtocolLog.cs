using System;
using System.IO;
using System.Globalization;
using System.Text;

namespace StayGreen.Core
{
    /// <summary>
    /// Schreibt Start und Stopp (und Pausen, Auto-Stopp usw.) in eine Textdatei, damit sich spaeter
    /// nachvollziehen laesst, wann StayGreen gelaufen ist.
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

        public void Write(DateTime time, string kind, string message)
        {
            if (!_settings.LogEnabled) return;
            try
            {
                string path = CurrentPath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(path, FormatLine(time, kind, message) + Environment.NewLine, new UTF8Encoding(true));
                LastError = null;
            }
            catch (Exception ex)
            {
                // Ein nicht beschreibbares Protokoll darf das Aktivhalten nie stoeren.
                LastError = ex.Message;
            }
        }
    }
}
