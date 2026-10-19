using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Services;

// Simulate a repository that returns a failed State instead of throwing.
public sealed class FailingPushRepository<T, TKey>(IRepository<T, TKey> inner) : IRepository<T, TKey>
    where TKey : notnull
{
    public Func<TKey, bool> FailWrite { get; set; } = _ => true;

    public Task<State<T, TKey>> InsertAsync(TKey key, T value, CancellationToken cancellationToken = default)
        => FailWrite(key) ? Task.FromResult(new State<T, TKey>(false, value, key, null, "Simulated save failure")) : inner.InsertAsync(key, value, cancellationToken);
    public Task<State<T, TKey>> UpdateAsync(TKey key, T value, CancellationToken cancellationToken = default)
        => FailWrite(key) ? Task.FromResult(new State<T, TKey>(false, value, key, null, "Simulated save failure")) : inner.UpdateAsync(key, value, cancellationToken);
    public Task<State<T, TKey>> DeleteAsync(TKey key, CancellationToken cancellationToken = default)
        => inner.DeleteAsync(key, cancellationToken);
    public Task<State<T, TKey>> ExistAsync(TKey key, CancellationToken cancellationToken = default)
        => inner.ExistAsync(key, cancellationToken);
    public Task<T?> GetAsync(TKey key, CancellationToken cancellationToken = default)
        => inner.GetAsync(key, cancellationToken);
    public IAsyncEnumerable<Entity<T, TKey>> QueryAsync(IFilterExpression filter, CancellationToken cancellationToken = default)
        => inner.QueryAsync(filter, cancellationToken);
    public ValueTask<TProperty> OperationAsync<TProperty>(OperationType<TProperty> operation, IFilterExpression filter, CancellationToken cancellationToken = default)
        => inner.OperationAsync(operation, filter, cancellationToken);
    public IAsyncEnumerable<BatchResult<T, TKey>> BatchAsync(BatchOperations<T, TKey> operations, CancellationToken cancellationToken = default)
        => inner.BatchAsync(operations, cancellationToken);
    public ValueTask<bool> BootstrapAsync(CancellationToken cancellationToken = default)
        => inner.BootstrapAsync(cancellationToken);
}
