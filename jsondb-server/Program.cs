using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using WebSocketSharp;

namespace JSONDB.Server
{
    internal class Program
    {
        /// <summary>
        /// The JSONDB server address.
        /// </summary>
        private static IPAddress _serverAddress = IPAddress.Any;

        /// <summary>
        /// Get the value determinating if an user is currently connected through the console.
        /// </summary>
        private static bool _userIsConnected;

        /// <summary>
        /// The name of the currently used server.
        /// </summary>
        private static string _serverName = string.Empty;

        /// <summary>
        /// The Database instance used with the current connection.
        /// </summary>
        private static Database _db;

        /// <summary>
        /// Check if the server is running.
        /// </summary>
        private static bool _isRunning = true;

        /// <summary>
        /// Check if a server is already running. Used to allow multi instance for the console.
        /// </summary>
        private static bool _aServerIsRunning = false;

        /// <summary>
        /// The main program logic.
        /// </summary>
        /// <param name="args">Program arguments passed through the console</param>
        public static void Main(string[] args)
        {
            // Getting the server address from the list of arguments
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
                                    _serverAddress = IPAddress.Parse(args[i + 1]);
                                }
                            }
                            break;

                        case "-noServer":
                            _aServerIsRunning = true;
                            break;
                    }
                }
            }

            Server web = null;
            System.Threading.Thread qThread = null;

            if (!_aServerIsRunning)
            {
                try
                {
                    // Create an HTTP Server
                    web = new Server(_serverAddress, 2717);

                    // Add WebSocket Services to the HTTP server
                    web.AddWebSocketService(
                        "/",
                        () =>
                            new ClientSocketServer
                            {
                                Protocol = "jsondb",
                                IgnoreExtensions = true,
                                EmitOnPing = false
                            }
                    );
                    web.AddWebSocketService(
                        "/jdbwebapi",
                        () =>
                            new ApiSocketServer
                            {
                                IgnoreExtensions = true,
                                EmitOnPing = false,
                                OriginValidator = (val) =>
                                {
                                // Check the value of the Origin header, and return true if valid.
                                Uri origin;
                                    return !val.IsNullOrEmpty()
                                           && Uri.TryCreate(val, UriKind.Absolute, out origin)
                                           && origin.Host == _serverAddress.ToString()
                                           && origin.Port == 2717;
                                }
                            }
                    );

                    // Start the HTTP server
                    web.Start();

                    // Execute queries while the server is running
                    qThread = new System.Threading.Thread((state) =>
                    {
                        while (_isRunning)
                        {
                            if (ClientSocketServer.Pools.Count > 0 && !ClientSocketServer.Executing)
                            {
                                ClientSocketServer.Pools.Dequeue().Send();
                            }
                        }
                    });

                    qThread.Start();
                }
                catch (System.Net.Sockets.SocketException)
                {
                    _aServerIsRunning = true;
                }
                catch (Exception)
                {
                    Console.WriteLine("An error occured while starting the server, please try again. If the error persist, contact your administrator.");
                    Console.WriteLine("Press Enter to close.");
                    Console.ReadKey(true);
                    _isRunning = false;
                }
            }

            // If the JSONDB Server is launched as a console
            if (!Console.IsInputRedirected)
            {
                Console.WriteLine("Welcome to JSONDB Server");
                Console.WriteLine("Copyright (c) 2016 Centers Technologies. All rights reserved.");
                Console.WriteLine("Server version 1.0.0");
                Console.WriteLine();
                Console.WriteLine("The server is listening for incomming queries at the address jsondb://" + _serverAddress + ":2717");
                Console.WriteLine("The web administration interface is available at the address http://" + _serverAddress + ":2717");
                Console.WriteLine();
                Console.WriteLine("Type 'help' for the list of available commands.");
                Console.WriteLine();

                // Continue to recieve commands until we get a "close" command
                while (_isRunning)
                {
                    if (_userIsConnected)
                    {
                        Console.Write("$ " + _db.GetUsername() + "@" + _serverName + " >  ");
                    }
                    else
                    {
                        Console.Write("$ jsondb >  ");
                    }

                    var command = Console.ReadLine() ?? string.Empty;
                    var cmd = command.ToLower().TrimStart();

                    if (cmd == string.Empty)
                    {
                        // Do nothing...
                    }
                    else if (cmd.StartsWith("help"))
                    {
                        Console.WriteLine("Use help [commandName] for a detailed help about a command.");
                        Console.WriteLine();
                        Console.WriteLine("mkserver           Create a new server.");
                        Console.WriteLine("connect            Connect to a server.");
                        Console.WriteLine("mkdatabase         Create a new database.");
                        Console.WriteLine("cd                 Change the current working database.");
                        Console.WriteLine("mktable            Create a new table in the current working database.");
                        Console.WriteLine("query              Execute a query.");
                        Console.WriteLine("desc server        Show the list of databases in the current working server.");
                        Console.WriteLine("desc database      Show the list of tables in the current working database.");
                        Console.WriteLine("disconnect         Disconnect from a server.");
                        Console.WriteLine("close              Disconnect and close the console.");
                        Console.WriteLine("clear              Clear the console.");
                    }
                    else if (cmd.StartsWith("mkserver"))
                    {
                        ExecMkServer(command);
                    }
                    else if (cmd.StartsWith("rmserver"))
                    {
                        ExecRmSrever(command);
                    }
                    else if (cmd.StartsWith("desc"))
                    {
                        ExecDesc(command);
                    }
                    else if (cmd.StartsWith("connect"))
                    {
                        ExecConnect(command);
                    }
                    else if (cmd.StartsWith("mkdatabase"))
                    {
                        ExecMkDatabase(command);
                    }
                    else if (cmd.StartsWith("rmdatabase"))
                    {
                        ExecRmDatabase(command);
                    }
                    else if (cmd.StartsWith("cd"))
                    {
                        ExecChangeDatabase(command);
                    }
                    else if (cmd.StartsWith("mktable"))
                    {
                        ExecMkTable(command);
                    }
                    else if (cmd.StartsWith("rmtable"))
                    {
                        ExecRmTable(command);
                    }
                    else if (cmd.StartsWith("query"))
                    {
                        ExecQuery(command);
                    }
                    else if (cmd.StartsWith("disconnect"))
                    {
                        ExecDisconnect();
                    }
                    else if (cmd.StartsWith("quit") || cmd.StartsWith("exit") || cmd.StartsWith("close") || cmd.StartsWith("shutdown"))
                    {
                        ExecClose();
                    }
                    else if (cmd.StartsWith("clear"))
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
                while (_isRunning)
                {
                    var command = Console.In.ReadLine() ?? string.Empty;
                    if (command.ToLower().StartsWith("quit") || command.ToLower().StartsWith("exit") || command.ToLower().StartsWith("close") || command.ToLower().StartsWith("shutdown"))
                    {
                        ExecClose();
                    }
                }
            }

            if (!_aServerIsRunning)
            {
                // Stop the server when a "close" command is recieved
                web?.Stop();

                // Stop to execute queries
                qThread?.Abort();
            }
        }

        private static void ExecRmTable(string command)
        {
            if (_userIsConnected)
            {
                if (_db.IsWorkingDatabase())
                {
                    var table = command.Remove(0, 7).Trim();
                    while (table == string.Empty)
                    {
                        Console.Write(" -> Table name: ");
                        table = _validateName(Console.ReadLine()?.Trim());
                    }

                    if (Util.Exists(Util.MakePath(_db.GetServer(), _db.GetDatabase(), table + ".jdbt")))
                    {
                        try
                        {
                            Console.Write("The table will be deleted. It's recommended to backup your table before continue. Are you sure to delete this table ? (Type 'yes' to confirm): ");
                            var choice = Console.ReadLine()?.ToLower().Trim();
                            if (choice == "yes" || choice == "y")
                            {
                                File.Delete(Util.MakePath(_db.GetServer(), _db.GetDatabase(), table + ".jdbt"));
                                Console.WriteLine("Table deleted.");
                            }
                            else
                            {
                                Console.WriteLine("Table deletion cancelled.");
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Unable to delete the table: " + e.Message);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unable to delete the table: The table doesn't exist in the current database.");
                    }
                }
                else
                {
                    Console.WriteLine("Unable to delete the table: No database selected.");
                }
            }
            else
            {
                Console.WriteLine("Unable to delete the table: You are not connected to a server.");
            }
        }

        private static void ExecRmDatabase(string command)
        {
            if (_userIsConnected)
            {
                var database = command.Remove(0, 10).Trim();

                while (database == string.Empty)
                {
                    Console.Write(" -> Database name: ");
                    database = _validateName(Console.ReadLine()?.Trim());
                }

                try
                {
                    if (_db.Exists(database))
                    {
                        Console.Write("The database will be deleted, note that ALL your tables will be also deleted. It's recommended to backup your database before continue. Are you sure to delete this database ? (Type 'yes' to confirm): ");
                        var choice = Console.ReadLine()?.ToLower().Trim();
                        if (choice == "yes" || choice == "y")
                        {
                            Directory.Delete(Util.MakePath(_db.GetServer(), database), true);
                            Console.WriteLine("Database deleted.");
                        }
                        else
                        {
                            Console.WriteLine("Database deletion cancelled.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("The database \"" + database + "\" doesn't exist in the current server.");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to delete the database: " + e.Message);
                }
            }
            else
            {
                Console.WriteLine("Unable to delete the database: You are not connected to a server.");
            }
        }

        private static void ExecRmSrever(string command)
        {
            var parts = command.Remove(0, 8).Trim().Split(' ');

            var username = string.Empty;
            var server = string.Empty;

            var password = new Regex("\"(.*)\"", RegexOptions.IgnoreCase).Replace(new Regex("-p \".*\"", RegexOptions.IgnoreCase).Match(command).Value.Replace("-p ", ""), "$1");

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

            while (server == string.Empty)
            {
                Console.Write(" -> Server name: ");
                server = _validateName(Console.ReadLine()?.Trim());
            }

            while (username == string.Empty)
            {
                Console.Write(" -> Username: ");
                username = _validateName(Console.ReadLine()?.Trim());
            }

            if (command.Trim().Length == 8 && password == string.Empty)
            {
                Console.Write(" -> Password: ");
                password = _password();
            }

            try
            {
                Jsondb.Connect(server, username, password);
                Console.Write("The server will be deleted, note that ALL your databases and tables will be also deleted. It's recommended to backup your server before continue. Are you sure to delete this server ? (Type 'yes' to confirm): ");
                var choice = Console.ReadLine()?.ToLower().Trim();
                if (choice == "yes" || choice == "y")
                {
                    Directory.Delete(Util.MakePath(Util.AppRoot(), "servers", server), true);
                    Configuration.RemoveServer(server);
                    Console.WriteLine("Server deleted.");
                }
                else
                {
                    Console.WriteLine("Server deletion cancelled.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to delete the server: " + e.Message);
            }
        }

        private static void ExecDesc(string command)
        {
            var descWhat = command.Remove(0, 4).Trim();
            int maxLength;

            switch (descWhat)
            {
                case "server":
                    if (_userIsConnected)
                    {
                        var databases = Database.GetDatabaseList(_serverName);
                        maxLength = databases.Select(database => database.Length).Concat(new[] {4}).Max();
                        maxLength += 2;
                        Console.Write("+");
                        for (var i = 0; i < maxLength; i++)
                        {
                            Console.Write("-");
                        }
                        Console.Write("+");
                        Console.WriteLine();
                        Console.Write("|");
                        for (var i = 0; i < (maxLength / 2) - 2; i++)
                        {
                            Console.Write(" ");
                        }
                        Console.Write("Name");
                        for (var i = (maxLength / 2) + 2; i < maxLength; i++)
                        {
                            Console.Write(" ");
                        }
                        Console.Write("|");
                        Console.WriteLine();
                        Console.Write("+");
                        for (var i = 0; i < maxLength; i++)
                        {
                            Console.Write("-");
                        }
                        Console.Write("+");
                        Console.WriteLine();
                        foreach (var database in databases)
                        {
                            Console.Write("| ");
                            Console.Write(database);
                            for (var i = 1 + database.Length; i < maxLength; i++)
                            {
                                Console.Write(" ");
                            }
                            Console.Write("|");
                            Console.WriteLine();
                        }
                        Console.Write("+");
                        for (var i = 0; i < maxLength; i++)
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
                    if (_userIsConnected)
                    {
                        if (_db.IsWorkingDatabase())
                        {
                            var tables = _db.GetTableList();
                            maxLength = tables.Select(table => table.Length).Concat(new[] {4}).Max();
                            maxLength += 2;
                            Console.Write("+");
                            for (var i = 0; i < maxLength; i++)
                            {
                                Console.Write("-");
                            }
                            Console.Write("+");
                            Console.WriteLine();
                            Console.Write("|");
                            for (var i = 0; i < (maxLength / 2) - 2; i++)
                            {
                                Console.Write(" ");
                            }
                            Console.Write("Name");
                            for (var i = (maxLength / 2) + 2; i < maxLength; i++)
                            {
                                Console.Write(" ");
                            }
                            Console.Write("|");
                            Console.WriteLine();
                            Console.Write("+");
                            for (var i = 0; i < maxLength; i++)
                            {
                                Console.Write("-");
                            }
                            Console.Write("+");
                            Console.WriteLine();
                            foreach (var table in tables)
                            {
                                Console.Write("| ");
                                Console.Write(table);
                                for (var i = 1 + table.Length; i < maxLength; i++)
                                {
                                    Console.Write(" ");
                                }
                                Console.Write("|");
                                Console.WriteLine();
                            }
                            Console.Write("+");
                            for (var i = 0; i < maxLength; i++)
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
            if (_userIsConnected)
            {
                var database = command.Remove(0, 10).Trim();

                while (database == string.Empty)
                {
                    Console.Write(" -> Database name: ");
                    database = _validateName(Console.ReadLine()?.Trim());
                }

                try
                {
                    _db.CreateDatabase(database);
                    Console.WriteLine("Database created.");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to create the database: " + e.Message);
                }
            }
            else
            {
                Console.WriteLine("Unable to create the database: You are not connected to a server.");
            }
        }

        private static void ExecQuery(string command)
        {
            if (_userIsConnected)
            {
                var query = command.Remove(0, 5).Trim();

                while (query == string.Empty)
                {
                    Console.Write(" -> ");
                    query = Console.ReadLine()?.Trim();
                }

                try
                {
                    Console.WriteLine(_db.Query(query).ToString());
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
            var parts = command.Remove(0, 8).Trim().Split(' ');

            var username = string.Empty;
            var server = string.Empty;

            var password = new Regex("\"(.*)\"", RegexOptions.IgnoreCase).Replace(new Regex("-p \".*\"", RegexOptions.IgnoreCase).Match(command).Value.Replace("-p ", ""), "$1");

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

            while (server == string.Empty)
            {
                Console.Write(" -> Server name: ");
                server = _validateName(Console.ReadLine()?.Trim());
            }

            while (username == string.Empty)
            {
                Console.Write(" -> Username: ");
                username = _validateName(Console.ReadLine()?.Trim());
            }

            if (command.Trim().Length == 8 && password == string.Empty)
            {
                Console.Write(" -> Password (Leave blank to ignore (not recommended)): ");
                password = _password();
            }

            try
            {
                Jsondb.CreateServer(server, username, password);
                Console.WriteLine("Server created.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to create the server: " + e.Message);
            }
        }

        private static void ExecChangeDatabase(string command)
        {
            if (_userIsConnected)
            {
                var database = command.Remove(0, 2).Trim().Split(' ')[0];
                if (database == string.Empty)
                {
                    Console.WriteLine("Use: cd DatabaseName");
                }
                else
                {
                    if (_db.Exists(database))
                    {
                        try
                        {
                            _db.SetDatabase(database);
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

        private static void ExecClose()
        {
            if (_userIsConnected)
            {
                _db.Disconnect();
            }
            _isRunning = false;
        }

        private static void ExecDisconnect()
        {
            _serverName = string.Empty;
            _db.Disconnect();
            _userIsConnected = false;
        }

        private static void ExecMkTable(string command)
        {
            if (_userIsConnected)
            {
                var table = command.Remove(0, 7).Trim();
                if (_db.IsWorkingDatabase())
                {
                    while (table == string.Empty)
                    {
                        Console.Write(" -> Table name: ");
                        table = _validateName(Console.ReadLine()?.Trim());
                    }
                    var prototype = new JObject();
                    var isAddindFields = true;
                    do
                    {
                        Console.Write("Do you want to add a new column? (Yes/No) ");
                        var res = Console.ReadLine()?.Trim().ToLower();
                        if (res == "yes" || res == "y")
                        {
                            Console.Write(" -> Column name: ");
                            var cName = Console.ReadLine()?.Trim();
                            Console.Write(" -> Column type: ");
                            var cType = Console.ReadLine()?.Trim();
                            Console.Write(" -> Default value (Leave blank to ignore): ");
                            var cDefv = Console.ReadLine()?.Trim();
                            Console.Write(" -> Max length (Leave blank to ignore): ");
                            var cMaxl = Console.ReadLine()?.Trim();
                            Console.Write(" -> Not null (Yes/No): ");
                            var cNull = Console.ReadLine()?.Trim().ToLower();
                            Console.Write(" -> Auto increment (Yes/No): ");
                            var cAuto = Console.ReadLine()?.Trim().ToLower();
                            Console.Write(" -> Primary key (Yes/No): ");
                            var cPkey = Console.ReadLine()?.Trim().ToLower();
                            Console.Write(" -> Unique key (Yes/No): ");
                            var cUkey = Console.ReadLine()?.Trim().ToLower();

                            var properties = new JObject {["type"] = cType ?? "string"};
                            if (cDefv != string.Empty) properties["default"] = cDefv;
                            if (cMaxl != string.Empty) properties["max_length"] = cMaxl;
                            if (cNull == "yes" || cNull == "y") properties["not_null"] = true;
                            if (cAuto == "yes" || cAuto == "y") properties["auto_increment"] = true;
                            if (cPkey == "yes" || cPkey == "y") properties["primary_key"] = true;
                            if (cUkey == "yes" || cUkey == "y") properties["unique_key"] = true;

                            prototype[cName] = properties;
                        }
                        else
                        {
                            isAddindFields = false;
                        }
                    } while (isAddindFields);
                    Console.WriteLine("The table \"" + table + "\" will be created with these columns:");
                    Console.WriteLine(prototype.ToString());
                    Console.Write("It's OK? (Yes/No) ");
                    var ok = Console.ReadLine()?.Trim().ToLower();
                    if (ok == "yes" || ok == "y")
                    {
                        try
                        {
                            _db.CreateTable(table, prototype);
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
            var cmdArgs = command.Remove(0, 7).TrimStart().Split(' ');
            var username = string.Empty;
            var server = string.Empty;
            var database = string.Empty;

            var password = new Regex("\"(.*)\"", RegexOptions.IgnoreCase).Replace(new Regex("-p \".*\"", RegexOptions.IgnoreCase).Match(command).Value.Replace("-p ", ""), "$1");

            for (int i = 0, l = cmdArgs.Length; i < l; ++i)
            {
                switch (cmdArgs[i])
                {
                    case "-u":
                        username = cmdArgs[i + 1];
                        break;
                    case "-s":
                        server = cmdArgs[i + 1];
                        break;
                    case "-d":
                        database = cmdArgs[i + 1];
                        break;
                }
            }

            if (server == string.Empty)
            {
                Console.WriteLine("Cannot connect to a server. No server provided. Use \"-s ServerName\" to give the name of the server to use with the connection.");
            }
            else
            {
                if (username == string.Empty)
                {
                    Console.WriteLine("Cannot connect to a server. No user provided. Use \"-u Username\" to give the username.");
                }
                else
                {
                    try
                    {
                        _db = Jsondb.Connect(server, username, password, database);
                        _userIsConnected = true;
                        _serverName = server;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unable to connect to the database: " + e.Message);
                    }
                }
            }
        }

        private static string _validateName(string name)
        {
            if (Regex.IsMatch(name, "^[a-zA-Z0-9_]+$")) return name;
            Console.WriteLine("Invalid server name.");
            Console.WriteLine();
            return string.Empty;
        }

        private static string _password()
        {
            ConsoleKeyInfo c;
            var password = string.Empty;

            while ((c = Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                password += c.KeyChar;
            }
            Console.WriteLine();

            return password;
        }
    }
}
