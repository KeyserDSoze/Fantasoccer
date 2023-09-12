// See https://aka.ms/new-console-template for more information
using FantacalcioV4.ApiManager;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Rystem.Security.Cryptography;
using Rystem;

namespace Migrator
{
    public static class EntryPoint
    {
        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            var storageAccountName = "superfc";
            var storageAccountKey = "CS124aZwZywrMrxFiCdGFipSN+UbExX1BPEb/9aY1dWGLdSboO2drxh3yFSFOVM6uGc7Grahs/sZcpDRtP77yg==";
            services
                .AddRystem()
                .AddAzureService()
                .AddStorage(new Rystem.Azure.Integration.Storage.StorageAccount(storageAccountName, storageAccountKey));
            services.AddFantacalcioApi();
            var serviceProvider = services.BuildServiceProvider().CreateScope().ServiceProvider;
            serviceProvider.UseRystem();
            var manager = serviceProvider.GetRequiredService<Manager>();
            var groups = await manager.Group.ListAsync();
            foreach (var group in groups)
            {
                Console.WriteLine(group);
            }
        }
    }
}

