using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed class SeasonPlayerKeyFormatTests
{
    [Theory]
    [InlineData("Mario Rossi", 11, "11:mariorossi")]
    [InlineData("D'Angelo", 12, "12:dangelo")]
    [InlineData("Named&#x27;", 15, "15:namedx")]
    public void Readable_season_keys_retain_the_legacy_normalization(string name, int year, string expected)
        => Assert.Equal(expected, KeyUtils.GetSeasonPlayerKey(name, year));

    [Fact]
    public void A_season_key_cannot_be_invented_for_an_empty_name_or_invalid_year()
    {
        Assert.Throws<ArgumentException>(() => KeyUtils.GetSeasonPlayerKey("", 15));
        Assert.Throws<ArgumentException>(() => KeyUtils.GetSeasonPlayerKey("12345", 15));
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyUtils.GetSeasonPlayerKey("Player", 0));
    }
}

public sealed class SeasonPlayerKeyTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public async Task A_generated_indexed_key_is_unique_by_year_and_normalized_name_not_by_club_role_or_GUID()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var first = new PlayerSeason { Player = new() { Name = "Provider player" }, NormalizedName = "providerplayer",
            Season = new() { SeasonIndex = 15, StartYear = 2026 } };
        var second = new PlayerSeason { Player = new() { Name = "Provider player" }, NormalizedName = "providerplayer",
            Season = new() { SeasonIndex = 16, StartYear = 2027 } };
        database.PlayerSeasons.AddRange(first, second); await database.SaveChangesAsync();
        Assert.Equal("15:providerplayer", first.PlayerKey); Assert.Equal("16:providerplayer", second.PlayerKey);
        Assert.Equal(first.PlayerId, (await database.PlayerSeasons.SingleAsync(x => x.PlayerKey == "15:providerplayer")).PlayerId);
        var index = await database.Database.SqlQueryRaw<string>("""
            SELECT indexdef AS "Value" FROM pg_indexes WHERE tablename = 'PlayerSeasons' AND indexname = 'IX_PlayerSeasons_PlayerKey'
            """).SingleAsync();
        Assert.Contains("CREATE UNIQUE INDEX", index);
        database.PlayerSeasons.Add(new() { Player = new() { Name = "PROVIDER-PLAYER" }, NormalizedName = "providerplayer", SeasonIndex = 15 });
        var conflict = Assert.IsType<PostgresException>((await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync())).InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, conflict.SqlState); Assert.Equal("IX_PlayerSeasons_PlayerKey", conflict.ConstraintName);
    }

    [PostgreSqlFact]
    public async Task Existing_ID_foreign_keys_reject_a_player_from_another_year_even_when_the_global_ID_exists()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var person = new FootballPlayer { Name = "Traceable player" };
        var club = new FootballClub { Name = "Traceable club" };
        database.Seasons.AddRange(new() { SeasonIndex = 15, StartYear = 2026 }, new() { SeasonIndex = 16, StartYear = 2027 });
        database.PlayerSeasons.Add(new() { Player = person, Club = club, SeasonIndex = 15, NormalizedName = "traceableplayer" });
        await database.SaveChangesAsync();
        database.FootballVotes.Add(new() { Player = person, Club = club, SeasonIndex = 16, Day = 1, Source = FootballVoteSource.Official, Value = 6 });
        var conflict = Assert.IsType<PostgresException>((await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync())).InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, conflict.SqlState);
        Assert.Contains("PlayerSeasons", conflict.ConstraintName!);
    }

    [PostgreSqlFact]
    public async Task Empty_or_noncanonical_normalized_keys_are_rejected_instead_of_creating_fake_player_keys()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        database.PlayerSeasons.Add(new() { Player = new() { Name = "Invalid key player" }, NormalizedName = "",
            Season = new() { SeasonIndex = 15, StartYear = 2026 } });
        var conflict = Assert.IsType<PostgresException>((await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync())).InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, conflict.SqlState);
    }
}
