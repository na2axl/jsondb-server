using System.Windows;
using System.Diagnostics;
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace JSONDB.UI
{
    public partial class App : Application
    {
        private static Process ServerProcess;

        private static AppSettings Settings = new AppSettings();

        static Mutex mutex = new Mutex(true, ResourceAssembly.FullName);

        [STAThread]
        public static void Main(string[] args)
        {
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                App app = new App();
                app.InitializeComponent();
                app.Run();
                mutex.ReleaseMutex();
            }
        }

        public void InitializeComponent()
        {
            StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
        }

        public static void StartServer()
        {
            string serverCommandLine = String.Empty;

            if (Settings.UseCustomServerAdress)
            {
                serverCommandLine += " -a " + Settings.CustomServerAdress;
            }

            if (null == ServerProcess || ServerProcess.HasExited)
            {
                ServerProcess = new Process();
                ProcessStartInfo StartInfo = new ProcessStartInfo();
                StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                StartInfo.CreateNoWindow = true;
                StartInfo.FileName = "jsondb-server.exe";
                StartInfo.Arguments = serverCommandLine;
                StartInfo.RedirectStandardInput = true;
                StartInfo.RedirectStandardOutput = false;
                StartInfo.UseShellExecute = false;
                ServerProcess.StartInfo = StartInfo;
                ServerProcess.Start();
            }
        }

        public static Process GetProcess()
        {
            return ServerProcess;
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
            if (!ServerProcess.HasExited)
            {
                ServerProcess.StandardInput.WriteLine("exit");
                ServerProcess.WaitForExit();
            }
        }

        public static bool ServerIsStopped()
        {
            return (null == ServerProcess || ServerProcess.HasExited);
        }

        public static void RunElevatedClient(string command)
        {
            try
            {
                Process process = new Process();
                ProcessStartInfo StartInfo = new ProcessStartInfo();
                StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                StartInfo.CreateNoWindow = true;
                StartInfo.FileName = "jsondb-elevated-client.exe";
                StartInfo.Arguments = command;
                StartInfo.Verb = "runas";
                StartInfo.UseShellExecute = true;
                process.StartInfo = StartInfo;
                process.Start();
            }
            catch { /* Operation surely cancelled by the user... */ }
        }
    }
}
