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
        private const string UncheckLabel = "uncheck_";
        private const string ServerUncheckLabel = "serveruncheck_";
        private const string ServerFinalUncheckLabel = "serveruncheck_uncheck_";
        private Task<State<Team, TeamDayKey>> BeforeInsertOrUpdateAsync(Entity<Team, TeamDayKey> entity)
        {
            if (entity.Key == null)
                return Task.FromResult(State.NotOk<Team, TeamDayKey>());
            if (entity.Value == null)
                return Task.FromResult(State.NotOk<Team, TeamDayKey>());
            if (entity.Key.Email.StartsWith(UncheckLabel))
            {
                entity.Key.Email = $"{ServerUncheckLabel}{entity.Key.Email}";
                return Task.FromResult(State.Ok<Team, TeamDayKey>());
            }
            else if (entity.Key.Email.StartsWith(ServerFinalUncheckLabel))
            {
                entity.Key.Email = entity.Key.Email.Replace(ServerFinalUncheckLabel, string.Empty);
                return Task.FromResult(State.Ok<Team, TeamDayKey>());
            }
            else
            {
                var response = _checker.Validate(entity.Value);
                entity.Value.LastUpdate = FantacalcioTime.ItalyNow;
                return Task.FromResult(new State<Team, TeamDayKey>(response.IsOk, message: response.Message));
            }
        }
    }
}
