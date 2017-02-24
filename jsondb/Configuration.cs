using Newtonsoft.Json.Linq;

namespace JSONDB
{
    /// <summary>
    /// Class Configuration.
    /// </summary>
    public static class Configuration
    {
        /// <summary>
        /// Remove a server from the list of registered server.
        /// </summary>
        /// <param name="server">The name of the server</param>
        public static void RemoveServer(string server)
        {
            var config = GetConfigFile("users");
            config.Remove(server);

            _writeConfigFile("users", config);
        }

        /// <summary>
        /// Add a new user in a registered server. If the server
        /// doesn't exist, the server will be created.
        /// </summary>
        /// <param name="server">The name of the server</param>
        /// <param name="username">The username</param>
        /// <param name="password">The password</param>
        public static void AddUser(string server, string username, string password)
        {
            var config = GetConfigFile("users");
            var user = new JObject
            {
                ["username"] = Util.Crypt(username),
                ["password"] = Util.Crypt(password)
            };

            if (config[server] == null)
            {
                config[server] = new JArray();
            }

            ((JArray)config[server]).Add(user);

            _writeConfigFile("users", config);
        }

        /// <summary>
        /// Remove an user from a registered server.
        /// </summary>
        /// <param name="server">The name of the server</param>
        /// <param name="username">The username</param>
        /// <param name="password">The password</param>
        public static void RemoveUser(string server, string username, string password)
        {
            var config = GetConfigFile("users");
            var i = 0;

            if (config[server] == null)
            {
                return;
            }

            foreach (var jToken in (JArray)config[server])
            {
                var user = (JObject) jToken;
                if (user["username"].ToString() == Util.Crypt(username) && user["password"].ToString() == Util.Crypt(password))
                {
                    ((JArray)config[server]).RemoveAt(i);
                    break;
                }
                ++i;
            }

            _writeConfigFile("users", config);
        }

        /// <summary>
        /// Get a configuration file.
        /// </summary>
        /// <param name="file">The name of the configuration file</param>
        /// <returns>The array of configuration</returns>
        public static JObject GetConfigFile(string file)
        {
            if (_exists(file))
            {
                return JObject.Parse(Util.ReadTextFile(_path(file)));
            }

            var o = new JObject();
            _writeConfigFile(file, o);
            return o;
        }

        /// <summary>
        /// Write a configuration file.
        /// </summary>
        /// <param name="file">The name of the file</param>
        /// <param name="o">The configuration to write</param>
        private static void _writeConfigFile(string file, JObject o)
        {
            Util.WriteTextFile(_path(file), o.ToString());
        }

        /// <summary>
        /// Check if a configuration file exist.
        /// </summary>
        /// <param name="file">THe name of the configuration file</param>
        /// <returns>true if the file exist, false otherwise</returns>
        private static bool _exists(string file)
        {
            if (!Util.Exists(_path()))
            {
                Util.MakeDirectory(_path());
            }

            return Util.Exists(_path(file));
        }

        /// <summary>
        /// Get the path to a configuration file.
        /// </summary>
        /// <param name="file">The name of the configuration file</param>
        /// <returns>The file path</returns>
        private static string _path(string file = "")
        {
            return string.IsNullOrEmpty(file) ? Util.MakePath(Util.AppRoot(), "config") : Util.MakePath(Util.AppRoot(), "config", file + ".json");
        }
    }
}
