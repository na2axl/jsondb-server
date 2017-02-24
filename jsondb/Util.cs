using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using WebSocketSharp.Server;

namespace JSONDB
{
    /// <summary>
    /// Class Util
    /// </summary>
    public static class Util
    {
        /// <summary>
        /// Return the UNIX timestamp.
        /// </summary>
        public static long TimeStamp
        {
            get
            {
                return (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
            }
        }

        /// <summary>
        /// Validate an IP address.
        /// </summary>
        /// <param name="address">The IP address</param>
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
            if (!ValidateAddress(address)) return false;

            var addr = IPAddress.Parse(address);
            try
            {
                var testServer = new WebSocketServer(addr, 2717);
                testServer.Start();

                var start = DateTime.Now.Second;
                var elapsed = false;
                var failure = false;

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

        /// <summary>
        /// Get the absolute path in which JSONDB is installed.
        /// </summary>
        /// <returns>The absolute path of the folder which contains JSONDB</returns>
        public static string AppRoot()
        {
            var directoryName = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
            return directoryName?.TrimEnd(Path.DirectorySeparatorChar) ?? "";
        }

        /// <summary>
        /// Make a path by joining all the parts with the character '\'.
        /// </summary>
        /// <param name="parts">The parts of the path</param>
        /// <returns>The path.</returns>
        public static string MakePath(params string[] parts)
        {
            var path = parts.Aggregate(string.Empty, (current, part) => current + (part.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar));

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
        /// Get the list of subdirectories in a directory.
        /// </summary>
        /// <param name="path">The path of the directory</param>
        /// <returns>The list of directories</returns>
        public static string[] GetDirectoriesList(string path)
        {
            var dir = Directory.GetDirectories(path);

            for (int i = 0, l = dir.Length; i < l; i++)
            {
                dir[i] = Path.GetFileNameWithoutExtension(dir[i]);
            }

            return dir;
        }

        /// <summary>
        /// Get the list of files in a directory.
        /// </summary>
        /// <param name="path">The path of the directory</param>
        /// <returns>The list of files</returns>
        public static string[] GetFilesList(string path)
        {
            var files = Directory.GetFiles(path);

            for (int i = 0, l = files.Length; i < l; i++)
            {
                files[i] = Path.GetFileNameWithoutExtension(files[i]);
            }

            return files;
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
            return Sha1(value + "<~>:q;axMw|S01%@yu*lfr^Q#j)OG<Z_dQOvzuTZsa^sm0K}*u9{d3A[ekV;/x[c");
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
        public static string Sha1(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);

            var sha1 = System.Security.Cryptography.SHA1.Create();
            var hashBytes = sha1.ComputeHash(bytes);

            return HexStringFromBytes(hashBytes);
        }

        /// <summary>
        /// Compute md5 hash for string encoded as UTF8
        /// </summary>
        /// <param name="s">String to be hashed</param>
        /// <returns></returns>
        public static string Md5(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);

            var md5 = System.Security.Cryptography.MD5.Create();
            var hashBytes = md5.ComputeHash(bytes);

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
            foreach (var b in bytes)
            {
                var hex = b.ToString("x2");
                sb.Append(hex);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Test if a file is available for writing.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>true if the file is writable, false otherwise</returns>
        public static bool IsWritable(string path)
        {
            try
            {
                var stream = File.Open(path, FileMode.Open);
                var canWrite = stream.CanWrite;
                stream.Close();
                return canWrite;
            }
            catch (Exception)
            {
                return false;
            }

        }

        /// <summary>
        /// Lock a file for read/write operations
        /// </summary>
        /// <param name="path">The path to the file to lock</param>
        public static void LockFile(string path)
        {
            if (Exists(path) && !Exists(path + ".lock"))
            {
                File.Create(path + ".lock").Close();
            }
        }

        /// <summary>
        /// Unlock a locked file.
        /// </summary>
        /// <param name="path">The path to the file to unlock</param>
        public static void UnlockFile(string path)
        {
            if (Exists(path) && Exists(path + ".lock"))
            {
                File.Delete(path + ".lock");
            }
        }

        /// <summary>
        /// Check if a file is locked.
        /// </summary>
        /// <param name="path">The path to the file to check</param>
        /// <returns>true if the file is locked and false otherwise</returns>
        public static bool FileIsLocked(string path)
        {
            return Exists(path + ".lock");
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
            return !Exists(path) ? null : File.ReadAllText(path);
        }

        /// <summary>
        /// Sort an object with a test function.
        /// </summary>
        /// <param name="array">The object to sort</param>
        /// <param name="callback">The function which will be called with the next and the current values as parameters</param>
        /// <returns>The sorted object</returns>
        public static JObject Sort(JObject array, Func<JToken, JToken, bool> callback)
        {
            var ret = new JObject();
            var tmp = new JArray();

            foreach (var item in array)
            {
                tmp.Add(item.Value);
            }
            for (int i = 0, l = tmp.Count; i < l-1; i++)
            {
                for (var j = i+1; j < l; j++)
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
                    if (JToken.DeepEquals(tmp[i], array[current.Key]))
                    {
                        ret[current.Key] = array[current.Key];
                        break;
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// Sort a JObject by keys with a test function.
        /// </summary>
        /// <param name="array">The JObject to sort</param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static JObject KeySort(JObject array, Func<JToken, JToken, bool> callbackInf, Func<JToken, JToken, bool> callbackSup)
        {
            var ret = new JObject();
            var tmp = new JArray();

            foreach (var i in array)
            {
                tmp.Add(i.Key);
            }

            _quickSort(ref tmp, 0, tmp.Count - 1, callbackInf, callbackSup);

            //for (int i = 0, l = tmp.Count; i < l - 1; i++)
            //{
            //    for (var j = i + 1; j < l; j++)
            //    {
            //        if (callback(tmp[j].ToString(), tmp[i].ToString()))
            //        {
            //            var k = tmp[i];
            //            tmp[i] = tmp[j];
            //            tmp[j] = k;
            //        }
            //    }
            //}

            for (int i = 0, l = tmp.Count; i < l; i++)
            {
                ret[tmp[i].ToString()] = array[tmp[i].ToString()];
                //foreach (var item in array)
                //{
                //    if (tmp[i].ToString() == item.Key)
                //    {
                //        ret[item.Key] = array[item.Key];
                //        break;
                //    }
                //}
            }

            return ret;
        }

        /// <summary>
        /// Sort an object with a test function.
        /// </summary>
        /// <param name="array">The object to sort</param>
        /// <param name="callback">The function which will be called with the next and the current values as parameters</param>
        /// <returns>The sorted object</returns>
        public static JArray Sort(JArray array, Func<JToken, JToken, bool> leftCompare, Func<JToken, JToken, bool> rightCompare)
        {
            var ret = array;

            _quickSort(ref ret, 0, ret.Count - 1, leftCompare, rightCompare);

            return ret;
        }

        /// <summary>
        /// QuickSort algorithm.
        /// </summary>
        /// <param name="array">The array to sort.</param>
        /// <param name="first">The index of the first element.</param>
        /// <param name="last">The index of the last element.</param>
        /// <param name="leftCompare">The callback to use to do the first check.</param>
        /// <param name="rightCompare">The callback to use to do the second check.</param>
        private static void _quickSort(ref JArray array, int first, int last, Func<JToken, JToken, bool> leftCompare, Func<JToken, JToken, bool> rightCompare)
        {
            if (first < last)
            {
                if (last - first > 10)
                {
                    int p = _partition(ref array, first, last, leftCompare, rightCompare);
                    if (p - first < last - p - 1)
                    {
                        _quickSort(ref array, first, p, leftCompare, rightCompare);
                        _quickSort(ref array, p + 1, last, leftCompare, rightCompare);
                    }
                    else
                    {
                        _quickSort(ref array, p + 1, last, leftCompare, rightCompare);
                        _quickSort(ref array, first, p, leftCompare, rightCompare);
                    }
                }
                else
                {
                    for (int i = first, l = last; i < l; i++)
                    {
                        for (int j = i + 1; j <= l; j++)
                        {
                            if (leftCompare(array[j], array[i]))
                            {
                                var k = array[i];
                                array[i] = array[j];
                                array[j] = k;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The QuickSort Hoare partition scheme.
        /// </summary>
        /// <param name="array">The array to sort.</param>
        /// <param name="first">The index of the first element.</param>
        /// <param name="last">The index of the last element.</param>
        /// <param name="leftCompare">The callback to use to do the first check.</param>
        /// <param name="rightCompare">The callback to use to do the second check.</param>
        /// <returns>The index of the pivot</returns>
        private static int _partition(ref JArray array, int first, int last, Func<JToken, JToken, bool> leftCompare, Func<JToken, JToken, bool> rightCompare)
        {
            int pIndex = first + (last - first) / 2;
            JToken pivot = array[first];
            int i = first - 1;
            int j = last + 1;

            while (true)
            {
                do i++; while (leftCompare(array[i], pivot));
                do j--; while (rightCompare(array[j], pivot));
                if (i >= j) return j;
                _swap(ref array, i, j);
            }
        }

        /// <summary>
        /// The swapper for QuickSort.
        /// </summary>
        /// <param name="array">The array</param>
        /// <param name="l">The index of the first element.</param>
        /// <param name="r">The index of the second element.</param>
        private static void _swap(ref JArray array, int l, int r)
        {
            JToken tmp = array[l];
            array[l] = array[r];
            array[r] = tmp;
        }

        /// <summary>
        /// Get a JArray of values contained in a JObject.
        /// </summary>
        /// <param name="array">The object to extract values</param>
        /// <returns>The JArray of extracted values</returns>
        public static JArray Values(JObject array)
        {
            var ret = new JArray();
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
            var ret = new JObject();
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
            var ret = new JArray();
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
            var ret = new JArray();
            var j = 0;

            if (start + length > array.Count)
            {
                length = array.Count - start;
            }

            for (var i = start; j < length; i++, j++)
            {
                ret.Add(array[i]);
            }

            return ret;
        }

        /// <summary>
        /// Slice a JArray.
        /// </summary>
        /// <param name="array">The JArray to slice</param>
        /// <param name="start">The start index position</param>
        /// <returns>The sliced JArray</returns>
        public static JArray Slice(JArray array, int start)
        {
            return Slice(array, start, array.Count);
        }

        /// <summary>
        /// Slice a JObject.
        /// </summary>
        /// <param name="array">The JObject to slice</param>
        /// <param name="start">The start index position</param>
        /// <param name="length">The length of th result JArray</param>
        /// <returns>The sliced JObject</returns>
        public static JObject Slice(JObject array, int start, int length)
        {
            var ret = new JObject();
            var j = 0;
            var i = 0;

            if (start + length > array.Count)
            {
                length = array.Count - start;
            }

            foreach (var item in array)
            {
                if (i >= start && j < length)
                {
                    ret[item.Key] = item.Value;
                    ++j;
                }
                ++i;
            }

            return ret;
        }

        /// <summary>
        /// Slice a JObject.
        /// </summary>
        /// <param name="array">The JObject to slice</param>
        /// <param name="start">The start index position</param>
        /// <returns>The sliced JObject</returns>
        public static JObject Slice(JObject array, int start)
        {
            return Slice(array, start, array.Count);
        }

        /// <summary>
        /// Get the intersection between two JObjects.
        /// </summary>
        /// <param name="array1">The first array</param>
        /// <param name="array2">The second array</param>
        /// <returns>The intersection between the two JObjects</returns>
        public static JObject IntersectKey(JObject array1, JObject array2)
        {
            var ret = new JObject();
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
            var ret = new JObject();
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
            var ret = new JObject();
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
            var ret = new JObject();

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
            var ret = new JObject();

            foreach (var array in arrays)
            {
                foreach (var item in array)
                {
                    ret[item.Key] = item.Value;
                }
            }

            return ret;
        }

        /// <summary>
        /// Merge two or more JArrays in one.
        /// </summary>
        /// <param name="arrays">JArrays to merge</param>
        /// <returns>The result of the merge</returns>
        public static JArray Merge(params JArray[] arrays)
        {
            var ret = new JArray();

            foreach (var array in arrays)
            {
                var settings = new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Union,
                    MergeNullValueHandling = MergeNullValueHandling.Merge
                };
                ret.Merge(array, settings);
            }

            return ret;
        }

        /// <summary>
        /// Compute difference between two or more JArrays.
        /// </summary>
        /// <param name="arrays">JArrays</param>
        /// <returns>The computed difference</returns>
        public static JArray Diff(params JArray[] arrays)
        {
            var ret = new JArray();
            var ignore = new JObject();

            for (int j = 0, m = arrays.Length; j < m; j++)
            {
                for (int i = 0, l = arrays[j].Count; i < l; i++)
                {
                    if (Array.IndexOf(ret.ToArray(), arrays[j][i].ToString()) == -1 && ignore[arrays[j][i].ToString()] == null)
                    {
                        ret.Add(arrays[j][i]);
                    }
                    else
                    {
                        ret.RemoveAt(Array.IndexOf(ret.ToArray(), arrays[j][i].ToString()));
                        ignore[arrays[j][i].ToString()] = true;
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Compute difference by keys between two or more JObjects.
        /// </summary>
        /// <param name="arrays">JObjects</param>
        /// <returns>The computed difference</returns>
        public static JObject DiffKey(params JObject[] arrays)
        {
            var ret = new JObject();
            var ignore = new JObject();

            foreach (var array in arrays)
            {
                foreach (var item in array)
                {
                    if (ret[item.Key] == null && ignore[item.Key] == null)
                    {
                        ret[item.Key] = item.Value;
                    }
                    else
                    {
                        ret.Remove(item.Key);
                        ignore[item.Key] = true;
                    }
                }
            }

            return ret;
        }
    }
}
