
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Leagues
{
    public class SuperLeagueTest
    {
        private readonly ILeagueCalculator _superLeagueCalculator;

        public SuperLeagueTest(IFactory<ILeagueCalculator> factory)
        {
            _superLeagueCalculator = factory.Create(LeagueType.SuperLeague.ToString())!;
        }
        [Fact]
        public void TestRightFormation()
        {
            var players = new List<EnrichedPlayer>
            {
                new EnrichedPlayer { Current = new Player { Name = "A",Role = Role.GoalKeeper, Position = FantaSoccerRole.GoalKeeper }, Vote = new Vote { Value = 6, OwnGoal = 1 } },
                new EnrichedPlayer { Current = new Player { Name = "B",Role = Role.GoalKeeper, Position = FantaSoccerRole.Tribune }, Vote = new Vote { Value = 6.5f, StoppedPenalty = 1 } },
                new EnrichedPlayer { Current = new Player { Name = "C",Role = Role.GoalKeeper, Position = FantaSoccerRole.BackupGoalKeeper }, Vote = new Vote { Value = 6.5f } },

                new EnrichedPlayer { Current = new Player { Name = "D", Role = Role.Defensor, Position = FantaSoccerRole.SecondBackupDefensor }, Vote = new Vote { Value = 6 } },
                new EnrichedPlayer { Current = new Player { Name = "E", Role = Role.Defensor, Position = FantaSoccerRole.Defensor }, Vote = new Vote { Value = 3 } },
                new EnrichedPlayer { Current = new Player { Name = "F",Role = Role.Defensor, Position = FantaSoccerRole.Defensor }, Vote = new Vote { Value = 6, Status = Behaviour.RedCard } },
                new EnrichedPlayer { Current = new Player { Name = "G",Role = Role.Defensor, Position = FantaSoccerRole.FirstBackupDefensor }, Vote = new Vote { Value = 6, Status = Behaviour.RedCard } },
                new EnrichedPlayer { Current = new Player { Name = "H",Role = Role.Defensor, Position = FantaSoccerRole.Tribune }, Vote = new Vote { Value = 5.5f } },
                new EnrichedPlayer { Current = new Player { Name = "I",Role = Role.Defensor, Position = FantaSoccerRole.Defensor }, Vote = new Vote { Value = 4 } },
                new EnrichedPlayer { Current = new Player { Name = "J",Role = Role.Defensor, Position = FantaSoccerRole.Defensor }, Vote = new Vote { Value = 4 } },
                new EnrichedPlayer { Current = new Player { Name = "K",Role = Role.Defensor, Position = FantaSoccerRole.Defensor }, Vote = new Vote { Value = 3 } },

                new EnrichedPlayer { Current = new Player { Name = "L",Role = Role.Midfielder, Position = FantaSoccerRole.Tribune }, Vote = new Vote { Value = 7, Goal = 1 } },
                new EnrichedPlayer { Current = new Player { Name = "M",Role = Role.Midfielder, Position = FantaSoccerRole.Tribune }, Vote = new Vote { Value = 5.5f } },
                new EnrichedPlayer { Current = new Player { Name = "N",Role = Role.Midfielder, Position = FantaSoccerRole.Midfielder }, Vote = new Vote { Value = 6 } },
                new EnrichedPlayer { Current = new Player { Name = "O",Role = Role.Midfielder, Position = FantaSoccerRole.Midfielder }, Vote = new Vote { Value = 5 } },
                new EnrichedPlayer { Current = new Player { Name = "P",Role = Role.Midfielder, Position = FantaSoccerRole.Midfielder }, Vote = new Vote { Value = 4 } },
                new EnrichedPlayer { Current = new Player { Name = "Q",Role = Role.Midfielder, Position = FantaSoccerRole.Midfielder }, Vote = new Vote { Value = 4 } },
                new EnrichedPlayer { Current = new Player { Name = "R",Role = Role.Midfielder, Position = FantaSoccerRole.FirstBackupMidfielder }, Vote = new Vote { Value = 5 } },
                new EnrichedPlayer { Current = new Player { Name = "S",Role = Role.Midfielder , Position = FantaSoccerRole.SecondBackupMidfielder}, Vote = new Vote { Value = 4 } },

                new EnrichedPlayer { Current = new Player { Name = "T",Role = Role.Forward, Position = FantaSoccerRole.Tribune }, Vote = new Vote { Value = 6, Assist = 1, Goal = 3 } },
                new EnrichedPlayer { Current = new Player { Name = "U", Role = Role.Forward, Position = FantaSoccerRole.Tribune }, Vote = new Vote { Value = 4 } },
                new EnrichedPlayer { Current = new Player { Name = "V", Role = Role.Forward, Position = FantaSoccerRole.Forward }, Vote = new Vote { Value = 6 } },
                new EnrichedPlayer { Current = new Player { Name = "X", Role = Role.Forward, Position = FantaSoccerRole.FirstBackupForward }, Vote = new Vote { Value = 4 } },
                new EnrichedPlayer { Current = new Player { Name = "Y", Role = Role.Forward, Position = FantaSoccerRole.SecondBackupForward }, Vote = new Vote { Value = 5.5f } },
                new EnrichedPlayer { Current = new Player { Name = "W", Role = Role.Forward, Position = FantaSoccerRole.Tribune }, Vote = new Vote { Value = 4.5f } },
            };
            var formation = _superLeagueCalculator.GetRightFormation(players);
            Assert.Equal(25, formation.Count());
            var charactersTeam = string.Join(',', formation.Where(x => x.CurrentPosition == FantaSoccerRole.GoalKeeper || x.CurrentPosition == FantaSoccerRole.Defensor || x.CurrentPosition == FantaSoccerRole.Midfielder || x.CurrentPosition == FantaSoccerRole.Forward).Select(x => x.Current.Name).Select(x => x).OrderBy(x => x));
            Assert.Equal("B,D,F,G,L,M,N,O,T,V,Y", charactersTeam);
        }
    }
}
