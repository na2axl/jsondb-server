using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Diagnostics;

namespace JSONDB.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static Process ServerProcess { get; set; }

        public void SetMainWindow(Window m)
        {
            MainWindow = m;
        }

        public static void StartServer()
        {
            if (null == ServerProcess || ServerProcess.HasExited)
            {
                ServerProcess = new Process();
                ProcessStartInfo StartInfo = new ProcessStartInfo();
                StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                StartInfo.FileName = "jsondb-server.exe";
                StartInfo.Arguments = "";
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
            KillServer();
            base.OnExit(e);
        }

        public static void KillServer()
        {
            if (!ServerProcess.HasExited)
            {
                ServerProcess.Kill();
                ServerProcess.WaitForExit();
            }
        }

        public static bool ServerIsStopped()
        {
            return (null == ServerProcess || ServerProcess.HasExited);
        }
    }
}
