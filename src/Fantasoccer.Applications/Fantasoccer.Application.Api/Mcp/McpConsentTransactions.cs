using System.Security.Cryptography;
using System.Text.Json;

namespace Fantasoccer.Application.Api.Mcp;

// Shared by replicas and survives restarts. Each transition is a versioned CAS.
// Store only the account email, not bearer tokens or serialized claims/privileges.
public sealed class McpConsentTransactions(IOAuthDocumentStorage storage)
{
    public sealed class Transaction
    {
        public string ReturnUrl { get; set; } = "";
        public string ClientName { get; set; } = "";
        public string? Email { get; set; }
        public string Status { get; set; } = "pending";
        public DateTimeOffset ExpiresUtc { get; set; }
    }
    private static string Key(string id) => "consent-" + id;
    private static bool ValidId(string id) => id.Length == 64 && id.All(char.IsAsciiHexDigit);

    public async Task<string> CreateAsync(string returnUrl, string clientName, CancellationToken cancellationToken)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var transaction = new Transaction { ReturnUrl = returnUrl, ClientName = clientName, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10) };
        if (!await storage.WriteAsync(new OAuthDocument { Id = Key(id), Kind = "consent", Version = Guid.NewGuid().ToString("N"),
            Payload = JsonSerializer.Serialize(transaction), DeleteAfterUtc = transaction.ExpiresUtc.AddDays(1) }, null, cancellationToken)) throw new InvalidOperationException("Unable to create consent transaction.");
        return id;
    }

    public async Task<Transaction?> GetAsync(string id, CancellationToken cancellationToken)
    {
        if (!ValidId(id)) return null;
        var document = await storage.GetAsync(Key(id), cancellationToken);
        var transaction = document?.Payload == null ? null : JsonSerializer.Deserialize<Transaction>(document.Payload);
        return transaction?.ExpiresUtc > DateTimeOffset.UtcNow && transaction.Status != "redeemed" ? transaction : null;
    }

    public Task<Transaction?> ApproveAsync(string id, string email, CancellationToken cancellationToken)
        => TransitionAsync(id, "pending", "approved", email, cancellationToken);
    public Task<Transaction?> ConsumeAsync(string id, CancellationToken cancellationToken)
        => TransitionAsync(id, "approved", "completed", null, cancellationToken);
    public Task<Transaction?> RedeemAsync(string id, CancellationToken cancellationToken)
        => TransitionAsync(id, "completed", "redeemed", null, cancellationToken);
    public Task<Transaction?> DenyAsync(string id, CancellationToken cancellationToken)
        => TransitionAsync(id, "pending", "redeemed", null, cancellationToken);

    private async Task<Transaction?> TransitionAsync(string id, string from, string to, string? email, CancellationToken cancellationToken)
    {
        if (!ValidId(id)) return null;
        var document = await storage.GetAsync(Key(id), cancellationToken);
        if (document?.Payload == null) return null;
        var transaction = JsonSerializer.Deserialize<Transaction>(document.Payload)!;
        if (transaction.ExpiresUtc <= DateTimeOffset.UtcNow || transaction.Status != from) return null;
        if (email != null) transaction.Email = email;
        transaction.Status = to;
        var previousVersion = document.Version;
        document.Version = Guid.NewGuid().ToString("N");
        document.Payload = JsonSerializer.Serialize(transaction);
        return await storage.WriteAsync(document, previousVersion, cancellationToken) ? transaction : null;
    }
}
