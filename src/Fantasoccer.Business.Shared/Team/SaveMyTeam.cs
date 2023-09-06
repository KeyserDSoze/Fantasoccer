using RepositoryFramework;

namespace Fantasoccer.Business.Shared
{
    internal sealed class SaveMyTeam : IRepositoryBusinessBeforeInsert<Team, TeamDayKey>, IRepositoryBusinessBeforeUpdate<Team, TeamDayKey>
    {
        public int Priority => -1;
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
            IEnumerable<(FantaSoccerRole Status, IEnumerable<(Role Role, int Count)> PlayersByRole)> values =
                from player in entity.Value.ActivePlayers
                group player by player.Position
                into status
                select (status.Key,
                        from playerByStatus in status
                        group playerByStatus by playerByStatus.Role
                        into roles
                        select (roles.Key, roles.Count())
                );
            var goalkeepers = (values.Any(x => x.Status == FantaSoccerRole.GoalKeeper) ? values.FirstOrDefault(x => x.Status == FantaSoccerRole.GoalKeeper).PlayersByRole.Sum(x => x.Count) : 0);
            var defensors = (values.Any(x => x.Status == FantaSoccerRole.Defensor) ? values.FirstOrDefault(x => x.Status == FantaSoccerRole.Defensor).PlayersByRole.Sum(x => x.Count) : 0);
            var midfielders = (values.Any(x => x.Status == FantaSoccerRole.Midfielder) ? values.FirstOrDefault(x => x.Status == FantaSoccerRole.Midfielder).PlayersByRole.Sum(x => x.Count) : 0);
            var forwards = (values.Any(x => x.Status == FantaSoccerRole.Forward) ? values.FirstOrDefault(x => x.Status == FantaSoccerRole.Forward).PlayersByRole.Sum(x => x.Count) : 0);
            var startingTeamCounter = goalkeepers + defensors + midfielders + forwards;
            if (startingTeamCounter != 11)
                return Task.FromResult(new State<Team, TeamDayKey>(false, message: "Meno di 11 giocatori titolari schierati."));
            foreach (var value in values)
            {
                if (value.Status != FantaSoccerRole.Tribune && value.PlayersByRole.Count() > 1)
                    return Task.FromResult(State.NotOk<Team, TeamDayKey>());
                switch (value.Status)
                {
                    case FantaSoccerRole.GoalKeeper:
                    case FantaSoccerRole.BackupGoalKeeper:
                        if (value.PlayersByRole.FirstOrDefault().Role != Role.GoalKeeper)
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: "Portiere errato."));
                        if (value.PlayersByRole.FirstOrDefault().Count != 1)
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: $"Portiere {(value.Status == FantaSoccerRole.GoalKeeper ? "titolare" : "di riserva")} manacante o troppi."));
                        break;
                    case FantaSoccerRole.Defensor:
                    case FantaSoccerRole.FirstBackupDefensor:
                    case FantaSoccerRole.SecondBackupDefensor:
                        if (value.PlayersByRole.FirstOrDefault().Role != Role.Defensor)
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: "Giocatore schierato erroneamente come difensore."));
                        if (value.Status == FantaSoccerRole.Defensor && (value.PlayersByRole.FirstOrDefault().Count <= 2 || value.PlayersByRole.FirstOrDefault().Count > 5))
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: $"Difensori titolari in numero sbagliato {value.PlayersByRole.FirstOrDefault().Count}. Puoi mettere da un minimo di 3 ad un massimo di 5 difensori."));
                        if (value.Status != FantaSoccerRole.Defensor && value.PlayersByRole.FirstOrDefault().Count != 1)
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: $"{(value.Status == FantaSoccerRole.FirstBackupDefensor ? "Primo difensore di riserva" : "Secondo difensore di riserva")} manacante o troppi."));
                        break;
                    case FantaSoccerRole.Midfielder:
                    case FantaSoccerRole.FirstBackupMidfielder:
                    case FantaSoccerRole.SecondBackupMidfielder:
                        if (value.PlayersByRole.FirstOrDefault().Role != Role.Midfielder)
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: "Giocatore schierato erroneamente come centrocampista."));
                        if (value.Status == FantaSoccerRole.Midfielder && (value.PlayersByRole.FirstOrDefault().Count <= 2 || value.PlayersByRole.FirstOrDefault().Count > 5))
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: $"Centrocampisti titolari in numero sbagliato {value.PlayersByRole.FirstOrDefault().Count}. Puoi mettere da un minimo di 3 ad un massimo di 5 centrocampisti."));
                        if (value.Status != FantaSoccerRole.Midfielder && value.PlayersByRole.FirstOrDefault().Count != 1)
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: $"{(value.Status == FantaSoccerRole.FirstBackupMidfielder ? "Primo centrocampista di riserva" : "Secondo centrocampista di riserva")} manacante o troppi."));
                        break;
                    case FantaSoccerRole.Forward:
                    case FantaSoccerRole.FirstBackupForward:
                    case FantaSoccerRole.SecondBackupForward:
                        if (value.PlayersByRole.FirstOrDefault().Role != Role.Forward)
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: "Giocatore schierato erroneamente come attaccante."));
                        if (value.Status == FantaSoccerRole.Forward && (value.PlayersByRole.FirstOrDefault().Count < 1 || value.PlayersByRole.FirstOrDefault().Count > 3))
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: $"Attaccanti titolari in numero sbagliato {value.PlayersByRole.FirstOrDefault().Count}. Puoi mettere da un minimo di 1 ad un massimo di 3 attaccanti."));
                        if (value.Status != FantaSoccerRole.Forward && value.PlayersByRole.FirstOrDefault().Count != 1)
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: $"{(value.Status == FantaSoccerRole.FirstBackupMidfielder ? "Primo attaccante di riserva" : "Secondo attaccante di riserva")} manacante o troppi."));
                        break;
                    case FantaSoccerRole.Tribune:
                        if (value.PlayersByRole.Sum(x => x.Count) != 7)
                            return Task.FromResult(new State<Team, TeamDayKey>(false, message: "Devono esserci sette giocatori in tribuna."));
                        break;
                    case FantaSoccerRole.All:
                        return Task.FromResult(new State<Team, TeamDayKey>(false, message: "Giocatore in ruolo errato."));
                }
            }
            return Task.FromResult(new State<Team, TeamDayKey>(true, message: "Team salvato con successo."));
        }
    }
}
