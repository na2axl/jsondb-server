using System;
using System.Threading.Tasks;
using System.Threading;
using EdgeJs;

namespace JSONDB
{
    public class Server
    {

        public static async void Serve()
        {
            var createWebSocketServer = Edge.Func(@"return require('jdb-core')");

            await createWebSocketServer(null);
        }

        public static void Main(string[] args)
        {
            Task.Run((Action)Serve);
            new ManualResetEvent(false).WaitOne();
        }

    }
}
