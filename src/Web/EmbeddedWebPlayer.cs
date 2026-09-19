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

            byte[]? data = null;
            if (File.Exists(filePath)) {
                data = File.ReadAllBytes(filePath);
            } else {
                // Fallback: Read from embedded assembly manifest resource
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = $"Scrim.Web.Assets.{relativePath.Replace('/', '.').Replace('\\', '.')}";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null) {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    data = ms.ToArray();
                } else {
                    var allResources = assembly.GetManifestResourceNames();
                    string targetSuffix = relativePath.Replace('/', '.').Replace('\\', '.');
                    string? match = Array.Find(allResources, r => r.EndsWith(targetSuffix, StringComparison.OrdinalIgnoreCase));
                    if (match != null) {
                        using var matchStream = assembly.GetManifestResourceStream(match);
                        if (matchStream != null) {
                            using var ms = new MemoryStream();
                            matchStream.CopyTo(ms);
                            data = ms.ToArray();
                        }
                    }
                }
            }

            if (data != null) {
                string ext = Path.GetExtension(relativePath).ToLower();
                response.ContentType = ext switch {
                    ".html" => "text/html; charset=utf-8",
                    ".css" => "text/css; charset=utf-8",
                    ".js" => "application/javascript; charset=utf-8",
                    ".webmanifest" => "application/manifest+json; charset=utf-8",
                    ".json" => "application/json; charset=utf-8",
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".svg" => "image/svg+xml",
                    ".ico" => "image/x-icon",
                    _ => "text/plain"
                };

                response.Headers.Add("Access-Control-Allow-Origin", "*");
                if (relativePath.Equals("sw.js", StringComparison.OrdinalIgnoreCase)) {
                    response.Headers.Add("Service-Worker-Allowed", "/");
                }

                response.ContentLength64 = data.Length;
                response.OutputStream.Write(data, 0, data.Length);
            } else {
                response.StatusCode = 404;
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes($"404 Not Found: {path}");
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            
            response.Close();
        }
    }
}
