using System;
using System.IO;
using StayGreen.Core;

namespace StayGreen.Platform
{
    /// <summary>
    /// Haelt Fehler in "error.log" fest, statt das Programm abstuerzen zu lassen. Ein Tool, das den Status
    /// halten soll, darf nicht wegen einer fehlgeschlagenen Anzeige-Aktualisierung verschwinden.
    /// Die Datei bleibt klein: gleiche Fehler werden nur gezaehlt, nicht jedes Mal ausgeschrieben, und ab einer
    /// Obergrenze wird eine Sicherung angelegt (siehe <see cref="ErrorLog"/>).
    /// </summary>
    static class CrashLog
    {
        static readonly ErrorLog Log = new ErrorLog(() => FilePath, LogFile.ErrorMaxBytes);

        public static string FilePath
        {
            get { return Path.Combine(SettingsStore.Directory, "error.log"); }
        }

        public static void Write(Exception ex)
        {
            Log.Write(DateTime.Now, ex == null ? "(unbekannter Fehler)" : ex.ToString());
        }
    }
}
