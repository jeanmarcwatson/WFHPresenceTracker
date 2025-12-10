using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DeskPresenceService
{
    public class WifiHelper
    {
        private readonly ILogger<WifiHelper> _logger;

        public WifiHelper(ILogger<WifiHelper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Returns the default IPv4 gateway address as a string
        /// (for example "192.168.1.1"), or null if it cannot be found.
        /// Prefers Wi-Fi interfaces over Ethernet and ignores 0.0.0.0.
        /// </summary>
        public string? GetDefaultGateway()
        {
            try
            {
                var allNics = NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(n =>
                        n.OperationalStatus == OperationalStatus.Up &&
                        (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                         n.NetworkInterfaceType == NetworkInterfaceType.Ethernet));

                // Prefer Wi-Fi (Wireless80211) first, then other interfaces
                var ordered = allNics
                    .OrderByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    .ThenBy(n => n.Name);

                foreach (var nic in ordered)
                {
                    var props = nic.GetIPProperties();

                    var gwAddr = props.GatewayAddresses
                        .Select(g => g.Address)
                        .FirstOrDefault(a =>
                            a.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(a) &&
                            !a.Equals(IPAddress.Any)); // ignore 0.0.0.0

                    if (gwAddr != null)
                    {
                        string addr = gwAddr.ToString();
                        _logger.LogInformation(
                            "Default gateway from interface {Name} ({Type}): {Gateway}",
                            nic.Name,
                            nic.NetworkInterfaceType,
                            addr);

                        return addr;
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Interface {Name} ({Type}) has no valid IPv4 gateway (skipping).",
                            nic.Name,
                            nic.NetworkInterfaceType);
                    }
                }

                _logger.LogInformation("No usable IPv4 default gateway found on any active interface.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get default gateway.");
                return null;
            }
        }

        /// <summary>
        /// Convenience helper to check if the current default gateway
        /// matches the configured "home" gateway address.
        /// </summary>
        public bool IsOnHomeNetwork(string expectedGateway)
        {
            var gw = GetDefaultGateway();
            if (string.IsNullOrWhiteSpace(gw))
                return false;

            return string.Equals(gw, expectedGateway, StringComparison.OrdinalIgnoreCase);
        }
    }
}
