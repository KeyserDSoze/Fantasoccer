using System.Security.Cryptography;
using System.Text;

namespace Fantasoccer.Business;

public static class PushEndpointIdentity
{
    public static string Hash(string endpoint)
    {
        var value = endpoint;
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo))
            value = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped).ToLowerInvariant() + uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
