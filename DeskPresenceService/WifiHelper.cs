using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DeskPresenceService;

public class WifiHelper
{
    public bool IsOnHomeNetwork(string expectedGatewayIp)
    {
        if (string.IsNullOrWhiteSpace(expectedGatewayIp))
            return true;

        string expected = expectedGatewayIp.Trim();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Only consider active Wi-Fi
                if (nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                    continue;

                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                var ipProps = nic.GetIPProperties();
                foreach (var gw in ipProps.GatewayAddresses)
                {
                    if (gw.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string gateway = gw.Address.ToString();
                        if (string.Equals(gateway, expected, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
            // If something unexpected happens, don't break WFH logic
            return true;
        }

        return false;
    }
}
