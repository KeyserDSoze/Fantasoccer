using Rystem;
using Rystem.Business;
using Rystem.Text;
using System;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public class FantacalcioContext : IData
    {
        public string Name { get; set; }

        public static async Task<T> GetAsync<T>(string name)
        {
            var context = new FantacalcioContext { Name = name };
            if (await context.ExistsAsync())
                return await context.ReadAsync<FantacalcioContext, T>();
            else
                return default;
        }
        public static Task<bool> ExistAsync(string name)
        {
            var context = new FantacalcioContext { Name = name };
            return context.ExistsAsync();
        }
        public static Task<bool> DeleteAsync(string name)
        {
            var context = new FantacalcioContext { Name = name };
            return context.DeleteAsync();
        }

        public static Task SaveAsync<T>(string name, T value)
            => new FantacalcioContext { Name = name }.WriteAsync(value);
    }
}