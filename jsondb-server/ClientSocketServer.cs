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
        /// Define if a query is currently executing.
        /// </summary>
        internal static bool Executing = false;

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
                Pools.Enqueue(new QueryPool(e.Data, Sessions, ID));
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
            Executing = false;
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
            private readonly WebSocketSessionManager _sessions;

            private Dictionary<string, string> _parsedQuery;

            public QueryPool(string query, WebSocketSessionManager sessions, string id)
            {
                _query = query;
                _sessions = sessions;
                _id = id;
                _context = _sessions[_id].Context;
                _parseQuery();
            }

            /// <summary>
            /// Send an enqueued query to the server.
            /// </summary>
            public void Send()
            {
                IWebSocketSession s;
                if (_sessions.TryGetSession(_id, out s))
                {
                    var serverName = _parsedQuery["jsondb-server-name"];
                    if (string.IsNullOrEmpty(serverName))
                    {
                       _sessions.CloseSession(_id, CloseStatusCode.InvalidData, "The request has not a server to connect on.");
                    }

                    else
                    {
                        var databaseName = _parsedQuery["jsondb-database-name"];
                        if (string.IsNullOrEmpty(databaseName))
                        {
                            _sessions.CloseSession(_id, CloseStatusCode.InvalidData, "The request has not a database to connect on.");
                        }
                        else
                        {
                            try
                            {
                                Executing = true;
                                var db = Jsondb.Connect(serverName, _parsedQuery["jsondb-user-name"], _parsedQuery["jsondb-user-password"]);
                                db.SetDatabase(databaseName);
                                var res = db.Query(_parsedQuery["jsondb-query-request"]);
                                _sessions.SendToAsync(res.ToString(), _id, (success) =>
                                {
                                    if (success)
                                    {
                                        Logger.Log("Query executed with ID: " + _id, Logger.LogType.Info);
                                        _sessions.CloseSession(_id, CloseStatusCode.Normal, "Received query executed.");
                                    }
                                    else
                                    {
                                        _sessions.CloseSession(_id, CloseStatusCode.ServerError, "Error when executing the query.");
                                    }
                                });
                            }
                            catch (Exception e)
                            {
                                _sessions.CloseSession(_id, CloseStatusCode.ServerError, e.Message);
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Parses the current query.
            /// </summary>
            private void _parseQuery()
            {
                _parsedQuery = new Dictionary<string, string>();
                var parts = System.Text.RegularExpressions.Regex.Split(_query, "\\[:Separator:\\]");

                foreach (var t in parts)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(t, "\\[Key:(.+)\\]=\\[Value:(.+)\\]");
                        _parsedQuery[m.Groups[1].Value.Trim('"')] = m.Groups[2].Value.Trim('"');
                    }
                }
            }
        }
    }
}
