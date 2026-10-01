using System;
using System.IO;

namespace StayGreen.Platform
{
    /// <summary>
    /// Haelt Fehler in "error.log" fest, statt das Programm abstuerzen zu lassen. Ein Tool, das den Status
    /// halten soll, darf nicht wegen einer fehlgeschlagenen Anzeige-Aktualisierung verschwinden.
    /// </summary>
    static class CrashLog
    {
        public static string FilePath
        {
            get { return Path.Combine(SettingsStore.Directory, "error.log"); }
        }

        public static void Write(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(SettingsStore.Directory);
                File.AppendAllText(FilePath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + Environment.NewLine + Environment.NewLine);
            }
            catch
            {
                // Selbst das Protokollieren darf nichts kaputt machen.
            }
        }
    }
}
