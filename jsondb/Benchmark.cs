using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace JSONDB
{
    /// <summary>
    /// A benchmark point.
    /// </summary>
    internal struct BenchPoint
    {
        public long Time { get; set; }
        public long Memory { get; set; }
    }

    /// <summary>
    /// Class Benchmark.
    /// </summary>
    internal static class Benchmark
    {
        /// <summary>
        /// The list of bench points.
        /// </summary>
        private static Dictionary<string, BenchPoint> _marker = new Dictionary<string, BenchPoint>();

        /// <summary>
        /// Create a new benchpoint.
        /// </summary>
        /// <param name="name">The name of the benchpoint.</param>
        public static void Mark(string name)
        {
            var point = new BenchPoint
            {
                Time = Util.TimeStamp,
                Memory = System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64
            };

            if (_marker == null) _marker = new Dictionary<string, BenchPoint>();

            _marker[name] = point;
        }

        /// <summary>
        /// Calculate the elapsed time between two benchpoints.
        /// </summary>
        /// <param name="point1">The name of the first benchpoint.</param>
        /// <param name="point2">The name of the second benchpoint.</param>
        /// <returns>The elapsed time.</returns>
        public static long ElapsedTime(string point1, string point2)
        {
            if (point1 == null)
            {
                return 0;
            }

            if (!_marker.ContainsKey(point1))
            {
                Mark(point1);
            }
            if (!_marker.ContainsKey(point2))
            {
                Mark(point2);
            }

            long s = _marker[point1].Time;
            long e = _marker[point2].Time;

            return e - s;
        }

        /// <summary>
        /// Calculate the amount of memory used between two benchpoints.
        /// </summary>
        /// <param name="point1">The name of the first benchpoint.</param>
        /// <param name="point2">The name of the second benchpoint.</param>
        /// <returns>The amount of memory used.</returns>
        public static long MemoryUsage(string point1, string point2)
        {
            if (point1 == null)
            {
                return 0;
            }

            if (!_marker.ContainsKey(point1))
            {
                Mark(point1);
            }
            if (!_marker.ContainsKey(point2))
            {
                Mark(point2);
            }

            long s = _marker[point1].Memory;
            long e = _marker[point2].Memory;

            return e - s;
        }

        /// <summary>
        /// Clean al benchpoints and reset the benchmark.
        /// </summary>
        public static void Reset()
        {
            _marker = new Dictionary<string, BenchPoint>();
        }
    }
}
