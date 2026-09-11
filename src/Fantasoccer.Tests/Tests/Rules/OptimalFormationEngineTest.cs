using Fantasoccer.Business;

namespace Fantasoccer.Tests.Tests.Rules
{
    public sealed class OptimalFormationEngineTest
    {
        [Fact]
        public void SelectsLegalFormationAndKeepsAllPlayers()
        {
            var players = CreatePlayers(3, 5, 4, 6);
            var result = new OptimalFormationEngine().Calculate(new OptimalFormationInput { Players = players });

            Assert.Equal(players.Count, result.Players.Count);
            Assert.Contains(result.Module, new[] { "3-4-3", "3-5-2", "4-3-3", "4-4-2", "4-5-1", "5-3-2", "5-4-1" });
            Assert.Equal(11, result.Players.Count(x => x.Position is FantaSoccerRole.GoalKeeper or FantaSoccerRole.Defensor or FantaSoccerRole.Midfielder or FantaSoccerRole.Forward));
            Assert.All(result.Players.Where(x => x.Position == FantaSoccerRole.GoalKeeper), x => Assert.Equal(Role.GoalKeeper, x.Role));
        }

        [Fact]
        public void UsesStableTieBreaking()
        {
            var players = CreatePlayers(2, 4, 4, 3);
            var engine = new OptimalFormationEngine();

            var first = engine.Calculate(new OptimalFormationInput { Players = players });
            var second = engine.Calculate(new OptimalFormationInput { Players = players.AsEnumerable().Reverse().ToList() });

            Assert.Equal(first.Module, second.Module);
            Assert.Equal(first.Players.Select(x => x.PlayerKey), second.Players.Select(x => x.PlayerKey));
        }

        [Fact]
        public void ReportsMissingFormationWhenRosterCannotCoverAllRoles()
        {
            var players = CreatePlayers(1, 1, 1, 1);
            var result = new OptimalFormationEngine().Calculate(new OptimalFormationInput { Players = players });

            Assert.Empty(result.Players);
            Assert.NotEmpty(result.Warnings);
        }

        [Fact]
        public void DoesNotStartUnavailablePlayerEvenWhenProjectionIsHigher()
        {
            var players = CreatePlayers(1, 4, 4, 3);
            var unavailable = players.Single(x => x.Role == Role.Defensor && x.Name.EndsWith("A"));
            var result = new OptimalFormationEngine().Calculate(new OptimalFormationInput
            {
                Players = players,
                Projections = new Dictionary<string, OptimalPlayerProjection>(StringComparer.OrdinalIgnoreCase)
                {
                    [unavailable.Key] = new(FantaAverage: 10, Availability: 0)
                }
            });

            Assert.DoesNotContain(result.Players, x => x.PlayerKey == unavailable.Key && IsStartingPosition(x.Position));
            Assert.Contains(result.Players, x => x.PlayerKey == unavailable.Key);
        }

        [Fact]
        public void PrefersThreeFourThreeWhenEveryRoleHasPlayableBackup()
        {
            var players = CreatePlayers(2, 5, 5, 4);

            var result = new OptimalFormationEngine().Calculate(new OptimalFormationInput { Players = players });

            Assert.Equal("3-4-3", result.Module);
        }

        [Fact]
        public void AvoidsThreeFourThreeWhenItLeavesNoPlayableMidfielderBackup()
        {
            var players = CreatePlayers(2, 5, 4, 4);

            var result = new OptimalFormationEngine().Calculate(new OptimalFormationInput { Players = players });

            Assert.Equal("4-3-3", result.Module);
        }

        private static bool IsStartingPosition(FantaSoccerRole position)
            => position is FantaSoccerRole.GoalKeeper or FantaSoccerRole.Defensor or FantaSoccerRole.Midfielder or FantaSoccerRole.Forward;

        private static List<Player> CreatePlayers(int goalkeepers, int defenders, int midfielders, int forwards)
        {
            var players = new List<Player>();
            Add(Role.GoalKeeper, goalkeepers);
            Add(Role.Defensor, defenders);
            Add(Role.Midfielder, midfielders);
            Add(Role.Forward, forwards);
            return players;

            void Add(Role role, int count)
            {
                for (var index = 0; index < count; index++)
                {
                    players.Add(new Player
                    {
                        Name = $"{role} Player {new string((char)('A' + index), 1)}",
                        Role = role,
                        RealTeam = new RealTeam { Name = "Team" },
                        IsActive = true,
                        Position = FantaSoccerRole.Tribune,
                        Status = PlayerInTeamStatus.Active
                    });
                }
            }
        }
    }
}
