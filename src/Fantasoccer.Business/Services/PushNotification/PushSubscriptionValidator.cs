using System.Security.Cryptography;
using WebPush;

namespace Fantasoccer.Business;

public static class PushSubscriptionValidator
{
    public static bool IsValid(PushSubscription? subscription)
    {
        if (subscription == null || string.IsNullOrWhiteSpace(subscription.P256DH) || string.IsNullOrWhiteSpace(subscription.Auth) ||
            subscription.Endpoint?.Length > 4096 ||
            !Uri.TryCreate(subscription.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != "https" || !endpoint.IsDefaultPort || !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Fragment) || !IsBrowserPushProvider(endpoint.DnsSafeHost)) return false;
        try
        {
            static byte[] Decode(string value)
            {
                if (value.Length > 128) throw new FormatException();
                return Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));
            }
            var key = Decode(subscription.P256DH);
            if (key.Length != 65 || key[0] != 4 || Decode(subscription.Auth).Length != 16) return false;
            using var publicKey = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = key[1..33], Y = key[33..65] }
            });
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or CryptographicException) { return false; }
    }

    // Endpoints originate with the browser, not with arbitrary user-hosted URLs.
    // Restrict the network destination as well as disabling HTTP redirects, so
    // registration cannot turn the dispatcher into an internal-network proxy.
    private static bool IsBrowserPushProvider(string host)
        => host.Equals("fcm.googleapis.com", StringComparison.OrdinalIgnoreCase) ||
           host.Equals("android.googleapis.com", StringComparison.OrdinalIgnoreCase) ||
           host.Equals("push.services.mozilla.com", StringComparison.OrdinalIgnoreCase) ||
           host.EndsWith(".push.services.mozilla.com", StringComparison.OrdinalIgnoreCase) ||
           host.Equals("web.push.apple.com", StringComparison.OrdinalIgnoreCase) ||
           host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase);
}
