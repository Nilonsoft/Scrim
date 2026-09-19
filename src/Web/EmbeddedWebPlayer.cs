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
            
            string relativePath = path.TrimStart('/');
            if (relativePath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) {
                relativePath = relativePath.Substring("assets/".Length);
            }
            if (string.IsNullOrEmpty(relativePath)) {
                relativePath = "index.html";
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = Path.Combine(baseDir, "Web", "Assets", relativePath);

            // Fallbacks for development / debugging
            if (!File.Exists(filePath)) {
                filePath = Path.Combine(baseDir, "..", "..", "..", "..", "src", "Web", "Assets", relativePath);
            }
            if (!File.Exists(filePath)) {
                filePath = Path.Combine(baseDir, "..", "..", "..", "src", "Web", "Assets", relativePath);
            }

            if (File.Exists(filePath)) {
                string ext = Path.GetExtension(filePath).ToLower();
                response.ContentType = ext switch {
                    ".html" => "text/html",
                    ".css" => "text/css",
                    ".js" => "application/javascript",
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".svg" => "image/svg+xml",
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
