using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace JSONDB.Server
{
    class APISocketServer : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            /// TODO: Implement this method
            base.OnMessage(e);
        }
    }
}
