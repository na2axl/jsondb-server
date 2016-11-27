using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;

namespace JSONDB.Library
{
    public static class Configuration
    {
        public static void RemoveServer(string server)
        {
            JObject Config = GetConfigFile("users");
            Config.Remove(server);

            _writeConfigFile("users", Config);
        }

        public static void AddUser(string server, string username, string password)
        {
            JObject Config = GetConfigFile("users");
            JObject user = new JObject();

            user["username"] = Util.Crypt(username);
            user["password"] = Util.Crypt(password);

            if (Config[server] == null)
            {
                Config[server] = new JArray();
            }

            ((JArray)Config[server]).Add(user);

            _writeConfigFile("users", Config);
        }

        public static void RemoveUser(string server, string username, string password)
        {
            JObject Config = GetConfigFile("users");
            int i = 0;

            if (Config[server] == null)
            {
                return;
            }

            foreach (JObject user in (JArray)Config[server])
            {
                if (user["username"].ToString() == Util.Crypt(username) && user["password"].ToString() == Util.Crypt(password))
                {
                    ((JArray)Config[server]).RemoveAt(i);
                    break;
                }
                ++i;
            }

            _writeConfigFile("users", Config);
        }

        public static JObject GetConfigFile(string file)
        {
            if (_exists(file))
            {
                return JObject.Parse(File.ReadAllText(_path(file)));
            }
            else
            {
                JObject o = new JObject();
                _writeConfigFile(file, o);
                return o;
            }
        }

        private static void _writeConfigFile(string file, JObject o)
        {
            if (!_exists(file))
            {
                File.CreateText(_path(file)).Close();
            }
            File.WriteAllText(_path(file), o.ToString(), Encoding.UTF8);
        }

        private static bool _exists(string file)
        {
            return File.Exists(_path(file));
        }

        private static string _path(string file)
        {
            if (file == String.Empty)
            {
                return Util.AppRoot() + "\\config\\";
            }

            return Util.AppRoot() + "\\config\\" + file + ".json";
        }
    }
}
