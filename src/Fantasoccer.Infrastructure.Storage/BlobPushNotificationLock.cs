using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System.Security.Cryptography;
using System.Text;

namespace Fantasoccer.Infrastructure;

// Use the same storage account as the repositories, including when the API is
// scaled out. The in-memory implementation is used only by local/test storage.
internal sealed class BlobPushNotificationLock(string connectionString) : IPushNotificationLock
{
    private readonly BlobContainerClient _container = new(connectionString, "pushnotificationlocks");

    public async ValueTask<IPushNotificationLockHandle> AcquireAsync(string name, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = _container.GetBlobClient(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))));
        try { await blob.UploadAsync(BinaryData.FromString(string.Empty), overwrite: false, cancellationToken); }
        catch (RequestFailedException ex) when (ex.Status == 409) { }
        var lease = blob.GetBlobLeaseClient();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await lease.AcquireAsync(TimeSpan.FromSeconds(60), cancellationToken: cancellationToken);
                return new HeldLease(lease, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
        }
    }

    private sealed class HeldLease : IPushNotificationLockHandle
    {
        private readonly BlobLeaseClient _lease;
        private readonly CancellationTokenSource _stop = new();
        private readonly CancellationTokenSource _lifetime;
        private readonly Task _renewal;
        public CancellationToken CancellationToken => _lifetime.Token;

        public HeldLease(BlobLeaseClient lease, CancellationToken cancellationToken)
        {
            _lease = lease;
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _renewal = RenewAsync();
        }

        private async Task RenewAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            try
            {
                while (await timer.WaitForNextTickAsync(_stop.Token))
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    await _lease.RenewAsync(cancellationToken: timeout.Token);
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch
            {
                // Abort repository writes and provider requests if ownership of the
                // distributed lock can no longer be guaranteed.
                await _lifetime.CancelAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            try { await _renewal; }
            finally
            {
                try { await _lease.ReleaseAsync(); }
                finally { _stop.Dispose(); _lifetime.Dispose(); }
            }
        }
    }
}
