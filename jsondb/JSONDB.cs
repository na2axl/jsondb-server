﻿using System;

namespace JSONDB.Library
{
    public static class JSONDB
    {
        /// <summary>
        /// Create a new server.
        /// </summary>
        /// <param name="name">The name of the server</param>
        /// <param name="username">The username</param>
        /// <param name="password">The password</param>
        public static void CreateServer(string name, string username, string password)
        {
            Benchmark.Mark("JSONDB_(CreateServer)_start");
            string path = Util.MakePath(Util.AppRoot(), "servers", name);

            if (username == String.Empty)
            {
                Benchmark.Mark("JSONDB_(CreateServer)_end");
                throw new Exception("JSONDB Error: Can't create the server. An username is required.");
            }

            if (Util.Exists(path))
            {
                Benchmark.Mark("JSONDB_(CreateServer)_end");
                throw new Exception("JSONDB Error: Can't create the server. The server already exists.");
            }

            Util.MakeDirectory(path);

            if (!Util.Exists(path))
            {
                Benchmark.Mark("JSONDB_(CreateServer)_end");
                throw new Exception("JSONDB Error: Can't create the server at \"" + path + "\". Maybe you don't have write access.");
            }

            Configuration.AddUser(name, username, password);
            Benchmark.Mark("JSONDB_(CreateServer)_end");
        }

        /// <summary>
        /// Check if a server exists.
        /// </summary>
        /// <param name="name">The name of the server</param>
        /// <returns>true if the server exist and false otherwise</returns>
        public static bool ServerExists(string name)
        {
            if (name == String.Empty)
            {
                return false;
            }

            return Util.Exists(Util.MakePath(Util.AppRoot(), "servers", name));
        }

        /// <summary>
        /// Get the list of servers.
        /// </summary>
        /// <returns>THe list of servers</returns>
        public static string[] ServersList()
        {
            return Util.GetDirectoriesList(Util.MakePath(Util.AppRoot(), "servers"));
        }

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="server">The name of the server to connect on</param>
        /// <param name="username">The username which match the server</param>
        /// <param name="password">The password which match the username</param>
        /// <param name="database">The name of the database</param>
        /// <returns>The Database connection instance</returns>
        public static Database Connect(string server, string username, string password, string database)
        {
            return new Database(server, username, password, database);
        }

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="server">The name of the server to connect on</param>
        /// <param name="username">The username which match the server</param>
        /// <param name="password">The password which match the username</param>
        /// <returns>The Database connection instance</returns>
        public static Database Connect(string server, string username, string password)
        {
            return new Database(server, username, password);
        }

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="server">The name of the server to connect on</param>
        /// <param name="credentials">Credentials sent by the websocket connection</param>
        /// <returns></returns>
        public static Database Connect(string server, string credentials)
        {
            return new Database(server, credentials);
        }
    }
}
