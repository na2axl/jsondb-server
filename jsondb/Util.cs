using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace JSONDB
{
    public class Util
    {
        /// <summary>
        /// Validate an IP address.
        /// </summary>
        /// <param name="adress">The IP address</param>
        /// <returns>true if is a valid address</returns>
        public static bool ValidateAddress(string adress)
        {
            IPAddress addr;
            return IPAddress.TryParse(address, out addr);
        }

        /// <summary>
        /// Get the absolute path in which JSONDB is installed.
        /// </summary>
        /// <returns>The absolute path of the folder which contains JSONDB</returns>
        public static string AppRoot()
        {
            return Directory.GetParent(".\\").FullName.TrimEnd('\\');
        }

        /// <summary>
        /// Make a path by joining all the parts with the character '\'.
        /// </summary>
        /// <param name="parts">The parts of the path</param>
        /// <returns>The path.</returns>
        public static string MakePath(params string[] parts)
        {
            string path = String.Empty;

            foreach (var part in parts)
            {
                path += part.TrimEnd('\\') + "\\";
            }

            return path.TrimEnd('\\');
        }

        /// <summary>
        /// Create all directories in a path.
        /// </summary>
        /// <param name="path">The path to create</param>
        public static void MakeDirectory(string path)
        {
            if (!Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Check if a file or a directory exist at the given path.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>true if exist and false otherwise</returns>
        public static bool Exists(string path)
        {
            return Directory.Exists(path) || File.Exists(path);
        }

        /// <summary>
        /// Crypt a string with the default JSONDB salt.
        /// </summary>
        /// <param name="value">The string to be crypted</param>
        /// <returns>The crypted value of the string</returns>
        public static string Crypt(string value)
        {
            return SHA1(value + "<~>:q;axMw|S01%@yu*lfr^Q#j)OG<Z_dQOvzuTZsa^sm0K}*u9{d3A[ekV;/x[c");
        }

        /// <summary>
        /// Prepend a leading zero on a number lesser than 10.
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

            var md5 = System.Security.Cryptography.MD5.Create();
            byte[] hashBytes = md5.ComputeHash(bytes);

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

        /// <summary>
        /// Write a text file.
        /// </summary>
        /// <param name="path">The path of the file</param>
        /// <param name="contents">The file contents</param>
        public static void WriteTextFile(string path, string contents)
        {
            if (!Exists(path))
            {
                File.CreateText(path).Close();
            }

            File.WriteAllText(path, contents);
        }

        /// <summary>
        /// Read a text file.
        /// </summary>
        /// <param name="path">The path of the file</param>
        /// <returns>The file contents</returns>
        public static string ReadTextFile(string path)
        {
            if (!Exists(path))
            {
                return null;
            }
            return File.ReadAllText(path);
        }
    }
}
