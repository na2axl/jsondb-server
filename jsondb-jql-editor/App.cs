using JSONDB.Library;
using System;
using System.Windows;

namespace JSONDB.JQLEditor
{
    public class App : Application
    {
        private static Database _db;
        private static string _cwf = String.Empty;

        public static string CurrentWorkingFile
        {
            get { return _cwf; }
            set
            {
                if (value != String.Empty && !Util.Exists(value))
                {
                    Util.WriteTextFile(value, "");
                }
                _cwf = value;
            }
        }
        public static Database DBConnection
        {
            get { return _db; }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            CurrentWorkingFile = args.Length > 0 ? args[0] : String.Empty;

            App app = new App();
            app.InitializeComponent();
            app.Run();
        }

        private void InitializeComponent()
        {
            StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
        }

        /// <summary>
        /// Check if the editor is connected to a server.
        /// </summary>
        /// <returns>true if connected and false otherwise</returns>
        public static bool IsConnected()
        {
            if (DBConnection != null)
            {
                return DBConnection.IsConnected();
            }

            return false;
        }

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="serverName">The name of the server</param>
        /// <param name="username">The username</param>
        /// <param name="password">The password</param>
        /// <param name="database">The name of the database to use</param>
        public static void Connect(string serverName, string username, string password, string database)
        {
            _db = Library.JSONDB.Connect(serverName, username, password, database);
        }

        /// <summary>
        /// Disconnect from the server.
        /// </summary>
        public static void Disconnect()
        {
            DBConnection.Disconnect();
        }
    }
}
