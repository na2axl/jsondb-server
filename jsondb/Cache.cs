using System;
using System.Collections.Generic;

namespace JSONDB.Library
{
    internal class Cache
    {
        /// <summary>
        /// An array of all cached data.
        /// </summary>
        private static Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Retrieve a cached table data.
        /// </summary>
        /// <param name="path">The path to the table</param>
        /// <returns>The cached data</returns>
        internal static string Get(string path)
        {
            if (!Data.ContainsKey(path))
            {
                Data[path] = Database.GetTableData(path).ToString();
            }

            return Data[path].ToString();
        }

        /// <summary>
        /// Update cached table data.
        /// </summary>
        /// <param name="path">The path to the table</param>
        /// <param name="data">The data to cache</param>
        internal static void Update(string path, string data)
        {
            if (data == String.Empty)
            {
                Data[path] = Database.GetTableData(path).ToString();
            }
            else
            {
                Data[path] = data;
            }
        }

        /// <summary>
        /// Reset all the cache.
        /// </summary>
        internal static void Reset()
        {
            Data = new Dictionary<string, string>();
        }
    }
}
