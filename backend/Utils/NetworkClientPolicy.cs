using System.Net;
using System.Net.Sockets;

namespace Virexaone.FMS.Backend.Utils;

public static class NetworkClientPolicy
{
    public static bool IsTrusted(IPAddress? address, IEnumerable<string> additionalAddresses)
    {
        if (address == null) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] octets = address.GetAddressBytes();
            if (octets[0] == 10 ||
                octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31 ||
                octets[0] == 192 && octets[1] == 168)
                return true;
        }

        return additionalAddresses.Any(value =>
            IPAddress.TryParse(value, out IPAddress? allowed) && allowed.Equals(address));
    }
}
