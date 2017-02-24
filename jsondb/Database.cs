using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace JSONDB
{
    /// <summary>
    /// Class Database
    /// </summary>
    public class Database
    {
        /// <summary>
        /// The name of the server.
        /// </summary>
        private string _serverName = string.Empty;

        /// <summary>
        /// The name of the database.
        /// </summary>
        private string _databaseName = string.Empty;

        /// <summary>
        /// The username used for connection.
        /// </summary>
        private string _username = string.Empty;

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="server">The name of the server to connect on</param>
        /// <param name="username">The username which match the server</param>
        /// <param name="password">The password which match the username</param>
        /// <param name="database">The name of the database</param>
        public Database(string server, string username, string password, string database = null)
        {
            Connect(server, username, password, database);
        }

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="server">The name of the server to connect on</param>
        /// <param name="credentials">Credentials sent by the WebSocket connection</param>
        public Database(string server, string credentials)
        {
            var userFound = false;

            if (server == string.Empty || credentials == string.Empty)
            {
                throw new Exception("Database Error: Can't connect to the server, missing parameters.");
            }

            Benchmark.Mark("Database_(connect)_start");
            var users = Configuration.GetConfigFile("users");

            if (users[server] == null)
            {
                Benchmark.Mark("Database_(connect)_end");
                throw new Exception("Database Error: There is no registered server with the name \"" + server + "\".");
            }

            foreach (var jToken in (JArray)users[server])
            {
                var user = (JObject)jToken;
                var currentCredentials = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(user["username"] + ":" + user["password"])
                );

                userFound = currentCredentials == credentials;
            }

            if (!userFound)
            {
                Benchmark.Mark("Database_(connect)_end");
                throw new Exception("Database Error: User's authentication failed. Access denied.");
            }

            _serverName = Util.MakePath(Util.AppRoot(), "servers", server);

            Benchmark.Mark("Database_(connect)_end");
        }

        /// <summary>
        /// Connect to a database
        /// </summary>
        /// <param name="server">The name of the server to connect on</param>
        /// <param name="username">The username which match the server</param>
        /// <param name="password">The password which match the username</param>
        /// <param name="database">The name of the database</param>
        /// <exception cref="Exception"></exception>
        private void Connect(string server, string username, string password, string database)
        {
            var userFound = false;

            if (string.IsNullOrWhiteSpace(server))
            {
                throw new ArgumentNullException("server", "Database Error: Can't connect to the server, the server name is null or empty.");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentNullException("username", "Database Error: Can't connect to the server, the user name is null or empty.");
            }

            Benchmark.Mark("Database_(connect)_start");
            var users = Configuration.GetConfigFile("users");

            if (users[server] == null)
            {
                Benchmark.Mark("Database_(connect)_end");
                throw new Exception("Database Error: There is no registered server with the name \"" + server + "\".");
            }

            foreach (var jToken in (JArray)users[server])
            {
                JObject user = (JObject)jToken;
                userFound = user["username"].ToString() == Util.Crypt(username) && user["password"].ToString() == Util.Crypt(password);

                if (userFound)
                    break;
            }

            if (!userFound)
            {
                Benchmark.Mark("Database_(connect)_end");
                throw new Exception("Database Error: User's authentication failed for user \"" + username + "\" on server \"" + server + "\" (Using password: " + (password.Length > 0 ? "Yes" : "No") + "). Access denied.");
            }

            _serverName = Util.MakePath(Util.AppRoot(), "servers", server);
            _username = username;

            if (!string.IsNullOrWhiteSpace(database))
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
            _serverName = string.Empty;
            _databaseName = string.Empty;
            _username = string.Empty;
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
                throw new InvalidOperationException("Database Error: Can't use the database \"" + database + "\", there is no connection established with a server.");
            }

            if (!Exists(database))
            {
                throw new Exception("Database Error: Can't use the database \"" + database + "\", the database doesn't exist in the server.");
            }

            _databaseName = database;

            return this;
        }

        /// <summary>
        /// Get tne path to the current working server.
        /// </summary>
        /// <returns>The path to the server</returns>
        public string GetServer()
        {
            return _serverName;
        }

        /// <summary>
        /// Get the name of the current working database.
        /// </summary>
        /// <returns>The name of the database</returns>
        public string GetDatabase()
        {
            return _databaseName;
        }

        /// <summary>
        /// Get the username of the current connected client.
        /// </summary>
        /// <returns>The username of the client</returns>
        public string GetUsername()
        {
            return _username;
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
            return string.IsNullOrWhiteSpace(_databaseName) ? new string[0] : Util.GetFilesList(Util.MakePath(_serverName, _databaseName));
        }

        /// <summary>
        /// Check if the user is connected.
        /// </summary>
        /// <returns>true id the user is connected, false otherwise</returns>
        public bool IsConnected()
        {
            return !string.IsNullOrWhiteSpace(_serverName);
        }

        /// <summary>
        /// Check if a database is set.
        /// </summary>
        /// <returns>true if a database is set and false otherwise</returns>
        public bool IsWorkingDatabase()
        {
            return !string.IsNullOrWhiteSpace(_databaseName);
        }

        /// <summary>
        /// Check if a database exist in the current working server.
        /// </summary>
        /// <param name="name">The name of the database</param>
        /// <returns>true if the database exist and false otherwise</returns>
        public bool Exists(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && Util.Exists(Util.MakePath(_serverName, name));
        }

        /// <summary>
        /// Create a new database in the current working server.
        /// </summary>
        /// <param name="name">The name of the database</param>
        /// <returns>The current Database instance</returns>
        public Database CreateDatabase(string name)
        {
            Benchmark.Mark("Database_(createDatabase)_start");
            if (string.IsNullOrWhiteSpace(name))
            {
                Benchmark.Mark("Database_(createDatabase)_end");
                throw new Exception("Database Error: Can't create the database, the database's name is missing.");
            }
            if (!IsConnected())
            {
                Benchmark.Mark("Database_(createDatabase)_end");
                throw new Exception("Database Error: Can't create the database \"" + name + "\", there is no connection established with a server.");
            }

            var path = Util.MakePath(_serverName, name);

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
            if (string.IsNullOrWhiteSpace(name))
            {
                Benchmark.Mark("Database_(createTable)_end");
                throw new Exception("Database Error: Can\'t create table, without a name.");
            }

            if (!IsWorkingDatabase())
            {
                Benchmark.Mark("Database_(createTable)_end");
                throw new Exception("Database Error: Trying to create a table without using a database.");
            }

            var path = Util.MakePath(_serverName, _databaseName, name + ".jdbt");

            if (Util.Exists(path))
            {
                Benchmark.Mark("Database_(createTable)_end");
                throw new Exception("Database Error: Can't create the table \"" + name + "\" in the database \"" + _databaseName + "\". The table already exist.");
            }

            var fields = new JArray();
            var properties = new JObject
            {
                ["last_insert_id"] = 0,
                ["last_valid_row_id"] = 0,
                ["last_link_id"] = 0,
                ["primary_keys"] = new JArray(),
                ["unique_keys"] = new JArray()
            };
            var aiExist = false;

            foreach (var item in prototype)
            {
                var field = item.Key;
                var prop = (JObject)item.Value;
                var hasAi = prop["auto_increment"] != null;
                var hasPk = prop["primary_key"] != null;
                var hasUk = prop["unique_key"] != null;
                var hasTp = prop["type"] != null;

                if (aiExist && hasAi)
                {
                    Benchmark.Mark("Database_(createTable)_end");
                    throw new Exception("Database Error: Can't use the \"auto_increment\" property on more than one field.");
                }

                if (!aiExist && hasAi)
                {
                    aiExist = true;
                    prototype[field]["unique_key"] = true;
                    prototype[field]["not_null"] = true;
                    prototype[field]["type"] = "int";
                    hasTp = true;
                    hasUk = true;
                }

                if (hasPk)
                {
                    prototype[field]["not_null"] = true;
                    ((JArray)properties["primary_keys"]).Add(field);
                }

                if (hasUk)
                {
                    prototype[field]["not_null"] = true;
                    ((JArray)properties["unique_keys"]).Add(field);
                }

                if (hasTp)
                {
                    var jToken = prop["type"];

                    if (jToken != null)
                    {
                        if (Regex.IsMatch(jToken.ToString(), "link\\(.+\\)"))
                        {
                            var link = Regex.Replace(jToken.ToString(), "link\\((.+)\\)", "$1");
                            var linkInfo = link.Split('.');
                            var linkTablePath = Util.MakePath(_serverName, _databaseName, linkInfo[0] + ".jdbt");

                            if (!Util.Exists(linkTablePath))
                            {
                                throw new Exception("Database Error: Can't create the table \"" + name +
                                                    "\". An error occur when linking the column \"" + field +
                                                    "\" with the column \"" + linkInfo[1] + "\", the table \"" + linkInfo[0] +
                                                    "\" doesn't exist in the database \"" + _databaseName + "\".");
                            }

                            var linkTableData = GetTableData(linkTablePath);
                            if (Array.IndexOf(linkTableData["prototype"].ToArray(), linkInfo[1]) == -1)
                            {
                                throw new Exception("Database Error: Can't create the table \"" + name +
                                                    "\". An error occur when linking the column \"" + field +
                                                    "\" with the column \"" + linkInfo[1] + "\", the column \"" +
                                                    linkInfo[1] + "\" doesn't exist in the table \"" + linkInfo[0] +
                                                    "\" .");
                            }
                            if ((linkTableData["properties"]["primary_keys"] != null &&
                                 Array.IndexOf(linkTableData["properties"]["primary_keys"].ToArray(), linkInfo[1]) == -1)
                                ||
                                (linkTableData["properties"]["unique_keys"] != null &&
                                 Array.IndexOf(linkTableData["properties"]["unique_keys"].ToArray(), linkInfo[1]) == -1))
                            {
                                throw new Exception("Database Error: Can't create the table \"" + name +
                                                    "\". An error occur when linking the column \"" + field +
                                                    "\" with the column \"" + linkInfo[1] + "\", the column \"" +
                                                    linkInfo[1] +
                                                    "\" is not a PRIMARY KEY or an UNIQUE KEY in the table \"" +
                                                    linkInfo[0] + "\" .");
                            }

                            ((JObject)prototype[field]).Remove("default");
                            ((JObject)prototype[field]).Remove("max_length");
                        }
                        else
                        {
                            switch (jToken.ToString())
                            {
                                case "bool":
                                case "boolean":
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
                                    break;
                                case "int":
                                case "integer":
                                case "number":
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
                                    break;
                                case "float":
                                case "decimal":
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
                                    break;
                                case "string":
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
                                    break;
                                default:
                                    throw new NotSupportedException("Database Error: The type \"" + jToken + "\" isn't supported by JSONDB.");
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

            var data = new JObject
            {
                ["prototype"] = fields,
                ["properties"] = properties,
                ["data"] = new JObject()
            };

            Util.WriteTextFile(path, data.ToString());

            Benchmark.Mark("Database_(createTable)_end");

            return this;
        }

        /// <summary>
        /// Gets the content of a table at the given path.
        /// </summary>
        /// <param name="tablePath">The path to the table</param>
        /// <returns>The content of the table</returns>
        internal static JObject GetTableData(string tablePath)
        {
            return JObject.Parse(Util.ReadTextFile(tablePath));
        }

        /// <summary>
        /// Write data in a table file.
        /// </summary>
        /// <param name="tablePath">The path to the tabe</param>
        /// <param name="data">The data to write</param>
        internal static void WriteTableData(string tablePath, JObject data)
        {
            Util.WriteTextFile(tablePath, data.ToString());
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
