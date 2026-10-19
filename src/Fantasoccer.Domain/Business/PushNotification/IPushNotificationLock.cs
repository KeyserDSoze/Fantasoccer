namespace Fantasoccer;

// Shared by API mutations, subscription cleanup, and the scheduled dispatcher.
public interface IPushNotificationLock
{
    ValueTask<IPushNotificationLockHandle> AcquireAsync(string name, CancellationToken cancellationToken = default);
}

public interface IPushNotificationLockHandle : IAsyncDisposable
{
    CancellationToken CancellationToken { get; }
}
