using System;
using System.Threading.Tasks;
using EdgeJs;

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

        public static bool ValidateAdress(string adress)
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
    }
}
