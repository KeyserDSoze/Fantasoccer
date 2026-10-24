using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Fantasoccer.Application.Api.Mcp.Storage;
using Fantasoccer.InMemory;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Fantasoccer.Application.Api.Mcp;

public static class McpRegistration
{
    public static void AddFantasoccerMcp(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("Mcp").Get<McpOptions>() ?? new();
        builder.Services.AddSingleton(settings);
        // Public authorization anchors must remain protected even before MCP is enabled.
        builder.Services.AddSingleton<McpEndpointCatalog>();
        builder.Services.AddScoped<McpAccessGuard>();
        if (!settings.Enabled)
        {
            // Disabling OAuth must not remove the key store/certificate needed
            // to read frontend tickets protected after the MCP release.
            ConfigureDataProtection(builder, settings);
            return;
        }

        var development = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
        if (!development && builder.Configuration["Integrations:Infrastructure"] == "InMemory")
            throw new InvalidOperationException("Production MCP cannot use volatile in-memory OAuth storage.");
        if (settings.Clients.Select(x => x.ClientId).Distinct(StringComparer.Ordinal).Count() != settings.Clients.Count)
            throw new InvalidOperationException("MCP client IDs must be unique.");
        if (!Uri.TryCreate(settings.Issuer, UriKind.Absolute, out var issuer) || issuer.Scheme != "https" ||
            issuer.AbsolutePath != "/" || !string.IsNullOrEmpty(issuer.Query) || !string.IsNullOrEmpty(issuer.Fragment) ||
            !Uri.TryCreate(settings.FrontendUrl, UriKind.Absolute, out var frontend) ||
            !string.IsNullOrEmpty(issuer.UserInfo) ||
            (frontend.Scheme != "https" && !(development && frontend.IsLoopback)))
            throw new InvalidOperationException("MCP requires an HTTPS root Issuer and FrontendUrl.");
        if (!development && ((string.IsNullOrWhiteSpace(settings.CertificatePath) && string.IsNullOrWhiteSpace(settings.CertificateBase64)) ||
            (string.IsNullOrWhiteSpace(settings.DataProtectionKeyPath) && string.IsNullOrWhiteSpace(settings.DataProtectionBlobName))))
            throw new InvalidOperationException("Production MCP requires a certificate and persistent Data Protection keys (Blob or an existing shared key path).");
        var certificate = ConfigureDataProtection(builder, settings);

        if (builder.Configuration["Integrations:Infrastructure"] == "InMemory")
            builder.Services.AddSingleton<IOAuthDocumentStorage, InMemoryOAuthDocumentStorage>();
        // Rystem relies on ASP.NET's single-scheme default. Preserve it when adding more schemes.
        builder.Services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme ??= BearerTokenDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme ??= BearerTokenDefaults.AuthenticationScheme;
            options.DefaultSignInScheme ??= BearerTokenDefaults.AuthenticationScheme;
        });
        builder.Services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, McpBearerHandler>(McpOptions.Scheme, _ => { })
            .AddCookie(McpOptions.ConsentScheme, options =>
            {
                options.Cookie.Name = "__Host-Fantasoccer-McpConsent";
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(2);
            });
        builder.Services.AddAuthorization(options => options.AddPolicy("mcp", policy =>
            policy.AddAuthenticationSchemes(McpOptions.Scheme).RequireAuthenticatedUser().RequireClaim(ClaimTypes.Email)));
        builder.Services.AddScoped<McpTokenValidator>();
        builder.Services.PostConfigure<BearerTokenOptions>(BearerTokenDefaults.AuthenticationScheme, options =>
        {
            var previous = options.Events.OnMessageReceived;
            options.Events.OnMessageReceived = async context =>
            {
                if (previous != null) await previous(context);
                if (context.Result != null) return;
                var header = context.Request.Headers.Authorization.ToString();
                if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return;
                var ticket = options.BearerTokenProtector.Unprotect(header[7..]);
                if (ticket?.Principal.HasClaim(x => x.Type == McpOptions.ResourceClaim) != true) return;
                var principal = await context.HttpContext.RequestServices.GetRequiredService<McpTokenValidator>()
                    .ValidateAsync(ticket, context.HttpContext.RequestAborted);
                if (principal == null) context.Fail("Invalid or revoked MCP connection.");
                else
                {
                    context.Principal = principal;
                    context.Properties = ticket.Properties;
                    context.Success();
                }
            };
        });

        builder.Services.AddScoped<IOpenIddictApplicationStore<OAuthApplication>, OAuthStore<OAuthApplication>>();
        builder.Services.AddScoped<IOpenIddictAuthorizationStore<OAuthAuthorization>, OAuthStore<OAuthAuthorization>>();
        builder.Services.AddScoped<IOpenIddictScopeStore<OAuthScope>, OAuthStore<OAuthScope>>();
        builder.Services.AddScoped<IOpenIddictTokenStore<OAuthToken>, OAuthStore<OAuthToken>>();
        builder.Services.AddOpenIddict()
            .AddCore(options =>
            {
                options.DisableEntityCaching();
                options.SetDefaultApplicationEntity<OAuthApplication>();
                options.SetDefaultAuthorizationEntity<OAuthAuthorization>();
                options.SetDefaultScopeEntity<OAuthScope>();
                options.SetDefaultTokenEntity<OAuthToken>();
            })
            .AddServer(options =>
            {
                options.SetIssuer(issuer);
                options.SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .SetRevocationEndpointUris("/connect/revoke");
                options.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow().RequireProofKeyForCodeExchange();
                options.RegisterScopes(McpOptions.Scope, Scopes.OfflineAccess);
                options.RegisterResources(settings.Resource);
                options.Configure(configuration =>
                {
                    configuration.CodeChallengeMethods.Clear();
                    configuration.CodeChallengeMethods.Add(CodeChallengeMethods.Sha256);
                    configuration.Scopes.Clear();
                    configuration.Scopes.UnionWith([McpOptions.Scope, Scopes.OfflineAccess]);
                });
                options.SetAccessTokenLifetime(TimeSpan.FromHours(24));
                options.AddEventHandler<OpenIddictServerEvents.ApplyConfigurationResponseContext>(handler => handler
                    .UseInlineHandler(context =>
                    {
                        context.Response.SetParameter("registration_endpoint", settings.Issuer.TrimEnd('/') + "/connect/register");
                        return default;
                    }));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));
                options.SetRefreshTokenReuseLeeway(TimeSpan.Zero);
                options.RemoveEventHandler(OpenIddictServerHandlers.RedeemTokenEntry.Descriptor);
                options.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
                    .SetOrder(OpenIddictServerHandlers.RedeemTokenEntry.Descriptor.Order)
                    .UseScopedHandler<McpRedeemTokenHandler>());
                if (development && certificate == null)
                    options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
                else
                {
                    options.AddSigningCertificate(certificate!).AddEncryptionCertificate(certificate!);
                }

                // Keep ASP.NET Core's existing opaque token format rather than migrate the frontend.
                // OpenIddict still manages client validation, PKCE, one-time codes, refresh and revocation.
                options.AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(handler => handler
                    .SetOrder(OpenIddictServerHandlers.Protection.GenerateIdentityModelToken.Descriptor.Order - 500)
                    .UseInlineHandler(context =>
                    {
                        if (context.TokenType != TokenTypeIdentifiers.AccessToken) return default;
                        var principal = context.Principal.Clone();
                        principal.SetTokenType(TokenTypeIdentifiers.AccessToken);
                        principal.SetAudiences(settings.Resource);
                        principal.SetClaim(Claims.Issuer, settings.Issuer.TrimEnd('/'));
                        principal.SetClaim(McpOptions.ResourceClaim, settings.Resource);
                        var properties = new AuthenticationProperties { ExpiresUtc = principal.GetExpirationDate() };
                        var protector = context.Transaction.GetHttpRequest()!.HttpContext.RequestServices
                            .GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
                            .Get(BearerTokenDefaults.AuthenticationScheme).BearerTokenProtector;
                        context.Token = protector.Protect(new AuthenticationTicket(principal, properties, BearerTokenDefaults.AuthenticationScheme));
                        return default;
                    }));
                options.AddEventHandler<OpenIddictServerEvents.ValidateTokenContext>(handler => handler
                    .SetOrder(OpenIddictServerHandlers.Protection.ValidateIdentityModelToken.Descriptor.Order - 500)
                    .UseInlineHandler(context =>
                    {
                        var protector = context.Transaction.GetHttpRequest()!.HttpContext.RequestServices
                            .GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
                            .Get(BearerTokenDefaults.AuthenticationScheme).BearerTokenProtector;
                        var ticket = protector.Unprotect(context.Token);
                        if (ticket?.Principal.FindFirstValue(McpOptions.ResourceClaim) == settings.Resource &&
                            ticket.Properties.ExpiresUtc > DateTimeOffset.UtcNow &&
                            ticket.Principal.GetClaim(Claims.Issuer) == settings.Issuer.TrimEnd('/'))
                        {
                            if (context.ValidTokenTypes.Count > 0 && !context.ValidTokenTypes.Contains(TokenTypeIdentifiers.AccessToken))
                            {
                                context.Reject(Errors.InvalidToken, "An MCP access token cannot be used for this grant.");
                                return default;
                            }
                            context.Principal = ticket.Principal;
                        }
                        return default;
                    }));
                options.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough();
            });

        builder.Services.AddSingleton<McpConsentTransactions>();
        builder.Services.AddScoped<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider, McpCorsPolicyProvider>();
        if (!builder.Environment.IsEnvironment("Testing")) builder.Services.AddHostedService<OAuthRetentionService>();
        builder.Services.AddSingleton<McpApiFacade>();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Runs before authentication, including OAuth errors rejected by OpenIddict itself.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/connect")
                    ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("other"));
            options.AddPolicy("mcp", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("mcp-oauth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("mcp-registration", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithTools<McpAccountTools>()
            .WithTools<McpCatalogTools>()
            .WithListToolsHandler((request, cancellationToken) =>
            {
                var services = request.Services!;
                var tools = settings.PublishAllTools
                    ? services.GetRequiredService<McpEndpointCatalog>().Operations.Values.Select(x => x.Tool).ToList()
                    : [];
                return ValueTask.FromResult(new ListToolsResult { Tools = tools });
            })
            .WithCallToolHandler(async (request, cancellationToken) =>
                await request.Services!.GetRequiredService<McpApiFacade>().CallAsync(request.Params!, cancellationToken));
    }

    private static X509Certificate2? ConfigureDataProtection(WebApplicationBuilder builder, McpOptions settings)
    {
        if (!string.IsNullOrEmpty(settings.DataProtectionKeyPath) && !string.IsNullOrEmpty(settings.DataProtectionBlobName))
            throw new InvalidOperationException("Choose one Data Protection key store, not both.");
        if (!string.IsNullOrWhiteSpace(settings.CertificatePath) && !string.IsNullOrWhiteSpace(settings.CertificateBase64))
            throw new InvalidOperationException("Choose one OAuth certificate source, not both.");
        // Load the GitHub secret directly: no PFX in the deployment artifact or
        // OS-specific certificate directory. Keep this configuration when disabled.
        var certificate = !string.IsNullOrWhiteSpace(settings.CertificateBase64)
            ? X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(settings.CertificateBase64), settings.CertificatePassword, X509KeyStorageFlags.EphemeralKeySet)
            : !string.IsNullOrWhiteSpace(settings.CertificatePath)
                ? X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath, settings.CertificatePassword, X509KeyStorageFlags.EphemeralKeySet)
                : null;
        var protection = builder.Services.AddDataProtection();
        if (!string.IsNullOrWhiteSpace(settings.DataProtectionBlobName))
        {
            var connection = builder.Configuration["ConnectionStrings:Storage"] ?? throw new InvalidOperationException("Missing existing storage connection.");
            protection.PersistKeysToAzureBlobStorage(new BlobContainerClient(connection, "mcp-keys").GetBlobClient(settings.DataProtectionBlobName));
        }
        if (!string.IsNullOrWhiteSpace(settings.DataProtectionKeyPath))
            protection.PersistKeysToFileSystem(new DirectoryInfo(settings.DataProtectionKeyPath));
        if (certificate != null) protection.ProtectKeysWithCertificate(certificate);
        return certificate;
    }

    public static async Task MapFantasoccerMcpAsync(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<McpOptions>();
        if (!settings.Enabled) return;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            foreach (var client in settings.Clients)
            {
                if (string.IsNullOrWhiteSpace(client.ClientId) || client.RedirectUris.Count == 0 ||
                    client.RedirectUris.Any(x => !McpClientRegistration.IsValidRedirect(x)))
                    throw new InvalidOperationException("MCP clients require an ID and exact HTTPS or loopback HTTP redirect URIs.");
                var descriptor = new OpenIddictApplicationDescriptor
                {
                    ClientId = client.ClientId,
                    ClientSecret = client.ClientSecret,
                    DisplayName = client.DisplayName,
                    ClientType = string.IsNullOrEmpty(client.ClientSecret) ? ClientTypes.Public : ClientTypes.Confidential,
                    ConsentType = ConsentTypes.Explicit
                };
                // Stable IDs prevent two cold-starting replicas from creating duplicate clients.
                foreach (var redirect in client.RedirectUris) descriptor.RedirectUris.Add(new Uri(redirect));
                descriptor.Permissions.UnionWith([
                    Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.Revocation,
                    Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code, Permissions.Prefixes.Scope + McpOptions.Scope,
                    Permissions.Prefixes.Resource + settings.Resource]);
                descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
                var existing = await manager.FindByClientIdAsync(client.ClientId);
                if (existing == null)
                {
                    var entity = new OAuthApplication { Id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(client.ClientId))) };
                    await manager.PopulateAsync(entity, descriptor, default);
                    try { await manager.CreateAsync(entity, default); }
                    catch (OpenIddictExceptions.ConcurrencyException)
                    {
                        if (await manager.FindByClientIdAsync(client.ClientId) == null) throw;
                    }
                }
                else
                {
                    for (var attempt = 0; ; attempt++)
                    {
                        try { await manager.UpdateAsync(existing, descriptor); break; }
                        catch (OpenIddictExceptions.ConcurrencyException) when (attempt < 4)
                        {
                            existing = await manager.FindByClientIdAsync(client.ClientId) ?? throw new InvalidOperationException("OAuth client disappeared during startup.");
                        }
                    }
                }
            }
        }
        app.MapMcp("/mcp").RequireAuthorization("mcp").RequireRateLimiting("mcp");
        app.MapMcpOAuthEndpoints();
        app.Services.GetRequiredService<McpApiFacade>().Initialize(app);
    }
}
