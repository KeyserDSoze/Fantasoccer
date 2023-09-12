using System.Threading.Tasks;
using System;

namespace FantacalcioV4.Database
{
    public record GroupContext(string Name)
    {
        public Task SaveAsync(Group group)
            => Task.WhenAll(FantacalcioContext.SaveAsync(FantacalcioId.GetGroup(Name), group),
                new GroupKey() { Name = Name }.RestoreAsync(group));

        public Task<Group> GetAsync()
           => FantacalcioContext.GetAsync<Group>(FantacalcioId.GetGroup(Name));
    }
}