using System.Windows;
using System.Diagnostics;

namespace JSONDB.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Process ServerProcess;

        private static AppSettings Settings = new AppSettings();

        public static void StartServer()
        {
            string serverCommandLine = "";

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
    }
}
