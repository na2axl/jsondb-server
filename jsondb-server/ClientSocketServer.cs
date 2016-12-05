using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace JSONDB.Server
{
    /// <summary>
    /// Socket server used to execute incomming queries.
    /// </summary>
    internal class ClientSocketServer : WebSocketBehavior
    {

        /// <summary>
        /// List of queries to execute with FIFO method.
        /// </summary>
        internal static Queue<QueryPool> Pools = new Queue<QueryPool>();

        /// <summary>
        /// Logger.
        /// </summary>
        private static readonly Logger Logger = new Logger();

        /// <summary>
        /// Occur when a new connection is opened by the client.
        /// </summary>
        protected override void OnOpen()
        {
            Logger.Log("New Connection with ID: " + ID, Logger.LogType.Info);
        }

        /// <summary>
        /// Occur when a new message (query) is sent by the client.
        /// </summary>
        /// <param name="e">The message event.</param>
        protected override void OnMessage(MessageEventArgs e)
        {
            Logger.Log("Incomming message with ID: " + ID, Logger.LogType.Info);
            if (e.IsText)
            {
                Pools.Enqueue(new QueryPool(e.Data, Context, ID));
                Logger.Log("Query enqueued with ID: " + ID, Logger.LogType.Info);
            }
            else
            {
                Context.WebSocket.Close(CloseStatusCode.UnsupportedData, "JSONDB server accepts only text as incoming data type.");
            }
        }

        /// <summary>
        /// Occur when the connection is closed by the server.
        /// </summary>
        /// <param name="e">The close event.</param>
        protected override void OnClose(CloseEventArgs e)
        {
            Logger.Log("Connection " + ID + " closed: " + e.Reason, Logger.LogType.Info);
        }

        /// <summary>
        /// Occur when an error occured while the client is connected.
        /// </summary>
        /// <param name="e">The error event.</param>
        protected override void OnError(ErrorEventArgs e)
        {
            var res = new JObject
            {
                ["error"] = true,
                ["result"] = e.Message
            };
            SendAsync(res.ToString(), (success) =>
            {
                Context.WebSocket.Close(CloseStatusCode.ServerError, e.Message);
            });
        }

        /// <summary>
        /// Nested class used to manage the execution of queries.
        /// </summary>
        internal class QueryPool
        {
            private readonly string _query;
            private readonly WebSocketContext _context;
            private readonly string _id;

            public QueryPool(string query, WebSocketContext context, string id)
            {
                _query = query;
                _context = context;
                _id = id;
            }

            /// <summary>
            /// Send an enqueued query to the server.
            /// </summary>
            public void Send()
            {
                var serverName = _context.Headers["jdb-server-name"];
                if (string.IsNullOrEmpty(serverName))
                {
                    _context.WebSocket.Close(CloseStatusCode.InvalidData, "The request has not a server to connect on.");
                }

                else
                {
                    var databaseName = _context.Headers["jdb-database-name"];
                    if (string.IsNullOrEmpty(databaseName))
                    {
                        _context.WebSocket.Close(CloseStatusCode.InvalidData, "The request has not a database to connect on.");
                    }
                    else
                    {
                        try
                        {
                            var db = Jsondb.Connect(serverName, _context.Headers["authorization"].Replace("Basic ", ""));
                            db.SetDatabase(databaseName);
                            var res = db.Query(_query);
                            _context.WebSocket.SendAsync(res.ToString(), (success) =>
                            {
                                if (success)
                                {
                                    Logger.Log("Query executed with ID: " + _id, Logger.LogType.Info);
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
