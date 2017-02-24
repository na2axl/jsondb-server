using System.Windows;
using System.Diagnostics;
using System;
using System.Threading;

namespace JSONDB.UI
{
    public class App : Application
    {
        private static Process _serverProcess;

        private static readonly AppSettings Settings = new AppSettings();

        private static readonly Mutex Mutex = new Mutex(true, ResourceAssembly.FullName);

        [STAThread]
        public static void Main(string[] args)
        {
            if (Mutex.WaitOne(TimeSpan.Zero, true))
            {
                var app = new App();
                app.InitializeComponent();
                app.Run();
                Mutex.ReleaseMutex();
            }
        }

        public void InitializeComponent()
        {
            StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
        }

        public static void StartServer()
        {
            var serverCommandLine = string.Empty;

            if (Settings.UseCustomServerAdress)
            {
                serverCommandLine += " -a " + Settings.CustomServerAdress;
            }

            if (null == _serverProcess || _serverProcess.HasExited)
            {
                _serverProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        FileName = "jsondb-server.exe",
                        Arguments = serverCommandLine,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = false,
                        UseShellExecute = false
                    }
                };

                _serverProcess.Start();
            }
        }

        public static Process GetProcess()
        {
            return _serverProcess;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            StartServer();
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            StopServer();
            base.OnExit(e);
        }

        public static void StopServer()
        {
            if (!_serverProcess.HasExited)
            {
                _serverProcess.StandardInput.WriteLine("exit");
                _serverProcess.WaitForExit();
            }
        }

        public static bool ServerIsStopped()
        {
            return (null == _serverProcess || _serverProcess.HasExited);
        }

        public static void RunElevatedClient(string command)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        FileName = "jsondb-elevated-client.exe",
                        Arguments = command,
                        Verb = "runas",
                        UseShellExecute = true
                    }
                };
                process.Start();
            }
            catch { /* Operation surely cancelled by the user... */ }
        }
    }
}
