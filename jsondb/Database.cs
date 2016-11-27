using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace JSONDB.Library
{
    public class Database
    {
        private string ServerName { get; set; } = String.Empty;
        private string DatabaseName { get; set; } = String.Empty;
        private string Username { get; set; } = String.Empty;
        private string Password { get; set; } = String.Empty;

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="server">The name of the server to connect on</param>
        /// <param name="username">The username which match the server</param>
        /// <param name="password">The password which match the username</param>
        /// <param name="database">The name of the database</param>
        public Database(string server, string username, string password, string database)
        {
            Connect(server, username, password, database);
        }

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="server">The name of the server to connect on</param>
        /// <param name="username">The username which match the server</param>
        /// <param name="password">The password which match the username</param>
        public Database(string server, string username, string password)
        {
            Connect(server, username, password, String.Empty);
        }

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="server">The name of the server to connect on</param>
        /// <param name="credentials">Credentials sent by the WebSocket connection</param>
        public Database(string server, string credentials)
        {
            bool userFound = false;

            if (server == String.Empty || credentials == String.Empty)
            {
                throw new Exception("Database Error: Can't connect to the server, missing parameters.");
            }

            Benchmark.Mark("Database_(connect)_start");
            var Users = Configuration.GetConfigFile("users");

            if (Users[server] == null)
            {
                Benchmark.Mark("Database_(connect)_end");
                throw new Exception("Database Error: There is no registered server with the name \"" + server + "\".");
            }

            foreach (JObject user in (JArray)Users[server])
            {
                var CurrentCredentials = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(user["username"].ToString() + ":" + user["password"].ToString())
                );

                if (CurrentCredentials == credentials)
                {
                    userFound = true;
                }
            }

            if (!userFound)
            {
                Benchmark.Mark("Database_(connect)_end");
                throw new Exception("Database Error: User's authentication failed. Access denied.");
            }

            ServerName = Util.MakePath(Util.AppRoot(), "servers", server);

            Benchmark.Mark("Database_(connect)_end");
        }

        private void Connect(string server, string username, string password, string database)
        {
            bool userFound = false;
    
            if (server == String.Empty || username == String.Empty)
            {
                throw new Exception("Database Error: Can't connect to the server, missing parameters.");
            }

            Benchmark.Mark("Database_(connect)_start");
            var Users = Configuration.GetConfigFile("users");

            if (Users[server] == null)
            {
                Benchmark.Mark("Database_(connect)_end");
                throw new Exception("Database Error: There is no registered server with the name \"" + server + "\".");
            }

            foreach (JObject user in (JArray)Users[server])
            {
                if (user["username"].ToString() == Util.Crypt(username) && user["password"].ToString() == Util.Crypt(password))
                {
                    userFound = true;
                }
            }

            if (!userFound)
            {
                Benchmark.Mark("Database_(connect)_end");
                throw new Exception("Database Error: User's authentication failed for user \"" + username + "\" on server \"" + server + "\" (Using password: " + (password.Length > 0 ? "Yes" : "No") + "). Access denied.");
            }

            ServerName = Util.MakePath(Util.AppRoot(), "servers", server);
            Username = username;
            Password = password;

            if (database != String.Empty)
            {
                try
                {
                    SetDatabase(database);
                }
                catch (Exception)
                {
                    Benchmark.Mark("Database_(connect)_end");
                    throw;
                }
            }

            Benchmark.Mark("Database_(connect)_end");
        }

        /// <summary>
        /// Disconnect from the server.
        /// </summary>
        public void Disconnect()
        {
            Benchmark.Mark("Database_(disconnect)_start");
            ServerName = String.Empty;
            DatabaseName = String.Empty;
            Username = String.Empty;
            Password = String.Empty;
            Benchmark.Mark("Database_(disconnect)_end");
        }

        /// <summary>
        /// Change the current working database.
        /// </summary>
        /// <param name="database">The name of the new database to use</param>
        /// <returns>The current Database instance</returns>
        public Database SetDatabase(string database)
        {
            if (!IsConnected())
            {
                throw new Exception("Database Error: Can't use the database \"" + database + "\", there is no connection established with a server.");
            }

            if (!Exists(database))
            {
                throw new Exception("Database Error: Can't use the database \"" + database + "\", the database doesn't exist in the server.");
            }

            DatabaseName = database;

            return this;
        }

        /// <summary>
        /// Get tne path of the current working server.
        /// </summary>
        /// <returns>The path of the server</returns>
        public string GetServer()
        {
            return ServerName;
        }

        /// <summary>
        /// Get the name of the current working database.
        /// </summary>
        /// <returns>The name of the database</returns>
        public string GetDatabase()
        {
            return DatabaseName;
        }

        /// <summary>
        /// Get the username of the current connected client.
        /// </summary>
        /// <returns></returns>
        public string GetUsername()
        {
            return Username;
        }

        /// <summary>
        /// Get the list of databases in a server.
        /// </summary>
        /// <param name="server">The name of the server</param>
        /// <returns>The list of databases</returns>
        public static string[] GetDatabaseList(string server)
        {
            return Util.GetDirectoriesList(Util.MakePath(Util.AppRoot(), "servers", server));
        }

        /// <summary>
        /// Get the list of tables in a database.
        /// </summary>
        /// <returns>The list of tables</returns>
        public string[] GetTableList()
        {
            if (DatabaseName == String.Empty)
            {
                return new string[0];
            }
            return Util.GetFilesList(Util.MakePath(ServerName, DatabaseName));
        }

        /// <summary>
        /// Check if the user is connected.
        /// </summary>
        /// <returns>true id the user is connected, false otherwise</returns>
        public bool IsConnected()
        {
            return ServerName != String.Empty;
        }

        /// <summary>
        /// Check if a database is set.
        /// </summary>
        /// <returns>true if a database is set and false otherwise</returns>
        public bool IsWorkingDatabase()
        {
            return DatabaseName != String.Empty;
        }

        /// <summary>
        /// Check if a database exist in the current working server.
        /// </summary>
        /// <param name="name">The name of the database</param>
        /// <returns>true if the database exist and false otherwise</returns>
        public bool Exists(string name)
        {
            if (name == null)
            {
                return false;
            }

            return Util.Exists(Util.MakePath(ServerName, name));
        }

        /// <summary>
        /// Create a new database in the current working server.
        /// </summary>
        /// <param name="name">The name of the database</param>
        /// <returns>The current Database instance</returns>
        public Database CreateDatabase(string name)
        {
            Benchmark.Mark("Database_(createDatabase)_start");
            if (name == null)
            {
                Benchmark.Mark("Database_(createDatabase)_end");
                throw new Exception("Database Error: Can't create the database, the database's name is missing.");
            }
            if (ServerName == String.Empty)
            {
                Benchmark.Mark("Database_(createDatabase)_end");
                throw new Exception("Database Error: Can't create the database \"" + name + "\", there is no connection established with a server.");
            }

            string path = Util.MakePath(ServerName, name);

            if (Exists(name))
            {
                Benchmark.Mark("Database_(createDatabase)_end");
                throw new Exception("Database Error: Can't create the database \"" + name + "\" in the server, the database already exist.");
            }

            Util.MakeDirectory(path);

            if (!Util.Exists(path))
            {
                Benchmark.Mark("Database_(createDatabase)_end");
                throw new Exception("Database Error: Can't create the database \"" + name + "\" in the server.");
            }
            Benchmark.Mark("Database_(createDatabase)_end");

            return this;
        }

        /// <summary>
        /// Create a new table in the current working database.
        /// </summary>
        /// <param name="name">The name of the table</param>
        /// <param name="prototype">The prtotype of the table</param>
        /// <returns>The current Database instance</returns>
        public Database CreateTable(string name, JObject prototype)
        {
            Benchmark.Mark("Database_(createTable)_start");
            if (name == null)
            {
                Benchmark.Mark("Database_(createTable)_end");
                throw new Exception("Database Error: Can\'t create table, without a name.");
            }

            if (DatabaseName == String.Empty)
            {
                Benchmark.Mark("Database_(createTable)_end");
                throw new Exception("Database Error: Trying to create a table without using a database.");
            }

            string path = Util.MakePath(ServerName, DatabaseName, name + ".jdbt");

            if (Util.Exists(path))
            {
                Benchmark.Mark("Database_(createTable)_end");
                throw new Exception("Database Error: Can't create the table \"" + name + "\" in the database \"" + DatabaseName + "\". The table already exist.");
            }

            JArray fields = new JArray();
            JObject properties = new JObject();
            properties["last_insert_id"] = 0;
            properties["last_valid_row_id"] = 0;
            properties["last_link_id"] = 0;
            properties["primary_keys"] = new JArray();
            properties["unique_keys"] = new JArray();
            bool ai_exist = false;

            var prototypeIterator = prototype.GetEnumerator();

            while (prototypeIterator.MoveNext())
            {
                string field = prototypeIterator.Current.Key;
                JObject prop = (JObject)prototypeIterator.Current.Value;
                bool has_ai = prop["auto_increment"] != null;
                bool has_pk = prop["primary_key"] != null;
                bool has_uk = prop["unique_key"] != null;
                bool has_tp = prop["type"] != null;

                if (ai_exist && has_ai)
                {
                    Benchmark.Mark("Database_(createTable)_end");
                    throw new Exception("Database Error: Can't use the \"auto_increment\" property on more than one field.");
                }
                else if (!ai_exist && has_ai)
                {
                    ai_exist = true;
                    prototype[field]["unique_key"] = true;
                    prototype[field]["not_null"] = true;
                    prototype[field]["type"] = "int";
                    has_tp = true;
                    has_uk = true;
                }

                if (has_pk)
                {
                    prototype[field]["not_null"] = true;
                    ((JArray)properties["primary_keys"]).Add(field);
                }

                if (has_uk)
                {
                    prototype[field]["not_null"] = true;
                    ((JArray)properties["unique_keys"]).Add(field);
                }

                if (has_tp)
                {
                    if (new Regex("link\\(.+\\)").IsMatch(prop["type"].ToString()))
                    {
                        string link = new Regex("link\\((.+)\\)").Replace(prop["type"].ToString(), "$1");
                        var link_info = link.Split('.');
                        var link_table_path = Util.MakePath(ServerName, DatabaseName, link_info[0] + ".jdbt");

                        if (!Util.Exists(link_table_path))
                        {
                            throw new Exception("Database Error: Can't create the table \"" + name + "\". An error occur when linking the column \"" + field + "\" with the column \"" + link[1] + "\", the table \"" + link_info[0] + "\" doesn't exist in the database \"" + DatabaseName + "\".");
                        }

                        JObject link_table_data = GetTableData(link_table_path);
                        if (Array.IndexOf(link_table_data["prototype"].ToArray(), link_info[1]) == -1)
                        {
                            throw new Exception("Database Error: Can't create the table \"" + name + "\". An error occur when linking the column \"" + field + "\" with the column \"" + link_info[1] + "\", the column \"" + link_info[1] + "\" doesn't exist in the table \"" + link_info[0] + "\" .");
                        }
                        if ((link_table_data["properties"]["primary_keys"] != null && Array.IndexOf(link_table_data["properties"]["primary_keys"].ToArray(), link_info[1]) == -1)
                            || (link_table_data["properties"]["unique_keys"] != null && Array.IndexOf(link_table_data["properties"]["unique_keys"].ToArray(), link_info[1]) == -1))
                        {
                            throw new Exception("Database Error: Can't create the table \"" + name + "\". An error occur when linking the column \"" + field + "\" with the column \"" + link[1] + "\", the column \"" + link_info[1] + "\" is not a PRIMARY KEY or an UNIQUE KEY in the table \"" + link_info[0] + "\" .");
                        }

                        ((JObject)prototype[field]).Remove("default");
                        ((JObject)prototype[field]).Remove("max_length");
                    }
                    else if (prop["type"].ToString() == "bool" || prop["type"].ToString() == "boolean")
                    {
                        if (prototype[field]["default"] != null)
                        {
                            bool res;
                            if (bool.TryParse(prototype[field]["default"].ToString(), out res))
                            {
                                prototype[field]["default"] = res;
                            }
                            else
                            {
                                prototype[field]["default"] = false;
                            }
                        }
                        ((JObject)prototype[field]).Remove("max_length");
                    }
                    else if (prop["type"].ToString() == "int" || prop["type"].ToString() == "integer" || prop["type"].ToString() == "number")
                    {
                        if (prototype[field]["default"] != null)
                        {
                            int res;
                            if (int.TryParse(prototype[field]["default"].ToString(), out res))
                            {
                                prototype[field]["default"] = res;
                            }
                            else
                            {
                                prototype[field]["default"] = 0;
                            }
                        }
                        ((JObject)prototype[field]).Remove("max_length");
                    }
                    else if (prop["type"].ToString() == "float" || prop["type"].ToString() == "decimal")
                    {
                        if (prototype[field]["default"] != null)
                        {
                            float res;
                            if (float.TryParse(prototype[field]["default"].ToString(), out res))
                            {
                                prototype[field]["default"] = res;
                            }
                            else
                            {
                                prototype[field]["default"] = 0f;
                            }
                        }
                        if (prototype[field]["max_length"] != null)
                        {
                            int res;
                            if (int.TryParse(prototype[field]["max_length"].ToString(), out res))
                            {
                                prototype[field]["max_length"] = res;
                            }
                            else
                            {
                                prototype[field]["max_length"] = 0;
                            }
                        }
                    }
                    else if (prop["type"].ToString() == "string")
                    {
                        if (prototype[field]["default"] != null)
                        {
                            prototype[field]["default"] = prototype[field]["default"].ToString();
                        }
                        if (prototype[field]["max_length"] != null)
                        {
                            int res;
                            if (int.TryParse(prototype[field]["max_length"].ToString(), out res))
                            {
                                prototype[field]["max_length"] = res;
                            }
                            else
                            {
                                prototype[field]["max_length"] = 0;
                            }
                        }
                    }
                }
                else
                {
                    prototype[field]["type"] = "string";
                }

                fields.Add(field);
            }

            properties.Merge(prototype);
            fields.AddFirst("#rowid");

            JObject data = new JObject();
            data["prototype"]  = fields;
            data["properties"] = properties;
            data["data"] = new JObject();

            Util.WriteTextFile(path, data.ToString());

            Benchmark.Mark("Database_(createTable)_end");

            return this;
        }

        /// <summary>
        /// Gets the content of a table at the given path.
        /// </summary>
        /// <param name="table_path">The path to the table</param>
        /// <returns>The content of the table</returns>
        internal static JObject GetTableData(string table_path)
        {
            return JObject.Parse(Util.ReadTextFile(table_path));
        }

        /// <summary>
        /// Write data in a table file.
        /// </summary>
        /// <param name="table_path">The path to the tabe</param>
        /// <param name="data">The data to write</param>
        internal static void WriteTableData(string table_path, JObject data)
        {
            Util.WriteTextFile(table_path, data.ToString());
        }

        /// <summary>
        /// Send a query to the database.
        /// </summary>
        /// <param name="query">The query</param>
        /// <returns>The query result</returns>
        public JObject Query(string query)
        {
            return new Query(this).Send(query);
        }

        /// <summary>
        /// Send multiple queries at once.
        /// </summary>
        /// <param name="queries">The queries</param>
        /// <returns>An array of queries' results</returns>
        public JObject[] MultiQuery(string queries)
        {
            return new Query(this).MultiSend(queries);
        }
    }
}
