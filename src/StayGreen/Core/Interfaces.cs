using System;

namespace StayGreen.Core
{
    /// <summary>Alles, was das Betriebssystem fuer das Aktivhalten leisten muss.</summary>
    public interface IInputBackend
    {
        /// <summary>Erzeugt eine winzige Eingabe. False, wenn das System sie nicht annimmt (z. B. gesperrte Sitzung).</summary>
        bool SendActivity(ActivityMode mode, int mousePixels);

        /// <summary>Zeit seit der letzten Eingabe des Nutzers (oder einer erzeugten Eingabe).</summary>
        TimeSpan GetIdleTime();

        /// <summary>PC und Bildschirm wach halten (an) bzw. wieder normal verhalten (aus).</summary>
        void SetKeepAwake(bool keepAwake);
    }

    /// <summary>Systemaktionen fuer den Auto-Stopp.</summary>
    public interface ISystemActions
    {
        void CloseTeams();

        void LockWorkstation();

        void Shutdown();
    }
}
