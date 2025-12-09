using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DeskPresenceService;

public class WifiHelper
{
    public bool IsOnHomeNetwork(string expectedSsid)
    {
        string? ssid = GetCurrentSsid();
        if (ssid == null) return false;
        return string.Equals(ssid, expectedSsid, StringComparison.OrdinalIgnoreCase);
    }

    private string? GetCurrentSsid()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = "wlan show interfaces",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) return null;

        string output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();

        var match = Regex.Match(output, @"SSID\s*:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
