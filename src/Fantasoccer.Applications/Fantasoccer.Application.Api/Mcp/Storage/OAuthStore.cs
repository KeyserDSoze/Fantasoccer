using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Fantasoccer.Application.Api.Mcp.Storage;

// One typed adapter per entity registered below. Managers retain OpenIddict's
// validation/hashing semantics; the storage boundary supplies optimistic concurrency.
// Disable OpenIddict entity caches: revocation/demotion must work across API instances.
public sealed class OAuthStore<T>(IOAuthDocumentStorage storage) :
    IOpenIddictApplicationStore<T>, IOpenIddictAuthorizationStore<T>,
    IOpenIddictScopeStore<T>, IOpenIddictTokenStore<T> where T : OAuthEntity, new()
{
    private static readonly string Kind = typeof(T).Name;
    private static string Key(string id) => Kind + "-" + id;

    public async ValueTask<T?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        var document = await storage.GetAsync(Key(identifier), cancellationToken);
        return document?.Payload == null ? null : Deserialize(document);
    }

    private static T Deserialize(OAuthDocument document)
    {
        var entity = JsonSerializer.Deserialize<T>(document.Payload!)!;
        entity.Version = document.Version;
        return entity;
    }

    private async IAsyncEnumerable<T> AllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var document in storage.ListAsync(Kind, cancellationToken)) yield return Deserialize(document);
    }

    private async ValueTask SaveAsync(T entity, bool create, bool delete, CancellationToken cancellationToken)
    {
        var version = Guid.NewGuid().ToString("N");
        var document = new OAuthDocument { Id = Key(entity.Id), Kind = Kind, Version = version,
            Payload = delete ? null : JsonSerializer.Serialize(entity),
            DeleteAfterUtc = delete || entity.Status is Statuses.Revoked or Statuses.Rejected
                ? DateTimeOffset.UtcNow.AddDays(7)
                : typeof(T) == typeof(OAuthToken) ? entity.ExpirationDate?.AddDays(7) : null };
        if (!await storage.WriteAsync(document, create ? null : entity.Version ?? throw new InvalidOperationException("Missing entity version."), cancellationToken))
            throw new OpenIddictExceptions.ConcurrencyException("OAuth document was concurrently modified.");
        entity.Version = version;
    }

    public ValueTask CreateAsync(T entity, CancellationToken cancellationToken) => SaveAsync(entity, true, false, cancellationToken);
    public ValueTask UpdateAsync(T entity, CancellationToken cancellationToken) => SaveAsync(entity, false, false, cancellationToken);
    // A versioned tombstone avoids delete/recreate (ABA) and listing/deletion races.
    public ValueTask DeleteAsync(T entity, CancellationToken cancellationToken) => SaveAsync(entity, false, true, cancellationToken);
    public ValueTask<T> InstantiateAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(new T()); }

    private async Task<IQueryable<T>> QueryAsync(CancellationToken cancellationToken)
    {
        var entities = new List<T>();
        await foreach (var entity in AllAsync(cancellationToken)) entities.Add(entity);
        return entities.AsQueryable();
    }
    public async ValueTask<long> CountAsync(CancellationToken cancellationToken) => (await QueryAsync(cancellationToken)).LongCount();
    public async ValueTask<long> CountAsync<TResult>(Func<IQueryable<T>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        => query(await QueryAsync(cancellationToken)).LongCount();
    public async ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<T>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
        => query(await QueryAsync(cancellationToken), state).FirstOrDefault();
    public async IAsyncEnumerable<T> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = (await QueryAsync(cancellationToken)).OrderBy(x => x.Id).Skip(offset ?? 0);
        foreach (var entity in count.HasValue ? query.Take(count.Value) : query) { cancellationToken.ThrowIfCancellationRequested(); yield return entity; }
    }
    public async IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<T>, TState, IQueryable<TResult>> query,
        TState state, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var item in query(await QueryAsync(cancellationToken), state)) { cancellationToken.ThrowIfCancellationRequested(); yield return item; }
    }
    private async IAsyncEnumerable<T> FilterAsync(Func<T, bool> predicate, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var entity in AllAsync(cancellationToken)) if (predicate(entity)) yield return entity;
    }
    private async ValueTask<T?> FirstAsync(Func<T, bool> predicate, CancellationToken cancellationToken)
    {
        await foreach (var entity in FilterAsync(predicate, cancellationToken)) return entity;
        return null;
    }
    private static bool Matches(T entity, string? subject, string? client, string? status, string? type)
        => (subject == null || entity.Subject == subject) && (client == null || entity.ApplicationId == client) &&
           (status == null || entity.Status == status) && (type == null || entity.Type == type);

    public IAsyncEnumerable<T> FindAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken)
        => FilterAsync(x => Matches(x, subject, client, status, type), cancellationToken);
    public IAsyncEnumerable<T> FindAsync(string? subject, string? client, string? status, string? type, ImmutableArray<string>? scopes, CancellationToken cancellationToken)
        => FilterAsync(x => Matches(x, subject, client, status, type) && (!scopes.HasValue || scopes.Value.All(x.Scopes.Contains)), cancellationToken);
    public IAsyncEnumerable<T> FindByApplicationIdAsync(string identifier, CancellationToken cancellationToken) => FilterAsync(x => x.ApplicationId == identifier, cancellationToken);
    public IAsyncEnumerable<T> FindByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken) => FilterAsync(x => x.AuthorizationId == identifier, cancellationToken);
    public IAsyncEnumerable<T> FindBySubjectAsync(string subject, CancellationToken cancellationToken) => FilterAsync(x => x.Subject == subject, cancellationToken);
    public ValueTask<T?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken) => FirstAsync(x => x.ClientId == identifier, cancellationToken);
    public ValueTask<T?> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken) => FirstAsync(x => x.ReferenceId == identifier, cancellationToken);
    public ValueTask<T?> FindByNameAsync(string name, CancellationToken cancellationToken) => FirstAsync(x => x.Name == name, cancellationToken);
    public IAsyncEnumerable<T> FindByNamesAsync(ImmutableArray<string> names, CancellationToken cancellationToken) => FilterAsync(x => names.Contains(x.Name!), cancellationToken);
    public IAsyncEnumerable<T> FindByResourceAsync(string resource, CancellationToken cancellationToken) => FilterAsync(x => x.Resources.Contains(resource), cancellationToken);
    public IAsyncEnumerable<T> FindByRedirectUriAsync(string uri, CancellationToken cancellationToken) => FilterAsync(x => x.RedirectUris.Contains(uri), cancellationToken);
    public IAsyncEnumerable<T> FindByPostLogoutRedirectUriAsync(string uri, CancellationToken cancellationToken) => FilterAsync(x => x.PostLogoutRedirectUris.Contains(uri), cancellationToken);

    public ValueTask<long> RevokeAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken)
        => RevokeWhereAsync(x => Matches(x, subject, client, status, type), cancellationToken);
    public ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken = default) => RevokeWhereAsync(x => x.ApplicationId == identifier, cancellationToken);
    public ValueTask<long> RevokeByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken) => RevokeWhereAsync(x => x.AuthorizationId == identifier, cancellationToken);
    public ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken = default) => RevokeWhereAsync(x => x.Subject == subject, cancellationToken);
    private async ValueTask<long> RevokeWhereAsync(Func<T, bool> predicate, CancellationToken cancellationToken)
    {
        long count = 0;
        await foreach (var entity in FilterAsync(predicate, cancellationToken))
        {
            // Retry against a fresh snapshot, never overwrite a concurrent redemption/revocation.
            var current = entity;
            for (var attempt = 0; current != null && attempt < 5; attempt++)
            {
                if (!predicate(current) || current.Status == Statuses.Revoked) break;
                current.Status = Statuses.Revoked;
                try { await UpdateAsync(current, cancellationToken); count++; break; }
                catch (OpenIddictExceptions.ConcurrencyException) when (attempt < 4) { current = await FindByIdAsync(current.Id, cancellationToken); }
            }
        }
        return count;
    }
    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        long count = 0;
        // Do not prune valid grants: token validation relies on them. Invalid old
        // records may be tombstoned; physical retention is an operational policy.
        await foreach (var entity in FilterAsync(x => x.CreationDate < threshold &&
            (x.Status != Statuses.Valid || (typeof(T) == typeof(OAuthToken) && x.ExpirationDate < DateTimeOffset.UtcNow)), cancellationToken))
        {
            try { await DeleteAsync(entity, cancellationToken); count++; }
            catch (OpenIddictExceptions.ConcurrencyException) { }
        }
        return count;
    }

    public ValueTask<string?> GetIdAsync(T e, CancellationToken ct) => ValueTask.FromResult<string?>(e.Id);
    public ValueTask<string?> GetApplicationIdAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.ApplicationId);
    public ValueTask<string?> GetAuthorizationIdAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.AuthorizationId);
    public ValueTask<string?> GetApplicationTypeAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.ApplicationType);
    public ValueTask<string?> GetClientIdAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.ClientId);
    public ValueTask<string?> GetClientSecretAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.ClientSecret);
    public ValueTask<string?> GetClientTypeAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.ClientType);
    public ValueTask<string?> GetConsentTypeAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.ConsentType);
    public ValueTask<string?> GetDisplayNameAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.DisplayName);
    public ValueTask<string?> GetDescriptionAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Description);
    public ValueTask<string?> GetNameAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Name);
    public ValueTask<string?> GetSubjectAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Subject);
    public ValueTask<string?> GetStatusAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Status);
    public ValueTask<string?> GetTypeAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Type);
    public ValueTask<string?> GetPayloadAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Payload);
    public ValueTask<string?> GetReferenceIdAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.ReferenceId);
    public ValueTask<DateTimeOffset?> GetCreationDateAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.CreationDate);
    public ValueTask<DateTimeOffset?> GetExpirationDateAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.ExpirationDate);
    public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.RedemptionDate);
    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Permissions);
    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.PostLogoutRedirectUris);
    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.RedirectUris);
    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Requirements);
    public ValueTask<ImmutableArray<string>> GetResourcesAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Resources);
    public ValueTask<ImmutableArray<string>> GetScopesAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Scopes);
    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Settings);
    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Properties);
    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.DisplayNames.ToImmutableDictionary(x => CultureInfo.GetCultureInfo(x.Key), x => x.Value));
    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.Descriptions.ToImmutableDictionary(x => CultureInfo.GetCultureInfo(x.Key), x => x.Value));
    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(T e, CancellationToken ct) => ValueTask.FromResult(e.JsonWebKeySet == null ? null : new JsonWebKeySet(e.JsonWebKeySet));

    public ValueTask SetApplicationIdAsync(T e, string? v, CancellationToken ct) { e.ApplicationId = v; return default; }
    public ValueTask SetAuthorizationIdAsync(T e, string? v, CancellationToken ct) { e.AuthorizationId = v; return default; }
    public ValueTask SetApplicationTypeAsync(T e, string? v, CancellationToken ct) { e.ApplicationType = v; return default; }
    public ValueTask SetClientIdAsync(T e, string? v, CancellationToken ct) { e.ClientId = v; return default; }
    public ValueTask SetClientSecretAsync(T e, string? v, CancellationToken ct) { e.ClientSecret = v; return default; }
    public ValueTask SetClientTypeAsync(T e, string? v, CancellationToken ct) { e.ClientType = v; return default; }
    public ValueTask SetConsentTypeAsync(T e, string? v, CancellationToken ct) { e.ConsentType = v; return default; }
    public ValueTask SetDisplayNameAsync(T e, string? v, CancellationToken ct) { e.DisplayName = v; return default; }
    public ValueTask SetDescriptionAsync(T e, string? v, CancellationToken ct) { e.Description = v; return default; }
    public ValueTask SetNameAsync(T e, string? v, CancellationToken ct) { e.Name = v; return default; }
    public ValueTask SetSubjectAsync(T e, string? v, CancellationToken ct) { e.Subject = v; return default; }
    public ValueTask SetStatusAsync(T e, string? v, CancellationToken ct) { e.Status = v; return default; }
    public ValueTask SetTypeAsync(T e, string? v, CancellationToken ct) { e.Type = v; return default; }
    public ValueTask SetPayloadAsync(T e, string? v, CancellationToken ct) { e.Payload = v; return default; }
    public ValueTask SetReferenceIdAsync(T e, string? v, CancellationToken ct) { e.ReferenceId = v; return default; }
    public ValueTask SetCreationDateAsync(T e, DateTimeOffset? v, CancellationToken ct) { e.CreationDate = v; return default; }
    public ValueTask SetExpirationDateAsync(T e, DateTimeOffset? v, CancellationToken ct) { e.ExpirationDate = v; return default; }
    public ValueTask SetRedemptionDateAsync(T e, DateTimeOffset? v, CancellationToken ct) { e.RedemptionDate = v; return default; }
    public ValueTask SetPermissionsAsync(T e, ImmutableArray<string> v, CancellationToken ct) { e.Permissions = v; return default; }
    public ValueTask SetPostLogoutRedirectUrisAsync(T e, ImmutableArray<string> v, CancellationToken ct) { e.PostLogoutRedirectUris = v; return default; }
    public ValueTask SetRedirectUrisAsync(T e, ImmutableArray<string> v, CancellationToken ct) { e.RedirectUris = v; return default; }
    public ValueTask SetRequirementsAsync(T e, ImmutableArray<string> v, CancellationToken ct) { e.Requirements = v; return default; }
    public ValueTask SetResourcesAsync(T e, ImmutableArray<string> v, CancellationToken ct) { e.Resources = v; return default; }
    public ValueTask SetScopesAsync(T e, ImmutableArray<string> v, CancellationToken ct) { e.Scopes = v; return default; }
    public ValueTask SetSettingsAsync(T e, ImmutableDictionary<string, string> v, CancellationToken ct) { e.Settings = v; return default; }
    public ValueTask SetPropertiesAsync(T e, ImmutableDictionary<string, JsonElement> v, CancellationToken ct) { e.Properties = v; return default; }
    public ValueTask SetDisplayNamesAsync(T e, ImmutableDictionary<CultureInfo, string> v, CancellationToken ct) { e.DisplayNames = v.ToImmutableDictionary(x => x.Key.Name, x => x.Value); return default; }
    public ValueTask SetDescriptionsAsync(T e, ImmutableDictionary<CultureInfo, string> v, CancellationToken ct) { e.Descriptions = v.ToImmutableDictionary(x => x.Key.Name, x => x.Value); return default; }
    public ValueTask SetJsonWebKeySetAsync(T e, JsonWebKeySet? v, CancellationToken ct) { e.JsonWebKeySet = v == null ? null : JsonSerializer.Serialize(v); return default; }
}
