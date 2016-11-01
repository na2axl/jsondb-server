using JSONDB.Library;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Text.RegularExpressions;
using WebSocketSharp;

namespace JSONDB.Server
{
    public class Program
    {
        /// <summary>
        /// The JSONDB server address.
        /// </summary>
        private static IPAddress ServerAddress = IPAddress.Any;

        /// <summary>
        /// Get the value determinating if an user is currently connected through the console.
        /// </summary>
        private static bool UserIsConnected = false;

        /// <summary>
        /// The name of the currently used server.
        /// </summary>
        private static string ServerName = String.Empty;

        /// <summary>
        /// The Database instance used with the current connection.
        /// </summary>
        private static Database DB;

        /// <summary>
        /// Check if the server is running.
        /// </summary>
        private static bool IsRunning = true;

        /// <summary>
        /// Check if a server is already running. Used to allow multi instance for the console.
        /// </summary>
        private static bool AServerIsRunning = false;

        /// <summary>
        /// The main program logic.
        /// </summary>
        /// <param name="args">Program arguments passed through the console</param>
        public static void Main(string[] args)
        {
            // Getting the server adsress from the list of arguments
            if (args.Length > 0)
            {
                for (int i = 0, l = args.Length; i < l; i++)
                {
                    switch (args[i])
                    {
                        case "-a":
                            if (Util.ValidateAddress(args[i + 1]))
                            {
                                if (Util.TestServerAddress(args[i + 1]))
                                {
                                    ServerAddress = IPAddress.Parse(args[i + 1]);
                                }
                            }
                            break;
                    }
                }
            }

            HTTPServer http = null;

            try
            {
                // Create an HTTP Server
                http = new HTTPServer(ServerAddress, 2717);

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
            }
            catch (Exception)
            {
                AServerIsRunning = true;
            }

            // If the JSONDB Server is launched as a console
            if (!Console.IsInputRedirected)
            {
                Console.WriteLine("Welcome to JSONDB Server");
                Console.WriteLine("Copyright (c) 2016 Centers Technologies. All rights reserved.");
                Console.WriteLine("Server version 1.0.0");
                Console.WriteLine();
                Console.WriteLine("The server is listening for incomming connections at the address jsondb://" + ServerAddress.ToString() + ":2717");
                Console.WriteLine("The server is serving the web administration interface at the address http://" + ServerAddress.ToString() + ":2717");
                Console.WriteLine();
                Console.WriteLine("Type 'help' for the list of available commands.");
                Console.WriteLine();

                // Continue to recieve commands until we get a "close" command
                while (IsRunning)
                {
                    if (UserIsConnected)
                    {
                        Console.Write("$ " + DB.GetUsername() + "@" + ServerName + " >  ");
                    }
                    else
                    {
                        Console.Write("$ jsondb >  ");
                    }

                    var command = Console.ReadLine();
                    Console.WriteLine();

                    if (command.Trim() == String.Empty)
                    {
                        // Do nothing...
                    }
                    else if (command.ToLower().StartsWith("help"))
                    {
                        Console.WriteLine("Use help [commandName] for a detailed help about a command.");
                        Console.WriteLine();
                        Console.WriteLine("mkserver          Create a new server.");
                        Console.WriteLine("connect           Connect to a server.");
                        Console.WriteLine("mkdatabase        Create a new database.");
                        Console.WriteLine("cd                Change the current working database.");
                        Console.WriteLine("mktable           Create a new table in the current working database.");
                        Console.WriteLine("query             Execute a query.");
                        Console.WriteLine("desc server       Show the list of databases in the current working server.");
                        Console.WriteLine("desc database     Show the list of tables in the current working database.");
                        Console.WriteLine("disconnect        Disconnect from a server.");
                        Console.WriteLine("close             Disconnect and close the console.");
                        Console.WriteLine("clear             Clear the console.");
                    }
                    else if (command.ToLower().StartsWith("mkserver"))
                    {
                        ExecMkServer(command);
                    }
                    else if (command.ToLower().StartsWith("desc"))
                    {
                        ExecDesc(command);
                    }
                    else if (command.ToLower().StartsWith("connect"))
                    {
                        ExecConnect(command);
                    }
                    else if (command.ToLower().StartsWith("mkdatabase"))
                    {
                        ExecMkDatabase(command);
                    }
                    else if (command.ToLower().StartsWith("cd"))
                    {
                        ExecChangeDatabase(command);
                    }
                    else if (command.ToLower().StartsWith("mktable"))
                    {
                        ExecMkTable(command);
                    }
                    else if (command.ToLower().StartsWith("query"))
                    {
                        ExecQuery(command);
                    }
                    else if (command.ToLower().StartsWith("disconnect"))
                    {
                        ExecDisconnect(command);
                    }
                    else if (command.ToLower().StartsWith("quit") || command.ToLower().StartsWith("exit") || command.ToLower().StartsWith("close") || command.ToLower().StartsWith("shutdown"))
                    {
                        ExecClose(command);
                    }
                    else if (command.ToLower().StartsWith("clear"))
                    {
                        Console.Clear();
                    }
                    else
                    {
                        Console.WriteLine("Unknow command.");
                    }

                    Console.WriteLine();
                }
            }
            // Otherwise JSONDB Server is launched with the UI
            else
            {
                // Continue to recieve queries until we get a "close" command
                while (IsRunning)
                {
                    string command = Console.In.ReadLine();
                    if (command.ToLower().StartsWith("quit") || command.ToLower().StartsWith("exit") || command.ToLower().StartsWith("close") || command.ToLower().StartsWith("shutdown"))
                    {
                        ExecClose(command);
                    }
                }
            }

            if (!AServerIsRunning)
            {
                // Stop the server when a "close" command is recieved
                http.Stop();
            }
        }

        private static void ExecDesc(string command)
        {
            string descWhat = command.Remove(0, 4).Trim();
            int max_length = 0;

            switch (descWhat)
            {
                case "server":
                    if (UserIsConnected)
                    {
                        string[] databases = Database.GetDatabaseList(ServerName);
                        max_length = 4;
                        foreach (var database in databases)
                        {
                            max_length = Math.Max(max_length, database.Length);
                        }
                        max_length += 2;
                        Console.Write("+");
                        for (int i = 0; i < max_length; i++)
                        {
                            Console.Write("-");
                        }
                        Console.Write("+");
                        Console.WriteLine();
                        Console.Write("|");
                        for (int i = 0; i < (max_length / 2) - 2; i++)
                        {
                            Console.Write(" ");
                        }
                        Console.Write("Name");
                        for (int i = (max_length / 2) + 2; i < max_length; i++)
                        {
                            Console.Write(" ");
                        }
                        Console.Write("|");
                        Console.WriteLine();
                        Console.Write("+");
                        for (int i = 0; i < max_length; i++)
                        {
                            Console.Write("-");
                        }
                        Console.Write("+");
                        Console.WriteLine();
                        foreach (var database in databases)
                        {
                            Console.Write("| ");
                            Console.Write(database);
                            for (int i = 1 + database.Length; i < max_length; i++)
                            {
                                Console.Write(" ");
                            }
                            Console.Write("|");
                            Console.WriteLine();
                        }
                        Console.Write("+");
                        for (int i = 0; i < max_length; i++)
                        {
                            Console.Write("-");
                        }
                        Console.Write("+");
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine("You are not connected to a server. Use the command \"connect\" first.");
                    }
                    break;
                case "database":
                    if (UserIsConnected)
                    {
                        if (DB.IsWorkingDatabase())
                        {
                            string[] tables = DB.GetTableList();
                            max_length = 4;
                            foreach (var table in tables)
                            {
                                max_length = Math.Max(max_length, table.Length);
                            }
                            max_length += 2;
                            Console.Write("+");
                            for (int i = 0; i < max_length; i++)
                            {
                                Console.Write("-");
                            }
                            Console.Write("+");
                            Console.WriteLine();
                            Console.Write("|");
                            for (int i = 0; i < (max_length / 2) - 2; i++)
                            {
                                Console.Write(" ");
                            }
                            Console.Write("Name");
                            for (int i = (max_length / 2) + 2; i < max_length; i++)
                            {
                                Console.Write(" ");
                            }
                            Console.Write("|");
                            Console.WriteLine();
                            Console.Write("+");
                            for (int i = 0; i < max_length; i++)
                            {
                                Console.Write("-");
                            }
                            Console.Write("+");
                            Console.WriteLine();
                            foreach (var table in tables)
                            {
                                Console.Write("| ");
                                Console.Write(table);
                                for (int i = 1 + table.Length; i < max_length; i++)
                                {
                                    Console.Write(" ");
                                }
                                Console.Write("|");
                                Console.WriteLine();
                            }
                            Console.Write("+");
                            for (int i = 0; i < max_length; i++)
                            {
                                Console.Write("-");
                            }
                            Console.Write("+");
                            Console.WriteLine();
                        }
                        else
                        {
                            Console.WriteLine("No database selected. Use the command \"cd DatabaseName\" first. For the list of databases, use the command \"desc server\".");
                        }
                    }
                    else
                    {
                        Console.WriteLine("You are not connected to a server. Use the command \"connect\" first.");
                    }
                    break;
                default:
                    Console.WriteLine("Bad command, use \"server\", \"database\" or \"table\", with the command \"desc\".");
                    break;
            }
        }

        private static void ExecMkDatabase(string command)
        {
            string database = command.Remove(0, 10).Trim();

            while (database == String.Empty)
            {
                Console.Write(" -> Database name: ");
                database = Console.ReadLine().Trim().Split(' ')[0];
            }

            try
            {
                DB.CreateDatabase(database);
                Console.WriteLine("Database created.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to create the database: " + e.Message);
            }
        }

        private static void ExecQuery(string command)
        {
            if (UserIsConnected)
            {
                string query = command.Remove(0, 5).Trim();

                while (query == String.Empty)
                {
                    Console.WriteLine(" -> ");
                    query = Console.ReadLine().Trim();
                }

                try
                {
                    Console.WriteLine(DB.Query(query).ToString());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to execute the query: " + e.Message);
                }
            }
            else
            {
                Console.WriteLine("You are not connected to a server, use the command \"connect\" first before send a query.");
            }
        }

        private static void ExecMkServer(string command)
        {
            string[] parts = command.Remove(0, 8).Trim().Split(' ');

            string username = String.Empty;
            string password = String.Empty;
            string server = String.Empty;

            password = new Regex("\"(.*)\"", RegexOptions.IgnoreCase).Replace(new Regex("-p \".*\"", RegexOptions.IgnoreCase).Match(command).Value.Replace("-p ", ""), "$1");

            for (int i = 0, l = parts.Length; i < l; i++)
            {
                switch (parts[i])
                {
                    case "-s":
                        server = parts[i + 1];
                        break;
                    case "-u":
                        username = parts[i + 1];
                        break;
                }
            }

            while (server == String.Empty)
            {
                Console.Write(" -> Server name: ");
                server = Console.ReadLine().Trim().Split(' ')[0];
            }

            while (username == String.Empty)
            {
                Console.Write(" -> Username: ");
                username = Console.ReadLine().Trim();
            }

            if (command.Trim().Length == 8 && password == String.Empty)
            {
                Console.Write(" -> Password (Leave blank to ignore (not recommended)): ");
                password = Console.ReadLine();
            }

            try
            {
                Library.JSONDB.CreateServer(server, username, password);
                Console.WriteLine("Server created.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to create the server: " + e.Message);
            }
        }

        private static void ExecChangeDatabase(string command)
        {
            if (UserIsConnected)
            {
                string database = command.Remove(0, 2).Trim().Split(' ')[0];
                if (database == String.Empty)
                {
                    Console.WriteLine("Use: cd DatabaseName");
                }
                else
                {
                    if (DB.Exists(database))
                    {
                        try
                        {
                            DB.SetDatabase(database);
                            Console.WriteLine("Database changed.");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Unable to change the working datbabase: " + e.Message);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unable to find the database \"" + database + "\" in the current server.");
                    }
                }
            }
            else
            {
                Console.WriteLine("You are not connected to a server. Use the command \"connect\" first.");
            }
        }

        private static void ExecClose(string command)
        {
            if (UserIsConnected)
            {
                DB.Disconnect();
            }
            IsRunning = false;
        }

        private static void ExecDisconnect(string command)
        {
            ServerName = String.Empty;
            DB.Disconnect();
            UserIsConnected = false;
        }

        private static void ExecMkTable(string command)
        {
            if (UserIsConnected)
            {
                string table = command.Remove(0, 7).Trim();
                if (DB.IsWorkingDatabase())
                {
                    while (table == String.Empty)
                    {
                        Console.Write(" -> Table name: ");
                        table = Console.ReadLine().Trim().Split(' ')[0];
                    }
                    JObject prototype = new JObject();
                    bool IsAddindFields = true;
                    do
                    {
                        Console.Write("Do you want to add a new column? (Yes/No) ");
                        string res = Console.ReadLine().Trim().ToLower();
                        if (res == "yes" || res == "y")
                        {
                            Console.Write(" -> Column name: ");
                            string c_name = Console.ReadLine().Trim();
                            Console.Write(" -> Column type: ");
                            string c_type = Console.ReadLine().Trim();
                            Console.Write(" -> Default value (Leave blank to ignore): ");
                            string c_defv = Console.ReadLine().Trim();
                            Console.Write(" -> Max length (Leave blank to ignore): ");
                            string c_maxl = Console.ReadLine().Trim();
                            Console.Write(" -> Not null (Yes/No): ");
                            string c_null = Console.ReadLine().Trim().ToLower();
                            Console.Write(" -> Auto increment (Yes/No): ");
                            string c_auto = Console.ReadLine().Trim().ToLower();
                            Console.Write(" -> Primary key (Yes/No): ");
                            string c_pkey = Console.ReadLine().Trim().ToLower();
                            Console.Write(" -> Unique key (Yes/No): ");
                            string c_ukey = Console.ReadLine().Trim().ToLower();

                            JObject properties = new JObject();
                            properties["type"] = c_type ?? "string";
                            if (c_defv != String.Empty) properties["default"] = c_defv;
                            if (c_maxl != String.Empty) properties["max_length"] = c_maxl;
                            if (c_null == "yes" || c_null == "y") properties["not_null"] = true;
                            if (c_auto == "yes" || c_auto == "y") properties["auto_increment"] = true;
                            if (c_pkey == "yes" || c_pkey == "y") properties["primary_key"] = true;
                            if (c_ukey == "yes" || c_ukey == "y") properties["unique_key"] = true;

                            prototype[c_name] = properties;
                        }
                        else
                        {
                            IsAddindFields = false;
                        }
                    } while (IsAddindFields);
                    Console.WriteLine("The table \"" + table + "\" will be created with these columns:");
                    Console.WriteLine(prototype.ToString());
                    Console.Write("It's OK? (Yes/No) ");
                    string ok = Console.ReadLine().Trim().ToLower();
                    if (ok == "yes" || ok == "y")
                    {
                        try
                        {
                            DB.CreateTable(table, prototype);
                            Console.WriteLine("Table created.");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("An error occured when creating the table: " + e.Message);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Table creation cancelled.");
                    }
                }
                else
                {
                    Console.WriteLine("No working database found. Use the command \"cd DatabaseName\" to use a database.");
                }
            }
            else
            {
                Console.WriteLine("You are not connected to a server. Use the command \"connect\" first.");
            }
        }

        private static void ExecConnect(string command)
        {
            var cmd_args = command.Remove(0, 7).TrimStart().Split(' ');
            string username = String.Empty;
            string password = String.Empty;
            string server = String.Empty;
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
                            ServerName = server;

                            try
                            {
                                DB = Library.JSONDB.Connect(ServerName, username, password, database);
                                UserIsConnected = true;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Unable to connect to the database: " + e.Message);
                            }
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
    }
}
