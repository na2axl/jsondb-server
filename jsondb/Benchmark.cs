using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JSONDB
{
    class Benchmark
    {
        private static JObject Marker = new JObject();

        public static void Mark(string name)
        {
            JObject Point = new JObject();
            Point["e"] = DateTime.Now.Millisecond;
            Point["m"] = 0;

            Marker[name] = Point;
        }

        public static int ElapsedTime(string point1, string point2)
        {
            if (point1 == null)
            {
                return 0;
            }

            if (Marker[point1] == null)
            {
                Mark(point1);
            }
            if (Marker[point2] == null)
            {
                Mark(point2);
            }

            int s = (int)Marker[point1]["e"];
            int e = (int)Marker[point2]["e"];

            return e - s;
        }

        public static int MemoryUsage(string point1, string point2)
        {
            if (point1 == null)
            {
                return 0;
            }

            if (Marker[point1] == null)
            {
                Mark(point1);
            }
            if (Marker[point2] == null)
            {
                Mark(point2);
            }

            int s = (int)Marker[point1]["m"];
            int e = (int)Marker[point2]["m"];

            return e - s;
        }
    }
}
