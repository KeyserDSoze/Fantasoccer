using RepositoryFramework;

namespace Fantasoccer.Business.Shared
{
    internal sealed class SaveMyTeam : IRepositoryBusinessBeforeInsert<Team, TeamDayKey>, IRepositoryBusinessBeforeUpdate<Team, TeamDayKey>
    {
        private readonly ITeamChecker _checker;
        public int Priority => -1;
        public SaveMyTeam(ITeamChecker checker)
        {
            _checker = checker;
        }
        public Task<State<Team, TeamDayKey>> BeforeInsertAsync(Entity<Team, TeamDayKey> entity, CancellationToken cancellationToken = default)
        {
            return BeforeInsertOrUpdateAsync(entity);
        }
        Task<State<Team, TeamDayKey>> IRepositoryBusinessBeforeUpdate<Team, TeamDayKey>.BeforeUpdateAsync(Entity<Team, TeamDayKey> entity, CancellationToken cancellationToken)
        {
            return BeforeInsertOrUpdateAsync(entity);
        }
        private Task<State<Team, TeamDayKey>> BeforeInsertOrUpdateAsync(Entity<Team, TeamDayKey> entity)
        {
            if (entity.Value == null)
                return Task.FromResult(State.NotOk<Team, TeamDayKey>());
            var response = _checker.Validate(entity.Value);
            return Task.FromResult(new State<Team, TeamDayKey>(response.IsOk, message: response.Message));
        }
    }
}
