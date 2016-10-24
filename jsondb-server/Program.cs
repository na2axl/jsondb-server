using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Threading.Tasks;
using System.Threading;
using EdgeJs;
using System.Net;
using System.Text.RegularExpressions;

namespace JSONDB.Server
{
    public class Program
    {

        private static IPAddress ServerAddress = IPAddress.Any;

        private static bool UserIsConnected = false;

        private static string Username = String.Empty;
        private static string Password = String.Empty;
        private static string ServerName = String.Empty;
        private static string DatabaseName = String.Empty;

        /// TODO: Uncomment this when you'll implement JSONDB in C#
        // private static Database DB;

        public static void Main(string[] args)
        {
            // Getting the server adsress from the list of arguments
            if (args.Length > 0)
            {
                for (int i = 0, l = args.Length; i < l; ++i)
                {
                    switch (args[i])
                    {
                        case "-a":
                            if (Util.ValidateAddress(args[i + 1]))
                            {
                                ServerAddress = IPAddress.Parse(args[i + 1]);
                            }
                            break;
                    }
                }
            }

            // Create an HTTP Server
            var http = new HTTPServer(ServerAddress, 2717);

            // Add WebSocket Services to the HTTP server
            http.AddWebSocketService<ClientSocketServer>(
                "/",
                () =>
                    new ClientSocketServer()
                    {
                        Protocol = "jsondb",
                        IgnoreExtensions = true,
                        EmitOnPing = false
                    }
            );
            http.AddWebSocketService<APISocketServer>(
                "/jdbwebapi",
                () =>
                    new APISocketServer()
                    {
                        IgnoreExtensions = true,
                        EmitOnPing = false,
                        OriginValidator = (val) =>
                        {
                            // Check the value of the Origin header, and return true if valid.
                            Uri origin;
                            return !val.IsNullOrEmpty()
                                   && Uri.TryCreate(val, UriKind.Absolute, out origin)
                                   && origin.Host == ServerAddress.ToString()
                                   && origin.Port == 2717;
                        }
                    }
            );

            // Start the HTTP server
            http.Start();

            // Continue to recieve commands until we get a "close" command
            Console.Clear();
            Console.WriteLine();
            var IsRunning = true;
            while (IsRunning)
            {
                if (UserIsConnected)
                {
                    Console.Write(" $ " + Username + "@" + ServerName + " >  ");
                }
                else
                {
                    Console.Write(" $ jsondb >  ");
                }
                var command = Console.ReadLine();
                if (command.ToLower().StartsWith("connect"))
                {
                    var cmd_args = command.Remove(0, 7).TrimStart().Split(' ');
                    string username = String.Empty;
                    string password = String.Empty;
                    string server   = String.Empty;
                    string database = String.Empty;

                    password = new Regex("\"(.*)\"", RegexOptions.IgnoreCase).Replace(new Regex("-p \".*\"", RegexOptions.IgnoreCase).Match(command).Value.Replace("-p ", ""), "$1");

                    for (int i = 0, l = cmd_args.Length; i < l; ++i)
                    {
                        switch (cmd_args[i])
                        {
                            case "-u":
                                username = cmd_args[i + 1];
                                break;
                            case "-s":
                                server = cmd_args[i + 1];
                                break;
                            case "-d":
                                database = cmd_args[i + 1];
                                break;
                        }
                    }

                    if (server == String.Empty)
                    {
                        Console.WriteLine("Cannot connect to a server. No server provided. Use \"-s ServerName\" to give the name of the server to use with the connection.");
                    }
                    else
                    {
                        if (username == String.Empty)
                        {
                            Console.WriteLine("Cannot connect to a server. No user provided. Use \"-u Username\" to give the username.");
                        }
                        else
                        {
                            var ServerList = Configuration.GetConfigFile("users");
                            if (ServerList[server] != null)
                            {
                                if (ServerList[server]["username"].ToString() == Util.Crypt(username) && ServerList[server]["password"].ToString() == Util.Crypt(password))
                                {
                                    Username = username;
                                    Password = password;
                                    ServerName = server;
                                    DatabaseName = database;

                                    /// TODO: Uncomment this when JSONDB will be implemented in C#
                                    // DB = JSONDB.Connect(Username, Password, ServerName, DatabaseName);

                                    UserIsConnected = true;
                                }
                                else
                                {
                                    Console.WriteLine("Cannot connect to a server. There is an error with the username or with the password.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Cannot connect to a server. No server found with the given name.");
                            }
                        }
                    }
                }
                else if (command.ToLower().StartsWith("disconnect"))
                {
                    Username = String.Empty;
                    Password = String.Empty;
                    ServerName = String.Empty;
                    DatabaseName = String.Empty;
                    // DB.Disconnect();
                    UserIsConnected = false;
                }
                else if (command.ToLower().StartsWith("quit") || command.ToLower().StartsWith("exit") || command.ToLower().StartsWith("close"))
                {
                    if (UserIsConnected)
                    {
                        // DB.Disconnect();
                    }
                    IsRunning = false;
                }
                else
                {
                    Console.WriteLine("Unknow command.");
                }
                Console.WriteLine();
            }

            // Stop the server when a "close" command is recieved
            http.Stop();
        }

    }
}
