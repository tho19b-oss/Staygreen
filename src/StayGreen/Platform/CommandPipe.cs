using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace StayGreen.Platform
{
    /// <summary>
    /// Kanal von einem zweiten Start (oder einem Skript) zur laufenden StayGreen-Instanz: eine Textzeile wie
    /// "stop" oder "pause 30" (siehe <see cref="StayGreen.Core.RemoteCommand"/>). Technisch eine Named Pipe, die nur dem
    /// eigenen Benutzerkonto Zugriff gibt, gilt also nur innerhalb der Windows-Sitzung und nie ueber das Netzwerk.
    /// </summary>
    sealed class CommandPipe : IDisposable
    {
        readonly string _name;
        Thread _listener;
        volatile bool _disposed;

        public CommandPipe(string name = null)
        {
            _name = name ?? DefaultName;
        }

        /// <summary>Pro Windows-Sitzung und Benutzer ein eigener Name, damit sich mehrere Anmeldungen nicht in die Quere kommen.</summary>
        public static string DefaultName
        {
            get
            {
                string user;
                try { user = WindowsIdentity.GetCurrent().User.Value; }
                catch { user = Environment.UserName; }
                int session;
                try { session = Process.GetCurrentProcess().SessionId; }
                catch { session = 0; }
                return "StayGreen.Cmd." + session + "." + user;
            }
        }

        /// <summary>Erste Instanz: ruft <paramref name="onLine"/> fuer jede empfangene Zeile auf (auf einem Hintergrundthread).</summary>
        public void Listen(Action<string> onLine)
        {
            _listener = new Thread(() => Loop(onLine))
            {
                IsBackground = true,
                Name = "StayGreen-Commands",
            };
            _listener.Start();
        }

        void Loop(Action<string> onLine)
        {
            PipeSecurity security;
            try
            {
                security = new PipeSecurity();
                security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,
                    PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
            }
            catch
            {
                return;    // Ohne einschraenkende Rechte lieber gar keinen Kanal anbieten.
            }

            while (!_disposed)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(_name, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                               PipeOptions.None, 0, 0, security))
                    {
                        server.WaitForConnection();
                        if (_disposed) return;
                        using (var reader = new StreamReader(server, new UTF8Encoding(false)))
                        {
                            string line = reader.ReadLine();
                            if (!string.IsNullOrEmpty(line)) onLine(line);
                        }
                    }
                }
                catch (Exception)
                {
                    if (_disposed) return;
                    Thread.Sleep(500);
                }
            }
        }

        /// <summary>Zweite Instanz: schickt eine Zeile an die laufende Instanz. False, wenn keine antwortet.</summary>
        public static bool Send(string line, string name = null, int timeoutMs = 2000)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", name ?? DefaultName, PipeDirection.Out, PipeOptions.None))
                {
                    client.Connect(timeoutMs);
                    byte[] data = new UTF8Encoding(false).GetBytes(line + "\n");
                    client.Write(data, 0, data.Length);
                    client.Flush();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _disposed = true;   // Der Hintergrundthread endet mit dem Prozess.
        }
    }
}
