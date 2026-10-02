using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using StayGreen.Core;

namespace StayGreen.Platform
{
    static class PlatformInfo
    {
        public static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
    }

    /// <summary>
    /// Teams beenden, Windows sperren, Herunterfahren und nachsehen, ob Teams laeuft. Auf anderen Systemen
    /// bewusst ohne Wirkung.
    /// </summary>
    sealed class WindowsSystemActions : ISystemActions, ITeamsProbe
    {
        // Neues Teams ("ms-teams.exe") und klassisches Teams ("Teams.exe").
        static readonly string[] TeamsProcesses = { "ms-teams", "Teams" };

        public bool IsTeamsRunning()
        {
            if (!PlatformInfo.IsWindows) return true;
            foreach (string name in TeamsProcesses)
            {
                Process[] processes;
                try { processes = Process.GetProcessesByName(name); }
                catch { continue; }

                bool any = processes.Length > 0;
                foreach (Process p in processes) p.Dispose();
                if (any) return true;
            }
            return false;
        }

        public void CloseTeams()
        {
            if (!PlatformInfo.IsWindows) return;
            foreach (string name in TeamsProcesses)
            {
                Process[] processes;
                try { processes = Process.GetProcessesByName(name); }
                catch { continue; }

                foreach (Process p in processes)
                {
                    try
                    {
                        // Erst hoeflich schliessen; Teams versteckt sich aber gern im Infobereich,
                        // deshalb nach kurzer Wartezeit beenden. Der Nutzer wurde vorher gewarnt (Auto-Stopp-Vorwarnung).
                        if (!p.CloseMainWindow() || !p.WaitForExit(3000))
                            p.Kill();
                    }
                    catch
                    {
                        // Prozess schon weg oder kein Zugriff: ignorieren.
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
        }

        public void LockWorkstation()
        {
            if (PlatformInfo.IsWindows) NativeMethods.LockWorkStation();
        }

        public void Shutdown()
        {
            if (!PlatformInfo.IsWindows) return;
            try
            {
                // Ohne /f: Programme mit ungespeicherten Daten werden nicht gewaltsam beendet.
                Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 3")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });
            }
            catch
            {
            }
        }
    }

    /// <summary>Start mit Windows ueber den Benutzer-Autostart (HKCU\...\Run, keine Administratorrechte noetig).</summary>
    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string DefaultValueName = "StayGreen";

        public static string ExpectedCommand
        {
            get { return "\"" + Application.ExecutablePath + "\" --autostart"; }
        }

        public static bool IsEnabled(string valueName = DefaultValueName)
        {
            if (!PlatformInfo.IsWindows) return false;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key != null && key.GetValue(valueName) != null;
            }
            catch
            {
                return false;
            }
        }

        public static string CurrentCommand(string valueName = DefaultValueName)
        {
            if (!PlatformInfo.IsWindows) return null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key == null ? null : key.GetValue(valueName) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>True, wenn der gewuenschte Zustand erreicht ist (Richtlinien koennen das Schreiben verbieten).</summary>
        public static bool Set(bool enable, string valueName = DefaultValueName, string command = null)
        {
            if (!PlatformInfo.IsWindows) return !enable;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return false;
                    if (enable) key.SetValue(valueName, command ?? ExpectedCommand);
                    else key.DeleteValue(valueName, false);
                }
                return IsEnabled(valueName) == enable;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Einstellungen auf der Platte. Portabel, wenn "StayGreen.portable" neben der EXE liegt.</summary>
    static class SettingsStore
    {
        static string _overridePath;

        public static void UseFile(string path)
        {
            _overridePath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        }

        public static bool IsPortable
        {
            get { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StayGreen.portable")); }
        }

        public static string Directory
        {
            get
            {
                if (_overridePath != null) return Path.GetDirectoryName(_overridePath);
                if (IsPortable) return AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StayGreen");
            }
        }

        public static string FilePath
        {
            get { return _overridePath ?? Path.Combine(Directory, "settings.ini"); }
        }

        /// <summary>
        /// Standardort des Protokolls: im Einstellungsordner (bzw. neben der EXE im portablen Modus). Der Ordner
        /// "Dokumente" wird auf vielen Firmen-PCs per OneDrive in die Cloud synchronisiert und taugt deshalb nicht
        /// als Vorgabe. Wer sein Protokoll schon dort hat (Version 1.1 und aelter), behaelt es, damit nichts reisst.
        /// </summary>
        public static string DefaultLogPath
        {
            get
            {
                const string name = "StayGreen-Protokoll.txt";
                if (IsPortable || _overridePath != null) return Path.Combine(Directory, name);

                try
                {
                    string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (!string.IsNullOrEmpty(documents))
                    {
                        string legacy = Path.Combine(documents, name);
                        if (File.Exists(legacy)) return legacy;
                    }
                }
                catch
                {
                    // Dokumente nicht erreichbar: neuer Standard.
                }
                return Path.Combine(Directory, name);
            }
        }

        /// <summary>Meldung des letzten fehlgeschlagenen Speicherns; null, wenn alles gut ging.</summary>
        public static string LastError { get; private set; }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return Settings.Parse(File.ReadAllText(FilePath));
            }
            catch
            {
                // Kaputte oder gesperrte Datei: mit Standardwerten weiterarbeiten.
            }
            return Settings.Parse(null);
        }

        /// <summary>
        /// Atomar schreiben (erst Temp-Datei, dann in einem Schritt ersetzen), damit nie eine halbe oder fehlende
        /// Datei bleibt. Bei einem Fehler steht der Grund in <see cref="LastError"/>, damit die Oberflaeche ihn zeigen kann.
        /// </summary>
        public static bool Save(Settings settings)
        {
            try
            {
                AtomicFile.WriteAllText(FilePath, settings.Serialize(), new System.Text.UTF8Encoding(false));
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }
    }

    /// <summary>Nur eine Instanz pro Windows-Sitzung. Ein zweiter Start holt das Fenster der ersten nach vorn.</summary>
    sealed class SingleInstance : IDisposable
    {
        const string MutexName = @"Local\StayGreen.SingleInstance";
        const string ShowEventName = @"Local\StayGreen.ShowWindow";

        Mutex _mutex;
        EventWaitHandle _showEvent;
        Thread _listener;
        volatile bool _disposed;

        public bool IsFirst { get; private set; }

        public SingleInstance()
        {
            try
            {
                bool created;
                _mutex = new Mutex(true, MutexName, out created);
                IsFirst = created;
                _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            }
            catch
            {
                // Im Zweifel lieber starten als stillschweigend nichts tun.
                IsFirst = true;
            }
        }

        /// <summary>Zweite Instanz: erste Instanz bitten, ihr Fenster zu zeigen.</summary>
        public void SignalFirstInstance()
        {
            try { if (_showEvent != null) _showEvent.Set(); }
            catch { }
        }

        /// <summary>Erste Instanz: bei jedem Signal <paramref name="onShow"/> aufrufen (auf einem Hintergrundthread).</summary>
        public void Listen(Action onShow)
        {
            if (_showEvent == null) return;
            _listener = new Thread(() =>
            {
                while (!_disposed)
                {
                    try
                    {
                        if (_showEvent.WaitOne(500) && !_disposed) onShow();
                    }
                    catch
                    {
                        return;
                    }
                }
            })
            {
                IsBackground = true,
                Name = "StayGreen-SingleInstance",
            };
            _listener.Start();
        }

        public void Dispose()
        {
            _disposed = true;
            try { if (IsFirst && _mutex != null) _mutex.ReleaseMutex(); } catch { }
            try { if (_mutex != null) _mutex.Dispose(); } catch { }
            try { if (_showEvent != null) _showEvent.Dispose(); } catch { }
        }
    }

    /// <summary>Unsichtbares Fenster, das WM_HOTKEY empfaengt (unabhaengig davon, ob das Hauptfenster sichtbar ist).</summary>
    sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        const int HotkeyId = 0x5347;
        bool _registered;

        public event Action Pressed;

        public HotkeyWindow()
        {
            if (!PlatformInfo.IsWindows) return;
            // HWND_MESSAGE (-3): reines Nachrichtenfenster, taucht nirgends auf.
            CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
        }

        /// <summary>Meldet die Tastenkombination an. False, wenn sie schon belegt oder nicht moeglich ist.</summary>
        public bool Register(Settings s)
        {
            Unregister();
            if (!PlatformInfo.IsWindows || Handle == IntPtr.Zero || !s.HotkeyEnabled) return false;

            uint vk;
            if (!HotkeyInfo.TryGetVirtualKey(s.HotkeyKey, out vk)) return false;

            uint mods = NativeMethods.MOD_NOREPEAT;
            if (s.HotkeyCtrl) mods |= NativeMethods.MOD_CONTROL;
            if (s.HotkeyAlt) mods |= NativeMethods.MOD_ALT;
            if (s.HotkeyShift) mods |= NativeMethods.MOD_SHIFT;
            if (s.HotkeyWin) mods |= NativeMethods.MOD_WIN;

            _registered = NativeMethods.RegisterHotKey(Handle, HotkeyId, mods, vk);
            return _registered;
        }

        public void Unregister()
        {
            if (_registered && Handle != IntPtr.Zero) NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                Action handler = Pressed;
                if (handler != null) handler();
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            Unregister();
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }
}
