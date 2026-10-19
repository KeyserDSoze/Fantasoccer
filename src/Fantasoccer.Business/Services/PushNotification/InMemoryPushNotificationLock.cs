using System.Collections.Concurrent;

namespace Fantasoccer.Business;

public sealed class InMemoryPushNotificationLock : IPushNotificationLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public async ValueTask<IPushNotificationLockHandle> AcquireAsync(string name, CancellationToken cancellationToken = default)
    {
        var semaphore = Locks.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore, cancellationToken);
    }

    private sealed class Releaser(SemaphoreSlim semaphore, CancellationToken cancellationToken) : IPushNotificationLockHandle
    {
        public CancellationToken CancellationToken => cancellationToken;
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
