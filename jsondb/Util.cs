using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;
using WebSocketSharp.Server;

namespace JSONDB
{
    public class Util
    {
        /// <summary>
        /// Validate an IP address.
        /// </summary>
        /// <param name="adress">The IP address</param>
        /// <returns>true if is a valid address</returns>
        public static bool ValidateAddress(string address)
        {
            IPAddress addr;
            return IPAddress.TryParse(address, out addr);
        }

        /// <summary>
        /// Test if the server can be created on the given IP address.
        /// </summary>
        /// <param name="address">The IP adress</param>
        /// <returns>true if the server can use the IP address, false otherwise</returns>
        public static bool TestServerAddress(string address)
        {
            if (ValidateAddress(address))
            {
                IPAddress addr = IPAddress.Parse(address);
                try
                {
                    var testServer = new WebSocketServer(addr, 2717);
                    testServer.Start();

                    var start = DateTime.Now.Second;
                    bool elapsed = false;
                    bool failure = false;

                    while (!elapsed)
                    {
                        if (testServer.IsListening)
                        {
                            elapsed = true;
                        }
                        else
                        {
                            if (DateTime.Now.Second - start > 10)
                            {
                                elapsed = true;
                                failure = true;
                            }
                        }
                    }

                    testServer.Stop();

                    return !failure;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Get the absolute path in which JSONDB is installed.
        /// </summary>
        /// <returns>The absolute path of the folder which contains JSONDB</returns>
        public static string AppRoot()
        {
            return Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
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
                path += part.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            }

            return path.TrimEnd(Path.DirectorySeparatorChar);
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

        /// <summary>
        /// Sort an object with a function test.
        /// </summary>
        /// <param name="array">The object to sort</param>
        /// <param name="callback">The function which will be called with the next and the current values as parameters</param>
        /// <returns>The sorted object</returns>
        public static JObject Sort(JObject array, Func<JToken, JToken, bool> callback)
        {
            JObject ret = new JObject();
            JArray tmp = new JArray();

            foreach (var item in array)
            {
                tmp.Add(item.Value);
            }
            for (int i = 0, l = tmp.Count; i < l-1; i++)
            {
                for (int j = i+1; j < l; j++)
                {
                    if (callback(tmp[j], tmp[i]))
                    {
                        var k = tmp[i];
                        tmp[i] = tmp[j];
                        tmp[j] = k;
                    }
                }
            }
            for (int i = 0, l = tmp.Count; i < l; i++)
            {
                foreach (var current in array)
                {
                    if (JObject.DeepEquals(tmp[i], array[current.Key]))
                    {
                        ret[current.Key] = array[current.Key];
                        break;
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// Sort an object with a function test.
        /// </summary>
        /// <param name="array">The object to sort</param>
        /// <param name="callback">The function which will be called with the next and the current values as parameters</param>
        /// <returns>The sorted object</returns>
        public static JArray Sort(JArray array, Func<JToken, JToken, bool> callback)
        {
            JArray ret = array;

            for (int i = 0, l = ret.Count; i < l - 1; i++)
            {
                for (int j = i + 1; j < l; j++)
                {
                    if (callback(ret[j], ret[i]))
                    {
                        var k = ret[i];
                        ret[i] = ret[j];
                        ret[j] = k;
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Get a JArray of values contained in a JObject.
        /// </summary>
        /// <param name="array">The object to extract values</param>
        /// <returns>The JArray of extracted values</returns>
        public static JArray Values(JObject array)
        {
            JArray ret = new JArray();
            foreach (var item in array)
            {
                ret.Add(item.Value);
            }
            return ret;
        }

        /// <summary>
        /// Concatenate two or more JObjects in one.
        /// </summary>
        /// <param name="arrays">JObjects to concatenate</param>
        /// <returns>The concatenation of JObjects</returns>
        public static JObject Concat(params JObject[] arrays)
        {
            JObject ret = new JObject();
            for (int i = 0, l = arrays.Length; i < l; i++)
            {
                foreach (var array in arrays[i])
                {
                    ret[array.Key] = array.Value;
                }
            }
            return ret;
        }

        /// <summary>
        /// Concatenate two or more JArrays in one.
        /// </summary>
        /// <param name="arrays">JArrays to concatenate</param>
        /// <returns>The concatenation of JArrays</returns>
        public static JArray Concat(params JArray[] arrays)
        {
            JArray ret = new JArray();
            for (int i = 0, l = arrays.Length; i < l; i++)
            {
                foreach (var item in arrays[i])
                {
                    ret.Add(item);
                }
            }
            return ret;
        }

        /// <summary>
        /// Slice a JArray.
        /// </summary>
        /// <param name="array">The JArray to slice</param>
        /// <param name="start">The start index position</param>
        /// <param name="length">The length of th result JArray</param>
        /// <returns>The sliced JArray</returns>
        public static JArray Slice(JArray array, int start, int length)
        {
            JArray ret = new JArray();
            int j = 0;

            if (start + length > array.Count)
            {
                length = array.Count - start;
            }

            for (int i = start; j < length; i++, j++)
            {
                ret.Add(array[i]);
            }

            return ret;
        }

        /// <summary>
        /// Get the intersection between two JObjects.
        /// </summary>
        /// <param name="array1">The first array</param>
        /// <param name="array2">The second array</param>
        /// <returns>The intersection between the two JObjects</returns>
        public static JObject IntersectKey(JObject array1, JObject array2)
        {
            JObject ret = new JObject();
            foreach (var item in array1)
            {
                if (array2[item.Key] != null)
                {
                    ret[item.Key] = item.Value;
                }
            }
            return ret;
        }

        /// <summary>
        /// Flip a JObject's keys -> values pairs.
        /// </summary>
        /// <param name="array">The array to flip</param>
        /// <returns>The flipped array</returns>
        public static JObject Flip(JObject array)
        {
            JObject ret = new JObject();
            foreach (var item in array)
            {
                ret[item.Value.ToString()] = item.Key;
            }
            return ret;
        }

        /// <summary>
        /// Flip a JArray's keys -> values pairs.
        /// </summary>
        /// <param name="array">The array to flip</param>
        /// <returns>The flipped array</returns>
        public static JObject Flip(JArray array)
        {
            JObject ret = new JObject();
            for (int i = 0, l = array.Count; i < l; i++)
            {
                ret[array[i].ToString()] = i;
            }
            return ret;
        }

        /// <summary>
        /// Create a JObject with a JArray of key and a JArray of values.
        /// </summary>
        /// <param name="keys">The JArray of keys</param>
        /// <param name="values">The JArray of values</param>
        /// <returns>The combined JObject</returns>
        public static JObject Combine(JArray keys, JArray values)
        {
            JObject ret = new JObject();

            for (int i = 0, l = keys.Count; i < l; i++)
            {
                ret[keys[i].ToString()] = values[i];
            }

            return ret;
        }

        /// <summary>
        /// Merge two or more JObjects in one.
        /// </summary>
        /// <param name="arrays">JObjects to merge</param>
        /// <returns>The result of merge</returns>
        public static JObject Merge(params JObject[] arrays)
        {
            JObject ret = new JObject();

            foreach (var array in arrays)
            {
                foreach (var item in array)
                {
                    ret[item.Key] = item.Value;
                }
            }

            return ret;
        }
    }
}
