using System;
using System.IO;

namespace JSONDB.Server
{
    internal class Logger
    {
        // --------------------
        // ENUMERATIONS
        // --------------------
        public enum LogType
        {
            Info,
            Error,
            Debug
        }

        // --------------------
        // FIELDS
        // --------------------
        private string _file;
        private readonly string _path = Util.MakePath(Util.AppRoot(), "log");
        private StreamWriter _writer;

        // --------------------
        // ATTRIBUTES
        // --------------------
        public string FileName => _file;

        /// <summary>
        /// Class constructor.
        /// </summary>
        public Logger()
        {
            var name = GenerateName();

            if (!Util.Exists(_path))
            {
                Util.MakeDirectory(_path);
            }

            if (!Util.Exists(Util.MakePath(_path, name)))
            {
                Util.WriteTextFile(Util.MakePath(_path, name), "");
            }

            _file = name;

            _writer = File.AppendText(Util.MakePath(_path, _file));
            _writer.AutoFlush = true;
        }

        /// <summary>
        /// Log a message in the file.
        /// </summary>
        /// <param name="text">The message to log</param>
        /// <param name="type">The type of the message</param>
        public void Log(string text, LogType type)
        {
            if (_file != GenerateName())
            {
                Util.WriteTextFile(Util.MakePath(_path, GenerateName()), "");
                _file = GenerateName();
                _writer = File.AppendText(Util.MakePath(_path, _file));
                _writer.AutoFlush = true;
            }

            var lt = type == LogType.Info ? "INFO " : (type == LogType.Error ? "ERROR" : (type == LogType.Debug ? "DEBUG" : "     "));
            _writer.WriteLine(DateTime.Now.ToLongTimeString() + " - " + lt + " - " + text);
        }

        /// <summary>
        /// Generate the file name
        /// </summary>
        /// <returns>The name of the file</returns>
        public static string GenerateName()
        {
            return "jsondb-log-" + DateTime.Now.Day + "-" + DateTime.Now.Month + "-" + DateTime.Now.Year + ".log";
        }
    }
}
