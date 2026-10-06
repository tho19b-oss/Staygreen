using System;

namespace StayGreen.Core
{
    /// <summary>Ausgang des Tests "Eingabe testen" im Reiter Aktivitaet.</summary>
    public enum InputTestResult
    {
        /// <summary>Windows hat die Eingabe angenommen und den Leerlaufzaehler zurueckgesetzt: Sie zaehlt als Aktivitaet.</summary>
        Confirmed,

        /// <summary>Windows hat die Eingabe angenommen, den Leerlaufzaehler aber nicht zurueckgesetzt.</summary>
        NotCounted,

        /// <summary>Windows hat die Eingabe abgelehnt (gesperrte Sitzung, blockierter Zugriff).</summary>
        Rejected,
    }

    /// <summary>
    /// "Eingabe testen": erzeugt dieselbe Eingabe wie das Aktivhalten und meldet nicht nur, dass Windows sie annimmt,
    /// sondern auch, ob sie als Aktivitaet zaehlt, also den Leerlaufzaehler zurueckgesetzt hat (den werten Teams und Co. aus).
    /// Warten und Uhr kommen von aussen, dadurch laeuft der Test in den Unit-Tests ohne echte Pausen.
    /// </summary>
    public static class InputTest
    {
        /// <summary>
        /// Pause vor der Eingabe. Der Klick auf den Knopf hat den Leerlaufzaehler gerade selbst zurueckgesetzt; ohne Pause
        /// liesse sich nicht unterscheiden, ob er vom Klick oder von der erzeugten Eingabe stammt. Wirkt die Eingabe nicht,
        /// ist der Zaehler danach mindestens so weit gelaufen wie diese Pause.
        /// </summary>
        public static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(150);

        /// <summary>Pause nach der Eingabe, bis Windows sie verarbeitet hat (der Selbsttest wartet ebenso lange).</summary>
        public static readonly TimeSpan Delivery = TimeSpan.FromMilliseconds(150);

        /// <summary>
        /// Spielraum fuer die grobe Aufloesung von Uhr und Leerlaufzaehler (Windows tickt etwa alle 16 ms). Muss deutlich
        /// kleiner sein als <see cref="Settle"/>, sonst geht eine wirkungslose Eingabe als wirksam durch.
        /// </summary>
        public static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Sendet eine Eingabe mit den aktuellen Einstellungen. <paramref name="clock"/> liefert eine laufende Zeit,
        /// <paramref name="wait"/> wartet die angegebene Dauer.
        /// </summary>
        public static InputTestResult Run(IInputBackend input, Settings settings, Func<TimeSpan> clock, Action<TimeSpan> wait)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (wait == null) throw new ArgumentNullException(nameof(wait));

            wait(Settle);
            TimeSpan sentAt = clock();
            if (!input.SendActivity(settings.Mode, settings.MousePixels, settings.InputKey))
                return InputTestResult.Rejected;

            wait(Delivery);
            TimeSpan idle = input.GetIdleTime();
            TimeSpan sinceSend = clock() - sentAt;

            // Hat die Eingabe gewirkt, ist der Zaehler hoechstens so alt wie die Zeit seit dem Senden. Sonst laeuft er
            // seit dem Klick weiter und ist um mindestens die Pause vor dem Senden aelter.
            return idle < sinceSend + Tolerance ? InputTestResult.Confirmed : InputTestResult.NotCounted;
        }
    }
}
