using System;
using System.Globalization;

namespace StayGreen.Core
{
    /// <summary>Was ein zweiter Start (oder ein Skript) der laufenden StayGreen-Instanz auftragen kann.</summary>
    public enum RemoteAction
    {
        /// <summary>Fenster nach vorn holen (das Verhalten, wenn StayGreen ohne Befehl noch einmal gestartet wird).</summary>
        Show,

        Start,
        Stop,
        Toggle,

        /// <summary>Fuer <see cref="RemoteCommand.Minutes"/> Minuten pausieren.</summary>
        Pause,

        /// <summary>Eine Pause sofort beenden.</summary>
        Resume,
    }

    /// <summary>
    /// Ein Befehl an die laufende Instanz, uebertragen als eine Textzeile ("start", "stop", "toggle", "resume",
    /// "pause 30"). Parsen und Formatieren liegen hier, damit sie ohne Windows getestet werden koennen; der
    /// Transportweg (Named Pipe) steht in der Plattformschicht.
    /// </summary>
    public sealed class RemoteCommand
    {
        static readonly char[] Separators = { ' ', '\t' };

        public const int DefaultPauseMinutes = 30;
        public const int MaxPauseMinutes = 24 * 60;

        public RemoteCommand(RemoteAction action)
            : this(action, 0)
        {
        }

        public RemoteCommand(RemoteAction action, int minutes)
        {
            Action = action;
            Minutes = action == RemoteAction.Pause ? ClampMinutes(minutes) : 0;
        }

        public RemoteAction Action { get; private set; }

        /// <summary>Nur bei <see cref="RemoteAction.Pause"/>: Dauer in Minuten.</summary>
        public int Minutes { get; private set; }

        public static int ClampMinutes(int minutes)
        {
            if (minutes <= 0) return DefaultPauseMinutes;
            return minutes > MaxPauseMinutes ? MaxPauseMinutes : minutes;
        }

        public string ToLine()
        {
            return Action == RemoteAction.Pause
                ? "pause " + Minutes.ToString(CultureInfo.InvariantCulture)
                : Action.ToString().ToLowerInvariant();
        }

        /// <summary>Liest eine Zeile tolerant (Gross-/Kleinschreibung und Leerraum egal). "pause" ohne Zahl = 30 Minuten.</summary>
        public static bool TryParse(string line, out RemoteCommand command)
        {
            command = null;
            if (string.IsNullOrWhiteSpace(line)) return false;

            string[] parts = line.Trim().ToLowerInvariant().Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Length > 2) return false;

            if (parts[0] == "pause")
            {
                int minutes = DefaultPauseMinutes;
                if (parts.Length == 2
                    && !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutes))
                    return false;
                command = new RemoteCommand(RemoteAction.Pause, minutes);
                return true;
            }

            if (parts.Length != 1) return false;
            switch (parts[0])
            {
                case "show": command = new RemoteCommand(RemoteAction.Show); return true;
                case "start": command = new RemoteCommand(RemoteAction.Start); return true;
                case "stop": command = new RemoteCommand(RemoteAction.Stop); return true;
                case "toggle": command = new RemoteCommand(RemoteAction.Toggle); return true;
                case "resume": command = new RemoteCommand(RemoteAction.Resume); return true;
                default: return false;
            }
        }

        public override string ToString()
        {
            return ToLine();
        }
    }
}
