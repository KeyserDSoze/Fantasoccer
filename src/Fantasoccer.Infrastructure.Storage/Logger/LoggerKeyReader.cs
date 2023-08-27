using RepositoryFramework.Infrastructure.Azure.Storage.Table;

namespace Fantasoccer.Infrastructure.Storage.Logger
{
    internal sealed class LoggerKeyReader : ITableStorageKeyReader<FantasoccerLog, string>
    {
        public (string PartitionKey, string RowKey) Read(string key)
        {
            var splitted = key.Split("_-_");
            return (splitted[0], splitted[1]);
        }

        public string Read(FantasoccerLog entity)
        {
            return $"{entity.PrimaryKey}_-_{entity.SecondaryKey}";
        }
    }
}
