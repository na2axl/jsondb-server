using Newtonsoft.Json.Linq;
using System;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace JSONDB.Server
{
    public class ClientSocketServer : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            if (e.IsText)
            {
                var ServerName = Context.Headers["jdb-server-name"] ?? String.Empty;
                if (ServerName == String.Empty)
                {
                    Context.WebSocket.Close(CloseStatusCode.InvalidData, "The request has not a server to connect on.");
                }
                else
                {
                    var ServerList = Configuration.GetConfigFile("users");
                    if (ServerList[ServerName] != null)
                    {
                        var CurrentCredentials = Convert.ToBase64String(
                            Encoding.UTF8.GetBytes(ServerList[ServerName]["username"].ToString() + ":" + ServerList[ServerName]["password"].ToString())
                        );
                        var SentCredentials = Context.Headers["authorization"].Replace("Basic ", "");

                        if (CurrentCredentials == SentCredentials)
                        {
                            /// TODO: Implement JSONDB in C# to use it here...
                            JObject res = new JObject();
                            res["_jdb_error"] = false;
                            res["_error_msg"] = String.Empty;
                            res["_query_result"] = QueryParser.Parse(e.Data);
                            Send(res.ToString());
                            Context.WebSocket.Close(CloseStatusCode.Normal, "Received query executed.");
                        }
                        else
                        {
                            Context.WebSocket.Close(CloseStatusCode.ServerError, "Bad user credentials.");
                        }
                    }
                    else
                    {
                        Context.WebSocket.Close(CloseStatusCode.ServerError, "No server was found.");
                    }
                }
            }
            else
            {
                Context.WebSocket.Close(CloseStatusCode.UnsupportedData, "JSONDB server accepts only text as incoming data type.");
            }
        }

        protected override void OnError(ErrorEventArgs e)
        {
            JObject res = new JObject();
            res["_jdb_error"] = true;
            res["_error_msg"] = e.Message;
            res["_query_result"] = new JObject();
            Send(res.ToString());
        }
    }
}
