using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Scrim.Server {
    public interface INetworkDiscoveryService {
        string PrimaryLocalIp { get; }
        string PublicIp { get; }
        bool IsUpnpMapped { get; }
        string UpnpStatus { get; }
        List<string> GetAllLocalIps();
        Task RefreshPublicIpAsync();
        bool TryMapUpnpPort(int port);
        void TryUnmapUpnpPort(int port);
        string GetLocalShareUrl(int port);
        string GetPublicShareUrl(int port);
    }

    public class NetworkDiscoveryService : INetworkDiscoveryService {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
        private int _lastMappedPort = 0;

        public string PrimaryLocalIp => GetAllLocalIps().FirstOrDefault() ?? "127.0.0.1";
        public string PublicIp { get; private set; } = "Discovering...";
        public bool IsUpnpMapped { get; private set; } = false;
        public string UpnpStatus { get; private set; } = "Standby";

        public NetworkDiscoveryService() {
            _ = RefreshPublicIpAsync();
        }

        public List<string> GetAllLocalIps() {
            var ips = new List<string>();
            try {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && 
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                foreach (var iface in interfaces) {
                    var props = iface.GetIPProperties();
                    foreach (var addr in props.UnicastAddresses) {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork) {
                            string ipStr = addr.Address.ToString();
                            if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254.")) {
                                ips.Add(ipStr);
                            }
                        }
                    }
                }
            } catch { }

            return ips.Distinct().ToList();
        }

        public async Task RefreshPublicIpAsync() {
            string[] endpoints = {
                "https://api.ipify.org",
                "https://icanhazip.com",
                "https://checkip.amazonaws.com"
            };

            foreach (var url in endpoints) {
                try {
                    string response = (await _httpClient.GetStringAsync(url)).Trim();
                    if (!string.IsNullOrEmpty(response) && response.Length <= 45 && System.Net.IPAddress.TryParse(response, out _)) {
                        PublicIp = response;
                        return;
                    }
                } catch { }
            }

            if (PublicIp == "Discovering...") {
                PublicIp = "Unavailable";
            }
        }

        public bool TryMapUpnpPort(int port) {
            string localIp = PrimaryLocalIp;
            if (string.IsNullOrEmpty(localIp) || localIp == "127.0.0.1") {
                UpnpStatus = "No active local network interface";
                return false;
            }

            try {
                Type? natType = Type.GetTypeFromProgID("HNetCfg.NATUPnP");
                if (natType != null) {
                    dynamic? nat = Activator.CreateInstance(natType);
                    dynamic? mappings = nat?.StaticPortMappingCollection;
                    if (mappings != null) {
                        try {
                            mappings.Remove(port, "TCP");
                        } catch { }

                        mappings.Add(port, "TCP", port, localIp, true, "Scrim Radio Broadcast");
                        _lastMappedPort = port;
                        IsUpnpMapped = true;
                        UpnpStatus = $"UPnP Port {port} forwarded on router";
                        return true;
                    }
                }

                UpnpStatus = "UPnP service unavailable on router (Manual Port Forward required)";
                IsUpnpMapped = false;
                return false;
            } catch (Exception ex) {
                UpnpStatus = $"Router UPnP disabled: {ex.Message}";
                IsUpnpMapped = false;
                return false;
            }
        }

        public void TryUnmapUpnpPort(int port) {
            int targetPort = port > 0 ? port : _lastMappedPort;
            if (targetPort <= 0) return;

            try {
                Type? natType = Type.GetTypeFromProgID("HNetCfg.NATUPnP");
                if (natType != null) {
                    dynamic? nat = Activator.CreateInstance(natType);
                    dynamic? mappings = nat?.StaticPortMappingCollection;
                    if (mappings != null) {
                        mappings.Remove(targetPort, "TCP");
                    }
                }
            } catch { } finally {
                IsUpnpMapped = false;
                UpnpStatus = "Standby";
            }
        }

        public string GetLocalShareUrl(int port) {
            return $"http://{PrimaryLocalIp}:{port}";
        }

        public string GetPublicShareUrl(int port) {
            if (!string.IsNullOrEmpty(PublicIp) && PublicIp != "Discovering..." && PublicIp != "Unavailable") {
                return $"http://{PublicIp}:{port}";
            }
            return GetLocalShareUrl(port);
        }
    }
}
