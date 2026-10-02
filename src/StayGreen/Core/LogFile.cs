using System;
using System.IO;
using System.Text;

namespace StayGreen.Core
{
    /// <summary>
    /// Haengt Text an eine Logdatei an und haelt sie klein: Wird die Datei zu gross, wird sie zur Sicherung
    /// ("datei.txt.1", eine aeltere Sicherung wird ersetzt) und es beginnt eine neue.
    /// </summary>
    public static class LogFile
    {
        /// <summary>Obergrenze fuer das Protokoll (rund 15.000 Zeilen).</summary>
        public const long ProtocolMaxBytes = 1024 * 1024;

        /// <summary>Obergrenze fuer die Fehlerdatei.</summary>
        public const long ErrorMaxBytes = 256 * 1024;

        /// <summary>Haengt <paramref name="text"/> an; legt Ordner und Datei bei Bedarf an. Wirft bei Schreibfehlern.</summary>
        public static void Append(string path, string text, long maxBytes)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            RotateIfNeeded(path, maxBytes);
            // Die Byte-Reihenfolgemarke (BOM) schreibt .NET nur, wenn die Datei neu angelegt wird.
            File.AppendAllText(path, text, new UTF8Encoding(true));
        }

        static void RotateIfNeeded(string path, long maxBytes)
        {
            if (maxBytes <= 0) return;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length < maxBytes) return;

                string backup = path + ".1";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(path, backup);
            }
            catch
            {
                // Laesst sich nicht drehen (gesperrt): weiter anhaengen, wichtiger ist, dass nichts verloren geht.
            }
        }
    }

    /// <summary>
    /// Fehlerprotokoll ohne Flut: Tritt derselbe Fehler immer wieder auf (z. B. in jedem Sekundentakt), wird er nur
    /// beim ersten Mal ausfuehrlich geschrieben. Die Wiederholungen werden gezaehlt und spaeter als eine Zeile
    /// nachgetragen. Zusammen mit der Groessenbegrenzung in <see cref="LogFile"/> waechst die Datei nie unbegrenzt.
    /// </summary>
    public sealed class ErrorLog
    {
        /// <summary>So lange werden gleiche Fehler zusammengefasst.</summary>
        public static readonly TimeSpan RepeatWindow = TimeSpan.FromMinutes(10);

        readonly Func<string> _path;
        readonly long _maxBytes;

        string _lastText;
        DateTime _lastWritten;
        int _suppressed;

        public ErrorLog(Func<string> path, long maxBytes)
        {
            _path = path;
            _maxBytes = maxBytes;
        }

        /// <summary>Anzahl der unterdrueckten Wiederholungen seit dem letzten Eintrag.</summary>
        public int Suppressed
        {
            get { return _suppressed; }
        }

        /// <summary>Schreibt den Fehler (oder zaehlt ihn nur mit). Wirft nie.</summary>
        public void Write(DateTime now, string text)
        {
            try
            {
                text = text ?? "";
                bool repeat = text == _lastText && now >= _lastWritten && now - _lastWritten < RepeatWindow;
                if (repeat)
                {
                    _suppressed++;
                    return;
                }

                var sb = new StringBuilder();
                if (_suppressed > 0)
                    sb.Append(Stamp(now)).Append("  (letzter Fehler noch ").Append(_suppressed).Append(" Mal aufgetreten)")
                        .Append(Environment.NewLine).Append(Environment.NewLine);
                sb.Append(Stamp(now)).Append("  ").Append(text).Append(Environment.NewLine).Append(Environment.NewLine);

                LogFile.Append(_path(), sb.ToString(), _maxBytes);
                _lastText = text;
                _lastWritten = now;
                _suppressed = 0;
            }
            catch
            {
                // Selbst das Protokollieren darf nichts kaputt machen.
            }
        }

        static string Stamp(DateTime time)
        {
            return time.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Schreibt eine Datei so, dass nie eine halbe oder fehlende Datei zurueckbleibt.</summary>
    public static class AtomicFile
    {
        /// <summary>
        /// Schreibt erst in eine Temp-Datei und ersetzt dann das Ziel in einem Schritt (<c>File.Replace</c>, auf NTFS
        /// atomar). Wo das nicht geht (z. B. FAT-USB-Stick im portablen Modus), wird auf Loeschen und Umbenennen
        /// zurueckgefallen. Wirft bei Schreibfehlern.
        /// </summary>
        public static void WriteAllText(string path, string text, Encoding encoding)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, encoding);

            if (!File.Exists(path))
            {
                File.Move(tmp, path);
                return;
            }

            try
            {
                File.Replace(tmp, path, null);
            }
            catch (Exception ex)
            {
                if (!(ex is IOException) && !(ex is PlatformNotSupportedException) && !(ex is UnauthorizedAccessException))
                    throw;
                File.Delete(path);
                File.Move(tmp, path);
            }
        }
    }
}
