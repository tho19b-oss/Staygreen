using System;

namespace StayGreen.Core
{
    /// <summary>Alles, was das Betriebssystem fuer das Aktivhalten leisten muss.</summary>
    public interface IInputBackend
    {
        /// <summary>
        /// Erzeugt eine winzige Eingabe (Taste <paramref name="key"/>, Mausbewegung oder beides). False, wenn das
        /// System sie nicht annimmt (z. B. gesperrte Sitzung).
        /// </summary>
        bool SendActivity(ActivityMode mode, int mousePixels, ActivityKey key);

        /// <summary>Zeit seit der letzten Eingabe des Nutzers (oder einer erzeugten Eingabe).</summary>
        TimeSpan GetIdleTime();

        /// <summary>PC und Bildschirm wach halten (an) bzw. wieder normal verhalten (aus).</summary>
        void SetKeepAwake(bool keepAwake);
    }

    /// <summary>Fragen an das System, die keine Eingabe sind.</summary>
    public interface ITeamsProbe
    {
        /// <summary>Laeuft Microsoft Teams (neu oder klassisch)?</summary>
        bool IsTeamsRunning();
    }

    /// <summary>Systemaktionen fuer den Auto-Stopp.</summary>
    public interface ISystemActions
    {
        void CloseTeams();

        void LockWorkstation();

        void Shutdown();
    }
}
