using System.Text.Json.Serialization;

namespace FantacalcioV4
{
    public record SummedVotesPlayer(string Key, SummedVotes SummedVote) : Player(Key, 0, default)
    {
        [JsonIgnore]
        public string OwnedTeam { get; set; }
        public void AddVote(Vote vote)
            => SummedVote.Votes.Add(vote);
    }
}