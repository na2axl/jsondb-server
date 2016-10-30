using System;
using System.Collections.Generic;

namespace JSONDB.Library
{
    struct BenchPoint
    {
        public long Time { get; set; }
        public int Memory { get; set; }
    }

    class Benchmark
    {
        private static Dictionary<string, BenchPoint> Marker = new Dictionary<string, BenchPoint>();

        public static void Mark(string name)
        {
            BenchPoint Point = new BenchPoint();
            Point.Time = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
            Point.Memory = 0;

            if (Marker == null) Marker = new Dictionary<string, BenchPoint>();

            Marker[name] = Point;
        }

        public static long ElapsedTime(string point1, string point2)
        {
            if (point1 == null)
            {
                return 0;
            }

            if (!Marker.ContainsKey(point1))
            {
                Mark(point1);
            }
            if (!Marker.ContainsKey(point2))
            {
                Mark(point2);
            }

            long s = Marker[point1].Time;
            long e = Marker[point2].Time;

            return e - s;
        }

        public static int MemoryUsage(string point1, string point2)
        {
            if (point1 == null)
            {
                return 0;
            }

            if (!Marker.ContainsKey(point1))
            {
                Mark(point1);
            }
            if (!Marker.ContainsKey(point2))
            {
                Mark(point2);
            }

            int s = Marker[point1].Memory;
            int e = Marker[point2].Memory;

            return e - s;
        }
    }
}
