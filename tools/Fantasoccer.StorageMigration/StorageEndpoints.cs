using Azure.Data.Tables;
using Azure.Storage.Blobs;

namespace Fantasoccer.StorageMigration;

public sealed class StorageEndpoints
{
    public BlobServiceClient Blobs { get; }
    public TableServiceClient Tables { get; }
    public string SourceId => SafeUri(Blobs.Uri) + "|" + SafeUri(Tables.Uri);

    public StorageEndpoints(string connectionString)
    {
        var blobOptions = new BlobClientOptions();
        blobOptions.Retry.MaxRetries = 3;
        blobOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(30);
        blobOptions.Diagnostics.IsLoggingEnabled = false;
        var tableOptions = new TableClientOptions();
        tableOptions.Retry.MaxRetries = 3;
        tableOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(30);
        tableOptions.Diagnostics.IsLoggingEnabled = false;
        Blobs = new BlobServiceClient(connectionString, blobOptions);
        Tables = new TableServiceClient(connectionString, tableOptions);
        if (Blobs.AccountName != Tables.AccountName)
            throw new ArgumentException("Blob and Table endpoints must use the same account.");
    }

    public void RequireLocalEmulator()
    {
        if (!IsLocal(Blobs.Uri) || !IsLocal(Tables.Uri) ||
            Blobs.AccountName is not ("devstoreaccount1" or "migrationtest" or "migrationrestore"))
            throw new InvalidOperationException("Restore/test operations require a dedicated local Azurite account.");
    }

    public void RequireExportPermission(bool allowRemoteSource)
    {
        if (IsLocal(Blobs.Uri) && IsLocal(Tables.Uri)) return;
        if (!allowRemoteSource || Blobs.Uri.Scheme != "https" || Tables.Uri.Scheme != "https")
            throw new InvalidOperationException("Remote export requires HTTPS and explicit --allow-remote-source.");
    }

    private static bool IsLocal(Uri uri) => uri.IsLoopback || uri.Host is "azurite" or "test-azurite";
    private static string SafeUri(Uri uri) => new UriBuilder(uri)
    {
        Query = "", Fragment = "", UserName = "", Password = ""
    }.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
}
