using JSONDB.Library;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace JSONDB.Server
{
    class HTTPServer : HttpServer
    {
        public HTTPServer(int port) : base(port)
        {
            InitializeServer();
        }

        public HTTPServer(int port, bool secure) : base(port, secure)
        {
            InitializeServer();
        }

        public HTTPServer(string url) : base(url)
        {
            InitializeServer();
        }

        public HTTPServer(System.Net.IPAddress address, int port) : base(address, port)
        {
            InitializeServer();
        }

        public HTTPServer(System.Net.IPAddress address, int port, bool secure) : base(address, port, secure)
        {
            InitializeServer();
        }

        public HTTPServer() : base()
        {
            InitializeServer();
        }

        private void InitializeServer()
        {
            // Set the rootpath of the web administration
            RootPath = Util.AppRoot() + "\\web\\public\\";

            // Set the HTTP GET request event
            OnGet += (sender, e) =>
            {
                var req = e.Request;
                var res = e.Response;

                var path = req.RawUrl;
                if (path.EndsWith("/"))
                {
                    path += "index.html";
                }

                var content = GetFile(path);
                if (content == null)
                {
                    res.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                if (path.EndsWith(".html"))
                {
                    res.ContentType = "text/html";
                    res.ContentEncoding = Encoding.UTF8;
                }
                else if (path.EndsWith(".js"))
                {
                    res.ContentType = "application/javascript";
                    res.ContentEncoding = Encoding.UTF8;
                }
                else if (path.EndsWith(".css"))
                {
                    res.ContentType = "text/css";
                    res.ContentEncoding = Encoding.UTF8;
                }
                else if (path.EndsWith(".ico"))
                {
                    res.ContentType = "image/x-icon";
                }

                res.Headers[HttpResponseHeader.Server] = "jsondb-server/1.0.0";

                res.WriteContent(content);
            };
        }
    }
}
