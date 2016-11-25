using JSONDB.Library;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace JSONDB.Server
{
    public class ClientSocketServer : WebSocketBehavior
    {

        internal static Queue<QueryPool> Pools = new Queue<QueryPool>();

        protected override void OnMessage(MessageEventArgs e)
        {
            if (e.IsText)
            {
                Pools.Enqueue(new QueryPool(e.Data, Context));
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
            SendAsync(res.ToString(), (success) =>
            {
                Context.WebSocket.Close(CloseStatusCode.ServerError, e.Message);
            });
        }

        internal class QueryPool
        {
            private string _query;
            private WebSocketContext _context;

            public QueryPool(string query, WebSocketContext context)
            {
                _query = query;
                _context = context;
            }

            /// <summary>
            /// Send an enqueued query to the server.
            /// </summary>
            public void Send()
            {
                var ServerName = _context.Headers["jdb-server-name"] ?? String.Empty;
                if (ServerName == String.Empty)
                {
                    _context.WebSocket.Close(CloseStatusCode.InvalidData, "The request has not a server to connect on.");
                }

                else
                {
                    var DatabaseName = _context.Headers["jdb-database-name"] ?? String.Empty;
                    if (DatabaseName == String.Empty)
                    {
                        _context.WebSocket.Close(CloseStatusCode.InvalidData, "The request has not a database to connect on.");
                    }
                    else
                    {
                        try
                        {
                            Database _db = Library.JSONDB.Connect(ServerName, _context.Headers["authorization"].Replace("Basic ", ""));
                            _db.SetDatabase(DatabaseName);
                            JObject res = new JObject();
                            res["_jdb_error"] = false;
                            res["_error_msg"] = String.Empty;
                            res["_query_result"] = _db.Query(_query);
                            _context.WebSocket.SendAsync(res.ToString(), (success) =>
                            {
                                if (success)
                                {
                                    _context.WebSocket.Close(CloseStatusCode.Normal, "Received query executed.");
                                }
                                else
                                {
                                    _context.WebSocket.Close(CloseStatusCode.ServerError, "Error when executing the query.");
                                }
                            });
                        }
                        catch (Exception e)
                        {
                            _context.WebSocket.Close(CloseStatusCode.ServerError, e.Message);
                        }
                    }
                }
            }
        }
    }
}
