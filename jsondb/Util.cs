using System;
using System.Threading.Tasks;
using EdgeJs;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace JSONDB
{
    public class Util
    {
        public static async void TestServerConnection(string adress, Func<object, Task<object>> callback)
        {
            var TestServer = Edge.Func(@"
                return function (data, cb) {
                    var WebSocketServer = require('ws').Server;
                    var wss = new WebSocketServer({server: app});
                    cb();
                };
            ");

            await TestServer(new {
                Callback = callback
            });
        }

        public static bool ValidateAddress(string adress)
        {
            var parts = adress.Split('.');

            if (parts.Length == 4)
            {
                foreach (var num in parts)
                {
                    if (int.Parse(num) > 255 || int.Parse(num) < 0)
                    {
                        return false;
                    }
                }
                return true;
            }

            return false;
        }

        public static async void CreateServer(string serverName, string username, string password)
        {
            var Create = Edge.Func(@"
                var JSONDB = require('jdb-jsondb');
                var jdb = new JSONDB();

                return function (opt, cb) {
                    jdb.createServer(opt.server, opt.username, opt.password);
                    cb();
                };
            ");

            await Create(new
            {
                server = serverName,
                username = username,
                password = password
            });
        }

        public static bool ServerExists(string serverName)
        {
            if (serverName == null)
            {
                return false;
            }

            return Directory.Exists(".\\servers\\" + serverName);
        }

        /// <summary>
        /// Get the absolute path in which JSONDB is installed
        /// </summary>
        /// <returns>The absolute path of the folder which contains JSONDB</returns>
        public static string AppRoot()
        {
            return Directory.GetParent(".\\").FullName.TrimEnd('\\');
        }

        /// <summary>
        /// Crypt a string with the default JSONDB salt
        /// </summary>
        /// <param name="value">The string to be crypted</param>
        /// <returns>The crypted value of the string</returns>
        public static string Crypt(string value)
        {
            return SHA1(value + "<~>:q;axMw|S01%@yu*lfr^Q#j)OG<Z_dQOvzuTZsa^sm0K}*u9{d3A[ekV;/x[c");
        }

        /// <summary>
        /// Prepend a leading zero on a number lesser than 10
        /// </summary>
        /// <param name="number">The number</param>
        /// <returns>The given number with a leading zero if necessary</returns>
        public static string Zeropad(int number)
        {
            if (number < 10)
            {
                return "0" + number;
            }

            return number.ToString();
        }

        /// <summary>
        /// Compute sha1 hash for string encoded as UTF8
        /// </summary>
        /// <param name="s">String to be hashed</param>
        /// <returns>40-character hex string</returns>
        public static string SHA1(string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);

            var sha1 = System.Security.Cryptography.SHA1.Create();
            byte[] hashBytes = sha1.ComputeHash(bytes);

            return HexStringFromBytes(hashBytes);
        }

        /// <summary>
        /// Compute md5 hash for string encoded as UTF8
        /// </summary>
        /// <param name="s">String to be hashed</param>
        /// <returns></returns>
        public static string MD5(string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);

            var sha1 = System.Security.Cryptography.MD5.Create();
            byte[] hashBytes = sha1.ComputeHash(bytes);

            return HexStringFromBytes(hashBytes);
        }

        /// <summary>
        /// Convert an array of bytes to a string of hex digits
        /// </summary>
        /// <param name="bytes">array of bytes</param>
        /// <returns>String of hex digits</returns>
        private static string HexStringFromBytes(byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                var hex = b.ToString("x2");
                sb.Append(hex);
            }
            return sb.ToString();
        }
    }
}
