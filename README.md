# StayGreen

**Hält deinen Microsoft-Teams-Status auf „Verfügbar“, wenn du mal kurz nicht am Platz bist.**

Eine winzige Windows-App: **eine einzige Datei** (`StayGreen.exe`, rund 140 KB), **ohne Installation**, **ohne Administratorrechte**, **ohne Netzwerkzugriff**.

[**⬇ Neueste Version herunterladen**](https://github.com/tho19b-oss/Staygreen/releases/latest) · [English version](README.en.md)

---

## In einer Minute startklar

1. Auf der [Release-Seite](https://github.com/tho19b-oss/Staygreen/releases/latest) **`StayGreen.exe`** herunterladen und doppelklicken.
2. Zeigt Windows „Der Computer wurde durch Windows geschützt“ (SmartScreen)? Dann **Weitere Informationen → Trotzdem ausführen**. Die EXE ist nicht digital signiert, deshalb fragt Windows bei jeder neuen, unbekannten Datei nach.
3. Fertig. StayGreen startet sofort, die Lampe oben im Fenster wird grün, und dein Status bleibt grün. Mit **Stoppen** hörst du wieder auf.

Beim ersten Mal lohnt ein Blick auf die Registerkarte **System**: Dort stellst du ein, ob StayGreen mit Windows starten und im Infobereich neben der Uhr weiterlaufen soll.

> Windows 11 versteckt neue Symbole im Infobereich hinter dem Pfeil `^`. Ziehe das StayGreen-Symbol heraus, wenn du es dauerhaft sehen willst.

## Funktionen

| Funktion | Beschreibung |
|---|---|
| **Status grün halten** | Erzeugt in einstellbaren Abständen (5–600 s, Standard 30 s) eine winzige echte Eingabe: Taste **F15** (gibt es auf keiner Tastatur und löst nirgends etwas aus), eine **Mausbewegung** von wenigen Pixeln hin und sofort zurück, oder beides. |
| **Intelligenter Modus** | Greift nur ein, wenn *du* gerade nichts tust. Solange du arbeitest, bleibt StayGreen im Hintergrund. |
| **PC wach halten** | Verhindert Standby, Bildschirmschoner und das automatische Sperren durch Inaktivität. |
| **Zeitplan** | Nur in bestimmten Zeitfenstern aktiv halten, z. B. Mo–Fr 08:00–12:00 und 13:00–17:00. Mehrere Fenster, beliebige Wochentage, auch über Mitternacht (22:00–06:00). Außerhalb pausiert StayGreen und der PC darf schlafen. |
| **Auto-Stopp** | Täglich zu einer Uhrzeit **oder** einmalig an einem Datum beenden, optional mit: **Teams beenden** (Status wechselt auf „Offline“), **Windows sperren**, **Computer herunterfahren** (60-Sekunden-Countdown, abbrechbar, ohne Zwangsbeenden von Programmen) und **StayGreen beenden**. Termine, die der PC verschlafen hat (Standby), werden nicht nachgeholt. |
| **Autostart** | Mit Windows starten, ohne Administratorrechte (Benutzer-Autostart). |
| **Beim Öffnen automatisch starten** | Doppelklick genügt, kein zusätzlicher Klick auf „Starten“. |
| **Infobereich (Tray)** | Minimieren und Schließen legen StayGreen neben die Uhr. Das Symbol zeigt den Zustand (grün, gelb, rot, grau). |
| **Minimiert starten** | Nur Symbol im Infobereich, kein Fenster. |
| **Protokoll** | Schreibt Start, Stopp, Pausen und Auto-Stopp mit Uhrzeit und Laufzeit in eine Textdatei (Standard: `Dokumente\StayGreen-Protokoll.txt`). |
| **Globaler Hotkey** | Starten/Stoppen per Tastenkombination (Standard Strg+Alt+G, frei wählbar). |
| **Deutsch & Englisch** | Automatisch nach Windows-Sprache, umschaltbar. |
| **Portabel** | Eine Datei `StayGreen.portable` neben der EXE → Einstellungen liegen ebenfalls dort (z. B. für USB-Stick). |
| **Einzelinstanz** | Ein zweiter Start holt das vorhandene Fenster nach vorn. |
| **Kommandozeile** | `--start`, `--no-start`, `--minimized`, `--settings <Datei>`, `--lang de\|en`, `--selftest`, `--help`. |

Funktioniert mit Microsoft Teams (neu und klassisch), Skype for Business, Slack, Zoom, Webex und allem, was die Leerlaufzeit von Windows auswertet.

## Wie funktioniert das?

Teams setzt dich nach etwa fünf Minuten ohne Tastatur- und Mausaktivität auf „Abwesend“. Die Leerlaufzeit liefert Windows selbst. StayGreen setzt diesen Zähler über die Windows-Funktion `SendInput` regelmäßig zurück und hält den PC mit `SetThreadExecutionState` wach. Es gibt keine Verbindung zu Teams, keine Anmeldung, keine Schnittstelle zu Microsoft: Teams sieht schlicht einen PC, an dem regelmäßig etwas eingegeben wird.

## Grenzen

- **Gesperrter PC (Win+L):** Beim Sperren zeigt Teams immer „Abwesend“. Dagegen hilft keine Software. StayGreen verhindert nur das *automatische* Sperren durch Inaktivität.
- **Teams im Browser:** Der Browser-Tab muss im Vordergrund sein, sonst zählt die Eingabe dort nicht.
- **Kalender und Anrufe:** Zustände wie „In einer Besprechung“, „Im Anruf“ oder „Nicht stören“ kommen von Teams selbst und bleiben unberührt.
- **Nur Windows** (10 und 11, auch Windows 7/8.1 mit .NET Framework 4.7.2).

## Fehlersuche

**Kommt überhaupt etwas an?**
`StayGreen.exe --selftest` starten (z. B. über eine Verknüpfung mit diesem Zusatz). StayGreen prüft dann auf deinem Rechner, ob Windows Tastendruck und Mausbewegung annimmt und den Leerlaufzähler zurücksetzt, ob der Hotkey auslöst und ob der Autostart schreibbar ist, und zeigt das Ergebnis in einem Fenster. Der Test legt nichts dauerhaft an.

**Der Status wird trotzdem „Abwesend“.**
Auf der Karte *Aktivität* auf **Jetzt testen** klicken. Meldet StayGreen Erfolg, kommt die Eingabe bei Windows an. Dann hilft meist die Methode **Taste und Maus**, ein kürzeres Intervall (30 s) oder das Abschalten des intelligenten Modus. Zeigt die Statuskarte „Eingaben werden abgelehnt“, blockiert Windows die Eingabe (gesperrte Sitzung, getrennte Remote-Sitzung oder Sicherheitssoftware).

**Windows oder der Virenscanner blockiert die Datei.**
Die EXE ist nicht signiert und neu, deshalb warnen SmartScreen und manche Scanner. Du kannst den Hash in `SHA256SUMS.txt` (liegt bei jedem Release) prüfen oder die EXE aus dem Quelltext selbst bauen (siehe unten). Blockiert eine Firmenrichtlinie (z. B. AppLocker) das Ausführen unsignierter Programme aus dem Download-Ordner, bleibt nur die Rücksprache mit der IT.

**Autostart lässt sich nicht einschalten.**
Eine Richtlinie verbietet dann das Schreiben des Autostart-Eintrags. StayGreen meldet das und lässt den Schalter aus.

**Alles zurücksetzen.**
StayGreen beenden, den Ordner `%APPDATA%\StayGreen` löschen. Den Autostart vorher auf der Karte *System* ausschalten (dabei wird der Eintrag unter `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entfernt). Mehr hinterlässt StayGreen nicht.

## Datenschutz und Sicherheit

- Kein Netzwerkzugriff, keine Telemetrie, keine Updates im Hintergrund.
- Läuft ohne Administratorrechte und schreibt nur in `%APPDATA%\StayGreen`, in die Protokolldatei (wenn aktiviert) und in den Benutzer-Autostart (wenn aktiviert).
- Der gesamte Quelltext liegt in diesem Repository. Release-Dateien entstehen ausschließlich in GitHub Actions aus dem veröffentlichten Quelltext.

## Hinweis zur Nutzung am Arbeitsplatz

StayGreen simuliert Eingaben auf *deinem eigenen* Rechner. Ob das am Arbeitsplatz erlaubt ist, regeln die IT- und Arbeitsplatz-Richtlinien deines Arbeitgebers. Informiere dich vorher und nutze es nur, wo es zulässig ist.

StayGreen ist ein unabhängiges Open-Source-Projekt und steht in keiner Verbindung zu Microsoft. „Microsoft Teams“ ist eine Marke von Microsoft. Die Idee ist von Werkzeugen wie „Status Holder“ inspiriert, der Code ist eine vollständige Eigenentwicklung.

## Selbst bauen und entwickeln

Voraussetzung: [.NET SDK 8](https://dotnet.microsoft.com/download) (auf Windows, Linux oder macOS).

```text
dotnet test  tests/StayGreen.Tests -c Release                 # Unit-Tests der Kernlogik
dotnet build src/StayGreen/StayGreen.csproj -c Release        # erzeugt bin/Release/net472/StayGreen.exe
```

Die EXE zielt auf **.NET Framework 4.7.2**, das in Windows 10 (ab 1803) und Windows 11 fest eingebaut ist. Deshalb braucht sie keine Laufzeit-Installation und bleibt eine einzelne Datei.

```text
src/StayGreen/
  Core/       Plattformneutrale Logik: Zeitplan, Auto-Stopp, Einstellungen, Engine, Texte (getestet)
  Platform/   Windows-Schicht: SendInput, Leerlaufzeit, Wach-Halten, Hotkey, Autostart, Selbsttest
  UI/         Oberfläche (WinForms): Hauptfenster, Registerkarten, Tray, Countdown
tests/        xUnit-Tests (laufen auf Linux und Windows)
tools/        make_icon.py erzeugt das Icon ohne Fremdabhängigkeiten
.github/      CI: Tests, Windows-Build, Selbsttest, Start-Test, Release
```

**Release veröffentlichen:** In GitHub unter *Actions → CI → Run workflow* die Option *Release* anhaken (oder ein Tag `v1.0.0` pushen). Die Version steht in `src/StayGreen/StayGreen.csproj` (`<Version>`).

## Lizenz

[MIT](LICENSE)
