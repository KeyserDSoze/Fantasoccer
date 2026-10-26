namespace Fantasoccer;

// Configuration only. PrivateKey must come from a private env/secret store and
// is never part of a controller, notification payload or diagnostic response.
public sealed class PushTransportOptions
{
    public bool Enabled { get; set; }
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    public string Subject { get; set; } = "";
    public bool HasValidConfiguration()
    {
        try
        {
            static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));
            var key = Decode(PrivateKey); var point = Decode(PublicKey);
            if (key.Length != 32 || point.Length != 65 || point[0] != 4 || !Uri.TryCreate(Subject, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("mailto" or "https") || (uri.Scheme == "https" && !string.IsNullOrEmpty(uri.UserInfo)) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || Subject.Any(char.IsControl)) return false;
            using var ec = System.Security.Cryptography.ECDsa.Create(new System.Security.Cryptography.ECParameters { Curve = System.Security.Cryptography.ECCurve.NamedCurves.nistP256, D = key });
            var publicPoint = ec.ExportParameters(false).Q;
            return point.AsSpan(1, 32).SequenceEqual(publicPoint.X) && point.AsSpan(33, 32).SequenceEqual(publicPoint.Y);
        }
        catch { return false; }
    }
}
