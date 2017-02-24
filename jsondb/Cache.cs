using System.Collections.Generic;

namespace JSONDB
{
    /// <summary>
    /// Class Cache.
    /// </summary>
    internal static class Cache
    {
        /// <summary>
        /// An array of all cached data.
        /// </summary>
        private static Dictionary<string, string> _data = new Dictionary<string, string>();

        /// <summary>
        /// Retrieve a cached table data.
        /// </summary>
        /// <param name="path">The path to the table</param>
        /// <returns>The cached data</returns>
        public static string Get(string path)
        {
            if (!_data.ContainsKey(path))
            {
                _data[path] = Database.GetTableData(path).ToString();
            }

            return _data[path];
        }

        /// <summary>
        /// Update cached table data.
        /// </summary>
        /// <param name="path">The path to the table</param>
        /// <param name="data">The data to cache</param>
        public static void Update(string path, string data)
        {
            if (data == string.Empty)
            {
                _data[path] = Database.GetTableData(path).ToString();
            }
            else
            {
                _data[path] = data;
            }
        }

        /// <summary>
        /// Reset all the cache.
        /// </summary>
        public static void Reset()
        {
            _data = new Dictionary<string, string>();
        }
    }
}
