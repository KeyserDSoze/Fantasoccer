using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

var directory = Environment.GetEnvironmentVariable("LOCAL_CERTIFICATE_DIRECTORY") ?? "/certificates";
var password = Environment.GetEnvironmentVariable("LOCAL_HTTPS_CERT_PASSWORD");
if (!Path.IsPathFullyQualified(directory) || string.IsNullOrWhiteSpace(password))
{
    Console.Error.WriteLine("An absolute certificate directory and LOCAL_HTTPS_CERT_PASSWORD are required.");
    return 1;
}
Directory.CreateDirectory(directory);
if (new[] { "localhost.pfx", "localhost.pem", "localhost.cer" }.Any(file => File.Exists(Path.Combine(directory, file)) || Directory.Exists(Path.Combine(directory, file))))
{
    Console.Error.WriteLine("Certificate files already exist. They were NOT overwritten; rotation requires preserving existing decryption keys.");
    return 1;
}
using var key = RSA.Create(3072);
var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var names = new SubjectAlternativeNameBuilder();
names.AddDnsName("localhost"); names.AddIpAddress(IPAddress.Loopback); names.AddIpAddress(IPAddress.IPv6Loopback);
request.CertificateExtensions.Add(names.Build());
request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
File.WriteAllBytes(Path.Combine(directory, "localhost.pfx"), certificate.Export(X509ContentType.Pfx, password));
File.WriteAllText(Path.Combine(directory, "localhost.pem"), certificate.ExportCertificatePem());
File.WriteAllBytes(Path.Combine(directory, "localhost.cer"), certificate.Export(X509ContentType.Cert));
// The PFX is encrypted by the private env-file password. File-level read access
// lets the non-root API user read a directly mounted PFX; protect the host directory.
if (!OperatingSystem.IsWindows())
    foreach (var file in new[] { "localhost.pfx", "localhost.pem", "localhost.cer" }) File.SetUnixFileMode(Path.Combine(directory, file), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
Console.WriteLine("Local certificate generated. No host trust store was modified. Trust localhost.cer explicitly on your own Windows account before using HTTPS/OAuth.");
return 0;
