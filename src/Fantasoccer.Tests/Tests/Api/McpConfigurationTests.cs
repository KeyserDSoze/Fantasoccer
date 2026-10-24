using Fantasoccer.Application.Api.Mcp;
using Microsoft.AspNetCore.Builder;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace Fantasoccer.Tests.Tests.Api;

public sealed class McpConfigurationTests
{
    [Fact]
    public void Disabled_feature_does_not_register_OAuth_or_require_secrets()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Mcp:Enabled"] = "false";
        builder.AddFantasoccerMcp();
        Assert.DoesNotContain(builder.Services, x => x.ServiceType == typeof(IOpenIddictApplicationManager));
        Assert.Contains(builder.Services, x => x.ImplementationInstance is McpOptions { Enabled: false });
    }

    [Fact]
    public void Enabled_feature_fails_closed_when_configuration_is_incomplete()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Mcp:Enabled"] = "true";
        builder.Configuration["Mcp:Issuer"] = "http://insecure.example";
        Assert.Throws<InvalidOperationException>(() => builder.AddFantasoccerMcp());
    }

    [Fact]
    public void Production_never_accepts_volatile_OAuth_storage()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration["Mcp:Enabled"] = "true";
        builder.Configuration["Integrations:Infrastructure"] = "InMemory";
        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddFantasoccerMcp());
        Assert.Contains("volatile", exception.Message);
    }

    [Fact]
    public void Duplicate_client_configuration_is_rejected_before_seeding()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration["Mcp:Enabled"] = "true";
        builder.Configuration["Mcp:Clients:0:ClientId"] = "duplicate";
        builder.Configuration["Mcp:Clients:1:ClientId"] = "duplicate";
        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddFantasoccerMcp());
        Assert.Contains("unique", exception.Message);
    }

    [Fact]
    public void Dynamic_registration_requires_no_preconfigured_clients()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration["Mcp:Enabled"] = "true";
        builder.Configuration["Mcp:Issuer"] = "https://api.example";
        builder.Configuration["Mcp:FrontendUrl"] = "https://frontend.example/";
        builder.AddFantasoccerMcp();
        Assert.Contains(builder.Services, x => x.ImplementationInstance is McpOptions { Clients.Count: 0 });
        Assert.Contains(builder.Services, x => x.ServiceType == typeof(IOpenIddictApplicationManager));
    }

    [Fact]
    public void Production_accepts_GitHub_Base64_PFX_and_an_existing_persistent_key_path()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=MCP configuration test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration["Mcp:Enabled"] = "true";
        builder.Configuration["Mcp:Issuer"] = "https://api.example";
        builder.Configuration["Mcp:FrontendUrl"] = "https://frontend.example/";
        builder.Configuration["Mcp:CertificateBase64"] = Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, "test-password"));
        builder.Configuration["Mcp:CertificatePassword"] = "test-password";
        builder.Configuration["Mcp:DataProtectionKeyPath"] = Path.GetTempPath();
        builder.AddFantasoccerMcp(); // Registration never writes/migrates the key ring.
        Assert.Contains(builder.Services, x => x.ImplementationInstance is McpOptions { Clients.Count: 0 });
        builder.Configuration["Mcp:CertificatePath"] = "another.pfx";
        Assert.Throws<InvalidOperationException>(() => builder.AddFantasoccerMcp());
    }

    [Fact]
    public void Enabling_and_disabling_MCP_preserves_sessions_with_the_existing_key_ring_and_discriminator()
    {
        var directory = Directory.CreateTempSubdirectory("fantasoccer-keyring-compat-");
        try
        {
            var legacy = new ServiceCollection();
            legacy.AddLogging();
            legacy.AddDataProtection().SetApplicationName("existing-application")
                .PersistKeysToFileSystem(directory);
            string existingTicket;
            using (var provider = legacy.BuildServiceProvider())
                existingTicket = provider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("existing-frontend-ticket").Protect("existing-session");
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=MCP compatibility test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Services.AddDataProtection().SetApplicationName("existing-application");
            builder.Configuration["Mcp:Enabled"] = "true";
            builder.Configuration["Mcp:Issuer"] = "https://api.example";
            builder.Configuration["Mcp:FrontendUrl"] = "https://frontend.example/";
            builder.Configuration["Mcp:CertificateBase64"] = Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, "password"));
            builder.Configuration["Mcp:CertificatePassword"] = "password";
            builder.Configuration["Mcp:DataProtectionKeyPath"] = directory.FullName;
            builder.AddFantasoccerMcp();
            using var updated = builder.Services.BuildServiceProvider();
            var protector = updated.GetRequiredService<IDataProtectionProvider>().CreateProtector("existing-frontend-ticket");
            Assert.Equal("existing-session", protector.Unprotect(existingTicket));
            updated.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));
            Assert.Contains(directory.GetFiles("*.xml"), x => File.ReadAllText(x.FullName).Contains("encryptedSecret"));
            Assert.Equal("existing-session", protector.Unprotect(existingTicket));
            var newerTicket = protector.Protect("session-after-MCP-release");
            var disabled = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            disabled.Services.AddDataProtection().SetApplicationName("existing-application");
            disabled.Configuration["Mcp:Enabled"] = "false";
            disabled.Configuration["Mcp:CertificateBase64"] = builder.Configuration["Mcp:CertificateBase64"];
            disabled.Configuration["Mcp:CertificatePassword"] = "password";
            disabled.Configuration["Mcp:DataProtectionKeyPath"] = directory.FullName;
            disabled.AddFantasoccerMcp();
            Assert.DoesNotContain(disabled.Services, x => x.ServiceType == typeof(IOpenIddictApplicationManager));
            using var disabledProvider = disabled.Services.BuildServiceProvider();
            var disabledProtector = disabledProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("existing-frontend-ticket");
            Assert.Equal("existing-session", disabledProtector.Unprotect(existingTicket));
            Assert.Equal("session-after-MCP-release", disabledProtector.Unprotect(newerTicket));
        }
        finally { directory.Delete(recursive: true); }
    }
}
