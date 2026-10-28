using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed class SeasonPlayerKeyFormatTests
{
    [Theory]
    [InlineData("Mario Rossi", 11, "11/mariorossi")]
    [InlineData("D'Angelo", 12, "12/dangelo")]
    [InlineData("Named&#x27;", 15, "15/namedx")]
    public void Readable_season_keys_retain_the_legacy_normalization(string name, int year, string expected)
        => Assert.Equal(expected, KeyUtils.GetSeasonPlayerKey(name, year));

    [Fact]
    public void A_season_key_cannot_be_invented_for_an_empty_name_or_invalid_year()
    {
        Assert.Throws<ArgumentException>(() => KeyUtils.GetSeasonPlayerKey("", 15));
        Assert.Throws<ArgumentException>(() => KeyUtils.GetSeasonPlayerKey("12345", 15));
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyUtils.GetSeasonPlayerKey("Player", 0));
    }

    [Fact]
    public void Club_keys_use_three_letter_codes_and_player_keys_remain_stable_when_display_names_change()
    {
        Assert.Equal("nap", KeyUtils.GetClubKey("NAP", "Napoli"));
        Assert.Equal("nap", KeyUtils.GetClubKey(null, "NAP"));
        Assert.Equal("nap", KeyUtils.GetClubKey(null, "Napoli"));
        Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey("N4P", "Napoli"));
        var legacy = new RealTeam { Name = "Napoli" };
        Assert.Equal("nap", legacy.Abbreviation); // Existing display behavior is unchanged.
        Assert.Null(legacy.ExplicitAbbreviation); // But no provider code was supplied.
        Assert.Equal("nap", KeyUtils.GetClubKey(legacy.ExplicitAbbreviation, legacy.Name));
        legacy.Abbreviation = "NAP";
        Assert.Equal("nap", KeyUtils.GetClubKey(legacy.ExplicitAbbreviation, legacy.Name));
        var player = new FootballPlayer { Name = "Provider player" };
        Assert.Equal("providerplayer", player.Id);
        player.Name = "Updated display label";
        Assert.Equal("providerplayer", player.Id);
        Assert.Null(player.LegacyId);
        var club = new FootballClub { Name = "Napoli", Abbreviation = "NAP" };
        Assert.Equal("nap", club.Id);
        club.Name = "Updated club label"; club.Abbreviation = "other";
        Assert.Equal("nap", club.Id);
        Assert.Null(club.LegacyId);
    }

    [Fact]
    public void Club_codes_use_a_finite_verified_catalogue_without_prefixes_fuzzy_names_or_guessed_aliases()
    {
        Assert.Equal(39, FantacalcioClubCodes.Names.Count);
        Assert.Equal(39, FantacalcioClubCodes.Names.Values.Distinct().Count());
        foreach (var (name, code) in FantacalcioClubCodes.Names)
        {
            Assert.Matches("^[a-z]{3}$", code);
            Assert.Equal(code, KeyUtils.GetClubKey(null, name));
            Assert.Equal(code, KeyUtils.GetClubKey("", name));
            Assert.Equal(code, KeyUtils.GetClubKey(null, " " + name.ToUpperInvariant() + " "));
            Assert.Equal(code, KeyUtils.GetClubKey(code.ToUpperInvariant(), name));
        }
        Assert.Equal("fio", KeyUtils.GetClubKey(null, "Fiorentina"));
        foreach (var unknown in new[] { "Fiorentina Primavera", "Fiorent", "Unknown fixture club", "Hellas" })
            Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey(null, unknown));
        Assert.Equal("abc", KeyUtils.GetClubKey("ABC", "Unknown fixture club"));
    }

    [Theory]
    [InlineData("Fiorentina", "NAP")]
    [InlineData("Napoli", "FIO")]
    [InlineData("Roma", "MIL")]
    public void Explicit_codes_that_contradict_verified_provider_names_are_refused_not_overwritten(string name, string code)
        => Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey(code, name));

    [Fact]
    public void Documented_exact_club_aliases_share_canonical_keys_without_enabling_prefix_or_fuzzy_matching()
    {
        Assert.Equal(4, FantacalcioClubCodes.Aliases.Count);
        foreach (var (alias, canonical) in FantacalcioClubCodes.Aliases)
        {
            Assert.False(FantacalcioClubCodes.Names.ContainsKey(alias));
            var code = FantacalcioClubCodes.Names[canonical];
            Assert.Equal(code, KeyUtils.GetClubKey(null, alias));
            Assert.Equal(code, KeyUtils.GetClubKey(null, " " + alias.ToUpperInvariant() + " "));
            Assert.Equal(code, KeyUtils.GetClubKey(code.ToUpperInvariant(), alias));
            Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey("NAP", alias));
        }
        Assert.Equal("juv", KeyUtils.GetClubKey(null, "Juve"));
        Assert.Equal("ver", KeyUtils.GetClubKey(null, "HELLAS VERONA"));
        Assert.Equal("Verona", FantacalcioClubCodes.CanonicalName("ver", "Hellas verona"));
        // Official club names seen in the 2024/25 legacy calendar: stored as the
        // canonical code and name, never as the long label.
        Assert.Equal(("com", "Como"), (KeyUtils.GetClubKey(null, "Como 1907"), FantacalcioClubCodes.CanonicalName("com", "Como 1907")));
        Assert.Equal(("ven", "Venezia"), (KeyUtils.GetClubKey(null, "VENEZIA FC"), FantacalcioClubCodes.CanonicalName("ven", "Venezia FC")));
        Assert.Equal("com", KeyUtils.GetClubKey("com", "Como 1907"));
        Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey("cag", "Como 1907"));
        foreach (var unknown in new[] { "Juve Next Gen", "Juve Women", "Juvent", "Juvé", "Hellas Verona Primavera", "Hellas Ver", "Como 1907 Women", "Venezia Mestre" })
            Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey(null, unknown));
    }

    [Fact]
    public void Provider_code_variants_resolve_only_together_with_the_verified_club_name()
    {
        // The 2024/25 calendar used both "Hellas verona/hel" and "Hellas verona/ver";
        // the 2026/27 provider calendar labels Monza "MONZ" in all 38 matchdays.
        Assert.Equal(new[] { ("hel", "ver"), ("monz", "mon") }, FantacalcioClubCodes.CodeVariants.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)));
        foreach (var name in new[] { "Hellas verona", "HELLAS VERONA", "Verona" })
        {
            Assert.Equal("ver", KeyUtils.GetClubKey("hel", name));
            Assert.Equal("ver", KeyUtils.GetClubKey("HEL", name));
            Assert.Equal("ver", KeyUtils.GetClubKey("ver", name));
        }
        foreach (var name in new[] { "Monza", "MONZA", " monza " })
        {
            Assert.Equal("mon", KeyUtils.GetClubKey("monz", name));
            Assert.Equal("mon", KeyUtils.GetClubKey("MONZ", name));
            Assert.Equal("mon", KeyUtils.GetClubKey("MON", name));
        }
        // A variant never moves another verified club, an unverified name keeps
        // its own valid explicit code, and labels are never trimmed or cut.
        foreach (var name in new[] { "Juventus", "Napoli", "Juve" })
        {
            Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey("hel", name));
            Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey("monz", name));
        }
        Assert.Equal("hel", KeyUtils.GetClubKey("hel", "Unknown fixture club"));
        Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey("monz", "Unknown fixture club"));
        foreach (var label in new[] { " monz", "monz ", "mon z", "monza", "mnz" })
            Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey(label, "Monza"));
    }

    [Theory]
    [InlineData("Juve", null)]
    [InlineData("JUVENTUS", null)]
    [InlineData("juventus", "JUV")]
    [InlineData("juv", null)]
    [InlineData("JUV", null)]
    [InlineData(" JuVe ", null)]
    public void Club_official_name_is_determined_by_the_readable_code_not_first_seen_spelling(string sourceName, string? abbreviation)
    {
        var code = KeyUtils.GetClubKey(abbreviation, sourceName);
        Assert.Equal("juv", code); Assert.Equal("Juventus", FantacalcioClubCodes.CanonicalName(code, sourceName));
        Assert.True(FantacalcioClubCodes.TryGetName(code.ToUpperInvariant(), out var name)); Assert.Equal("Juventus", name);
        foreach (var (canonical, key) in FantacalcioClubCodes.Names)
            Assert.Equal(canonical, FantacalcioClubCodes.CanonicalName(key, "ignored source label"));
    }

    [Theory]
    [InlineData("Catania", "cat")]
    [InlineData("Siena", "sie")]
    [InlineData("Livorno", "liv")]
    [InlineData("Cesena", "ces")]
    public void Early_legacy_seasons_use_verified_historical_codes_not_unverified_aliases(string name, string code)
    {
        Assert.Equal(code, KeyUtils.GetClubKey(null, name));
        Assert.Equal(code, KeyUtils.GetClubKey("", name));
        Assert.Equal(code, KeyUtils.GetClubKey(code.ToUpperInvariant(), name));
        Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey("xyz", name));
        Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey(null, name + " Primavera"));
        if (name == "Siena") Assert.Throws<ArgumentException>(() => KeyUtils.GetClubKey("SNA", name));
    }
}

public sealed class SeasonPlayerKeyTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public async Task Global_player_key_and_composite_year_key_are_real_primary_keys_not_generated_yearly_strings()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var first = new PlayerSeason { Player = new() { Name = "Provider player" }, NormalizedName = "providerplayer",
            Season = new() { SeasonIndex = 15, StartYear = 2026 } };
        var second = new PlayerSeason { Player = first.Player, NormalizedName = "providerplayer",
            Season = new() { SeasonIndex = 16, StartYear = 2027 } };
        database.PlayerSeasons.AddRange(first, second); await database.SaveChangesAsync();
        Assert.Equal("providerplayer", first.PlayerKey); Assert.Equal(first.PlayerId, second.PlayerId);
        Assert.Equal(first.PlayerId, (await database.PlayerSeasons.SingleAsync(x => x.PlayerId == "providerplayer" && x.SeasonIndex == 15)).PlayerId);
        var index = await database.Database.SqlQueryRaw<string>("""
            SELECT indexdef AS "Value" FROM pg_indexes WHERE tablename = 'PlayerSeasons' AND indexname = 'PK_PlayerSeasons'
            """).SingleAsync();
        Assert.Contains("CREATE UNIQUE INDEX", index);
        Assert.Contains("\"PlayerKey\", \"SeasonIndex\"", index);
        Assert.Equal(0, await database.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM information_schema.columns
            WHERE table_schema='public' AND table_name='PlayerSeasons' AND
                (column_name IN ('NormalizedName','YearlyPlayerKey') OR is_generated='ALWAYS')
            """).SingleAsync());
        database.ChangeTracker.Clear();
        database.PlayerSeasons.Add(new() { PlayerId = "providerplayer", SeasonIndex = 15 });
        var conflict = Assert.IsType<PostgresException>((await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync())).InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, conflict.SqlState); Assert.Equal("PK_PlayerSeasons", conflict.ConstraintName);
    }

    [PostgreSqlFact]
    public async Task Composite_natural_foreign_keys_reject_a_player_from_another_year_even_when_the_global_key_exists()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var person = new FootballPlayer { Name = "Traceable player" };
        var club = new FootballClub { Name = "Traceable club", Abbreviation = "trc" };
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
        database.PlayerSeasons.Add(new() { Player = new() { Id = "", Name = "Invalid key player" }, NormalizedName = "",
            Season = new() { SeasonIndex = 15, StartYear = 2026 } });
        var conflict = Assert.IsType<PostgresException>((await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync())).InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, conflict.SqlState);
    }
}
