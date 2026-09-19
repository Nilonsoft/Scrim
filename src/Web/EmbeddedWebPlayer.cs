using System;
using System.IO;
using System.Net;
using System.Reflection;

namespace Scrim.Web {
    public static class EmbeddedWebPlayer {
        public static void ServeAsync(HttpListenerContext context) {
            string path = context.Request.Url!.AbsolutePath;
            var response = context.Response;

            if (path == "/") {
                path = "/index.html";
            }

            // Since we're executing from bin/Debug/net..., the Assets folder needs to be copied, 
            // or we embed them. For simplicity, we'll read from the current app directory if we copy them.
            // But wait, it's better to just read from the filesystem relative to the executable for now.
            
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // The assets are in src/Web/Assets, but when compiled they should be copied to Output directory.
            // Let's assume they are copied to "Web/Assets/" in the output.
            string filePath = Path.Combine(baseDir, "Web", "Assets", path.TrimStart('/'));

            // Fallback for debugging if running directly from IDE
            if (!File.Exists(filePath)) {
                filePath = Path.Combine(baseDir, "..", "..", "..", "..", "src", "Web", "Assets", path.Replace("/assets/", "").TrimStart('/'));
            }

            if (File.Exists(filePath)) {
                string ext = Path.GetExtension(filePath).ToLower();
                response.ContentType = ext switch {
                    ".html" => "text/html",
                    ".css" => "text/css",
                    ".js" => "application/javascript",
                    _ => "text/plain"
                };

                byte[] buffer = File.ReadAllBytes(filePath);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            } else {
                response.StatusCode = 404;
                
                // For debugging
                Console.WriteLine($"404 Not Found: {filePath}");
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes($"404 Not Found: {path}");
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            
            response.Close();
        }
    }
}
