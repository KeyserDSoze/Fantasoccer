using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Fantasoccer.Domain;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using RepositoryFramework;

namespace Fantasoccer.DataMigration;

public sealed partial class CoreDatasetImporter
{
    private static void LoadAuctions(VerifiedJsonDataset dataset, CorePlan plan, CoreImportOptions options,
        Func<string, string, Guid> map, Func<RealPlayer, int, bool, PlayerSource, PlayerSeason> player, Func<RealTeam?, string> club,
        Func<string, string, UserAccount> addUser)
    {
        Guid Existing(string kind, string key) => plan.Maps.SingleOrDefault(x => x.Kind == kind && x.LegacyKey == key)?.TargetId
            ?? throw new CoreImportException("missing_auction_reference");
        foreach (var document in dataset.Documents.Where(x => x.Container == "auction"))
        {
            var entity = document.Envelope.Deserialize<Entity<AuctionWrapper, string>>() ?? throw new CoreImportException("invalid_auction_envelope");
            var source = entity.Value ?? throw new CoreImportException("missing_auction");
            if (source.Id != entity.Key || string.IsNullOrWhiteSpace(source.Id) || plan.Auctions.Any(x => x.Id == map("auction", source.Id)))
                throw new CoreImportException("auction_collision");
            var key = source.LeagueKey ?? throw new CoreImportException("missing_auction_scope");
            var group = Existing("group", key.Group);
            var league = Existing("league", Composite(key.Group, key.League));
            if (!plan.LeagueSeasons.Any(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == key.Year)) throw new CoreImportException("missing_auction_season");
            if ((int)source.Type is < 0 or > 2 || (int)source.Kind is < 0 or > 1 || (int)source.Status is < 0 or > 3 || (int)source.CurrentRole is < 0 or > 3)
                throw new CoreImportException("invalid_auction_state");
            var creatorEmail = Email(source.Creator);
            var creator = addUser(creatorEmail, creatorEmail);
            if (!plan.Memberships.Any(x => x.GroupId == group && x.UserId == creator.Id))
                plan.Memberships.Add(new() { GroupId = group, UserId = creator.Id, Roles = GroupRole.None });
            var auction = new AuctionSession { GroupId = group, LeagueId = league, SeasonIndex = key.Year, Id = map("auction", source.Id),
                CreatorUserId = creator.Id, CreatorEmail = source.Creator, CreatedAtUtc = Utc(source.Created), LegacyCreatedUtc = Local(source.Created),
                Type = (int)source.Type, Kind = (int)source.Kind, Status = (int)source.Status, SecondsPerAuction = source.SecondsPerAuction, CurrentRole = (int)source.CurrentRole };
            if (source.Actual is { } actual)
            {
                var sample = actual.Player ?? throw new CoreImportException("missing_actual_auction_player");
                var identity = player(sample, key.Year, false, PlayerOrigin(document, ".a.pl"));
                var team = Team(actual.Owner);
                auction.Actual = new() { PlayerId = identity.PlayerId, ClubId = club(sample.RealTeam), PlayerRole = (int)sample.Role, IsActive = sample.IsActive,
                    IsVisible = sample.Visible, StartedAtUtc = Utc(actual.Started), LegacyStartedUtc = Local(actual.Started),
                    TimerStartedAtUtc = actual.StartingTime is { } timer ? Utc(timer) : null, LegacyTimerStartedUtc = actual.StartingTime is { } localTimer ? Local(localTimer) : null,
                    WinningTeamId = team?.Id, WinningBasketId = team?.BasketId, OwnerEmail = actual.Owner, Price = actual.Price,
                    SubstitutedPlayerId = string.IsNullOrEmpty(actual.Substituted) ? null : Identity(actual.Substituted), Statistics = Statistics(sample) };
            }
            foreach (var (role, entries) in source.ListOfPlayers ?? [])
            {
                if ((int)role is < -1 or > 3) throw new CoreImportException("invalid_auction_player_role");
                foreach (var (entry, ordinal) in entries.Select((entry, ordinal) => (entry, ordinal)))
                {
                    var plaintextKey = DecryptLegacyAuctionKey(entry.EncryptedKey, options);
                    auction.PlayerOrder.Add(new() { Role = (int)role, Ordinal = ordinal, PlayerId = Identity(plaintextKey), IsShown = entry.IsShowed });
                }
            }
            foreach (var (role, last) in source.LastShow ?? [])
                auction.LastShown.Add(new() { Role = (int)role, PlayerId = string.IsNullOrEmpty(last) ? null : Identity(last) });
            foreach (var (win, ordinal) in (source.Winnings ?? []).Select((win, ordinal) => (win, ordinal)))
            {
                var team = Team(win.Owner);
                auction.Winnings.Add(new() { Ordinal = ordinal, PlayerId = Identity(win.PlayerKey), TeamId = team?.Id, BasketId = team?.BasketId,
                    OwnerEmail = win.Owner, Price = win.Price });
            }
            foreach (var (participant, ordinal) in (source.Participants ?? []).Select((participant, ordinal) => (participant, ordinal)))
                auction.LegacyParticipants.Add(new() { Ordinal = ordinal, Name = participant.Name ?? "", OwnerEmail = participant.Owner ?? "",
                    CoOwners = (participant.AdditionalOwners ?? []).Select((email, index) => new AuctionLegacyCoOwner { Ordinal = index, Email = email }).ToList() });
            plan.Auctions.Add(auction);

            string Identity(string legacyKey)
            {
                var id = plan.Players.SingleOrDefault(x => x.Id == legacyKey)?.Id ?? throw new CoreImportException("missing_auction_reference");
                if (!plan.PlayerSeasons.Any(x => x.PlayerId == id && x.SeasonIndex == key.Year)) throw new CoreImportException("missing_auction_player_season");
                return id;
            }
            FantasyTeamSeason? Team(string? owner)
            {
                if (string.IsNullOrEmpty(owner)) return null;
                var user = ExistingOwner(plan, group, owner, "missing_auction_owner");
                var baskets = plan.Participations.Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == key.Year).Select(x => x.BasketId).ToHashSet();
                var teams = plan.Owners.Where(x => x.GroupId == group && x.SeasonIndex == key.Year && x.UserId == user.Id && x.IsPrimary && baskets.Contains(x.BasketId)).ToArray();
                return teams.Length == 1 ? plan.Teams.Single(x => x.Id == teams[0].TeamId) : throw new CoreImportException("unresolved_historical_team_identity");
            }
        }
    }

    private static AuctionPlayerStatistic Statistics(StatPlayer source)
    {
        if (!float.IsFinite(source.Summatory) || !float.IsFinite(source.FantaSummatory)) throw new CoreImportException("invalid_auction_statistics");
        return new() { Summatory = source.Summatory, FantaSummatory = source.FantaSummatory, WithVote = source.WithVote, WithoutVote = source.WithoutVote,
            NoPlayed = source.NoPlayed, WithSpecial = source.WithSpecial, Goals = source.Goal, Penalties = source.Penalty, Assists = source.Assist,
            StoppedPenalties = source.StoppedPenalty, SufferedGoals = source.SufferedGoal, WrongedPenalties = source.WrongedPenalty, OwnGoals = source.OwnGoal,
            YellowCards = source.YellowCard, RedCards = source.RedCard, EnoughVotes = source.IsEnough, ManOfTheMatch = source.ManOfTheMatch, Injured = source.Injured,
            HasDescription = source.Description != null, DescriptionTop = source.Description?.Top, DescriptionDown = source.Description?.Down,
            HasGameHistory = source.AllGames != null, Games = (source.AllGames ?? []).Select((game, index) =>
                new FootballStatisticGame { Day = game.SerieADay, Ordinal = index, Vote = game.Vote, Positiveness = game.Positiveness }).ToList() };
    }

    private static string DecryptLegacyAuctionKey(string ciphertext, CoreImportOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.LegacyEncryptionKey) || string.IsNullOrWhiteSpace(options.LegacyEncryptionIV))
            throw new CoreImportException("legacy_auction_encryption_required");
        try
        {
            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(options.LegacyEncryptionKey); aes.IV = Convert.FromBase64String(options.LegacyEncryptionIV);
            using var bytes = new MemoryStream(Convert.FromBase64String(ciphertext));
            using var decrypted = new CryptoStream(bytes, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(decrypted);
            return reader.ReadToEnd();
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or ArgumentException)
        { throw new CoreImportException("invalid_legacy_auction_encryption"); }
    }
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
