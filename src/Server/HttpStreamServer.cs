using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Scrim.Server {
    public class HttpStreamServer : IStreamServer {
        private readonly BroadcastHub _hub;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;

        public HttpStreamServer(BroadcastHub hub) {
            _hub = hub;
        }

        public void Start(int port) {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{port}/");
            _listener.Start();
            
            _cts = new CancellationTokenSource();
            
            Task.Run(() => AcceptLoop(_cts.Token));
        }

        private async Task AcceptLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                var context = await _listener!.GetContextAsync();
                
                if (context.Request.Url!.AbsolutePath == "/stream") {
                    _ = HandleStreamClient(context, token);
                } else if (context.Request.Url!.AbsolutePath == "/") {
                    ServeWebPlayer(context);
                } else if (context.Request.Url!.AbsolutePath == "/api/events") {
                    _ = HandleSseClient(context, token);
                } else {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
        }

        private async Task HandleStreamClient(HttpListenerContext context, CancellationToken token) {
            var response = context.Response;
            response.ContentType = "audio/mpeg";
            response.SendChunked = true;

            var client = _hub.RegisterClient();
            try {
                using var stream = response.OutputStream;
                while (!token.IsCancellationRequested) {
                    var frame = await client.AudioChannel.Reader.ReadAsync(token);
                    await stream.WriteAsync(frame, token);
                    await stream.FlushAsync(token);
                }
            } catch {
            } finally {
                _hub.UnregisterClient(client.ClientId);
                response.Close();
            }
        }

        private async Task HandleSseClient(HttpListenerContext context, CancellationToken token) {
            var response = context.Response;
            response.ContentType = "text/event-stream";
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");

            try {
                using var writer = new StreamWriter(response.OutputStream);
                while (!token.IsCancellationRequested) {
                    await Task.Delay(5000, token);
                    await writer.WriteAsync("data: {\"ping\":true}\n\n");
                    await writer.FlushAsync();
                }
            } catch {
            } finally {
                response.Close();
            }
        }

        private void ServeWebPlayer(HttpListenerContext context) {
            var response = context.Response;
            response.ContentType = "text/html";
            string html = "<html><head><title>Scrim Radio</title></head><body style='background:#121212;color:white;'><h1>Scrim Radio</h1><audio controls src='/stream'></audio></body></html>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        public void Stop() {
            _cts?.Cancel();
            _listener?.Stop();
        }

        public void Dispose() {
            Stop();
            _listener?.Close();
        }
    }
}
