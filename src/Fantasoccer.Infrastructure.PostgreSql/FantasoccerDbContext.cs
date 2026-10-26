using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Infrastructure.PostgreSql;

public sealed class FantasoccerDbContext(DbContextOptions<FantasoccerDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<FantasyGroup> Groups => Set<FantasyGroup>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<FootballPlayer> Players => Set<FootballPlayer>();
    public DbSet<FootballClub> Clubs => Set<FootballClub>();
    public DbSet<PlayerSeason> PlayerSeasons => Set<PlayerSeason>();
    public DbSet<GroupBasket> Baskets => Set<GroupBasket>();
    public DbSet<BasketSeason> BasketSeasons => Set<BasketSeason>();
    public DbSet<GroupLeague> Leagues => Set<GroupLeague>();
    public DbSet<LeagueSeason> LeagueSeasons => Set<LeagueSeason>();
    public DbSet<LeagueBasketSeason> LeagueBasketSeasons => Set<LeagueBasketSeason>();
    public DbSet<FantasyTeamSeason> TeamSeasons => Set<FantasyTeamSeason>();
    public DbSet<TeamOwnership> TeamOwnerships => Set<TeamOwnership>();
    public DbSet<CoreMigrationState> CoreMigrationStates => Set<CoreMigrationState>();
    public DbSet<LegacyIdentityMap> LegacyIdentityMaps => Set<LegacyIdentityMap>();
    public DbSet<CoreImportCheckpoint> CoreImportCheckpoints => Set<CoreImportCheckpoint>();
    public DbSet<AccountPreferences> AccountPreferences => Set<AccountPreferences>();
    public DbSet<AccountSelection> AccountSelections => Set<AccountSelection>();
    public DbSet<UnresolvedAccountSelection> UnresolvedAccountSelections => Set<UnresolvedAccountSelection>();
    public DbSet<LeagueRules> LeagueRules => Set<LeagueRules>();
    public DbSet<OAuthRecord> OAuthRecords => Set<OAuthRecord>();
    public DbSet<TeamOwnershipChange> TeamOwnershipChanges => Set<TeamOwnershipChange>();
    public DbSet<RosterSnapshot> RosterSnapshots => Set<RosterSnapshot>();
    public DbSet<FootballMatchday> FootballMatchdays => Set<FootballMatchday>();
    public DbSet<FootballMatch> FootballMatches => Set<FootballMatch>();
    public DbSet<GroupLeagueBasket> GroupLeagueBaskets => Set<GroupLeagueBasket>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<FootballDataset> FootballDatasets => Set<FootballDataset>();
    public DbSet<FootballVote> FootballVotes => Set<FootballVote>();
    public DbSet<FootballChance> FootballChances => Set<FootballChance>();
    public DbSet<FootballPlayerStatistic> FootballPlayerStatistics => Set<FootballPlayerStatistic>();
    public DbSet<FootballClubRank> FootballClubRanks => Set<FootballClubRank>();
    public DbSet<FantasyCalendar> FantasyCalendars => Set<FantasyCalendar>();
    public DbSet<FantasyMatch> FantasyMatches => Set<FantasyMatch>();
    public DbSet<FantasyStandingSnapshot> FantasyStandingSnapshots => Set<FantasyStandingSnapshot>();
    public DbSet<PlayedMatchSnapshot> PlayedMatchSnapshots => Set<PlayedMatchSnapshot>();
    public DbSet<OpeningStandingSnapshot> OpeningStandingSnapshots => Set<OpeningStandingSnapshot>();
    public DbSet<LeaguePlayerOrder> LeaguePlayerOrders => Set<LeaguePlayerOrder>();
    public DbSet<MarketBook> MarketBooks => Set<MarketBook>();
    public DbSet<MarketTrade> MarketTrades => Set<MarketTrade>();
    public DbSet<MarketTradeEvent> MarketTradeEvents => Set<MarketTradeEvent>();
    public DbSet<AuctionSession> AuctionSessions => Set<AuctionSession>();
    public DbSet<AuctionPresence> AuctionPresence => Set<AuctionPresence>();
    public DbSet<AuctionRosterChange> AuctionRosterChanges => Set<AuctionRosterChange>();
    public DbSet<RankCreditConfirmation> RankCreditConfirmations => Set<RankCreditConfirmation>();
    public DbSet<HallArchive> HallArchives => Set<HallArchive>();
    public DbSet<CardLibrary> CardLibraries => Set<CardLibrary>();
    public DbSet<RuleCard> RuleCards => Set<RuleCard>();
    public DbSet<UserPushPreferences> UserPushPreferences => Set<UserPushPreferences>();
    public DbSet<PushDeviceBinding> PushDeviceBindings => Set<PushDeviceBinding>();
    public DbSet<PushDeviceOwnershipEvent> PushDeviceOwnershipEvents => Set<PushDeviceOwnershipEvent>();
    public DbSet<PushDayCheckpoint> PushDayCheckpoints => Set<PushDayCheckpoint>();
    public DbSet<LegacyPushActiveDay> LegacyPushActiveDays => Set<LegacyPushActiveDay>();
    public DbSet<PushDelivery> PushDeliveries => Set<PushDelivery>();
    public DbSet<PushDeliveryScopeConfirmation> PushDeliveryScopeConfirmations => Set<PushDeliveryScopeConfirmation>();
    public DbSet<PushFormationChangeReceipt> PushFormationChangeReceipts => Set<PushFormationChangeReceipt>();
    public DbSet<NewsEdition> NewsEditions => Set<NewsEdition>();
    public DbSet<TeamAdvice> TeamAdvice => Set<TeamAdvice>();
    public DbSet<ArchivedOperationalLog> ArchivedOperationalLogs => Set<ArchivedOperationalLog>();
    public DbSet<SqlJobState> SqlJobStates => Set<SqlJobState>();
    public DbSet<SqlJobRun> SqlJobRuns => Set<SqlJobRun>();
    public DbSet<HallRankContribution> HallRankContributions => Set<HallRankContribution>();
    public DbSet<HallBaselineConfirmation> HallBaselineConfirmations => Set<HallBaselineConfirmation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FantasoccerDbContext).Assembly);
    }
}
