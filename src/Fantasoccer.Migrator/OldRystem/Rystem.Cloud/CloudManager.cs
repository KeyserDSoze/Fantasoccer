using Rystem.Cloud.Azure;

namespace Rystem.Cloud
{
    public sealed class CloudManager
    {
        private readonly Dictionary<string, CloudOptions> Options = new();
        private readonly Dictionary<string, ICloudManagement> Manager = new();
        internal void Add(string key, CloudOptions options)
        {
            if (!Options.ContainsKey(key))
                Options.Add(key, options);
        }
        internal CloudOptions Get(string key)
            => Options[key];
        public async Task<ICloudManagement> GetManagerAsync(string key = "")
        {
            if (!Manager.ContainsKey(key))
            {
                //await RaceCondition.RunAsync(async () =>
                //{
                var value = Options[key];
                switch (value.Type)
                {
                    case CloudType.Azure:
                        Manager.Add(key, new AzureCloudManager(await value.GetAzureAadAppRegistrationAsync(), ServiceLocator.GetService<IHttpClientFactory>()));
                        break;
                    default:
                        Manager.Add(key, default);
                        break;
                }
                //}, $"RaceCondition.Cloud.{key}");
            }
            return Manager[key];
        }
    }
}
