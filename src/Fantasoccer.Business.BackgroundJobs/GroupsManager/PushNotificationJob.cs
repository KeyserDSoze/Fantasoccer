using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Timers;
using Fantasoccer.Business;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Engine.GroupsManager
{
    public class PushNotificationJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly ITeamCalculator _teamCalculator;
        private readonly IRepository<Group, string> _groupRepository;
        private readonly IRepository<LiveGroup, string> _liveGroupRepository;
        private readonly IRepository<Team, TeamDayKey> _teamDayRepository;
        private readonly IRepository<RealCalendar, int> _serieACalendar;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IRepository<Calendar, LeagueKey> _calendarRepository;
        private readonly IRepository<PushNotificationSettings, string> _pushNotificationRepository;
        private readonly PushNotificationStateStore _state;
        private CancellationToken _executionCancellationToken;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _liveVoteRepository;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _notificationVoteRepository;
        private readonly IPushNotificationLock _stateLock;
        private readonly PushNotificationSettingsService _settings;
        private readonly TimeProvider _timeProvider;
        public PushNotificationJob(IFantasoccerLogger logger,
            ITeamCalculator teamCalculator,
            IRepository<Group, string> groupRepository,
            IRepository<LiveGroup, string> liveGroupRepository,
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory,
            IRepository<Team, TeamDayKey> teamDayRepository,
            IRepository<RealCalendar, int> serieACalendar,
            IPushNotificationService pushNotificationService,
            IRepository<Calendar, LeagueKey> calendarRepository,
            IRepository<PushNotificationSettings, string> pushNotificationRepository,
            IRepository<PushNotificationSent, YearDayKey> pushNotificationSentRepository,
            IPushNotificationLock? stateLock = null,
            TimeProvider? timeProvider = null)
        {
            _logger = logger;
            _teamCalculator = teamCalculator;
            _groupRepository = groupRepository;
            _liveGroupRepository = liveGroupRepository;
            _teamDayRepository = teamDayRepository;
            _serieACalendar = serieACalendar;
            _pushNotificationService = pushNotificationService;
            _calendarRepository = calendarRepository;
            _pushNotificationRepository = pushNotificationRepository;
            _state = new(pushNotificationSentRepository);
            _liveVoteRepository = factory.Create("live")!;
            _notificationVoteRepository = factory.Create("notification")!;
            _stateLock = stateLock ?? new InMemoryPushNotificationLock();
            _settings = new(pushNotificationRepository, _stateLock);
            _timeProvider = timeProvider ?? TimeProvider.System;
        }
        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            await using var held = await _stateLock.AcquireAsync("push-job", cancellationToken);
            cancellationToken = held.CancellationToken;
            _executionCancellationToken = cancellationToken;
            await DispatchPendingAsync(cancellationToken);
            var actualYear = FantacalcioTime.ActualYear;
            var realCalendar = await _serieACalendar.GetAsync(actualYear).NoContext();
            if (realCalendar != null)
            {
                var liveDay = realCalendar.LiveDay;
                if (liveDay != default)
                {
                    var yearDayKey = new YearDayKey
                    {
                        Year = actualYear,
                        Day = liveDay.SerieADay
                    };
                    var liveVotes = await _liveVoteRepository.GetAsync(yearDayKey);
                    if (liveVotes != null)
                    {
                        var sentRecord = await GetRecordAsync(yearDayKey, cancellationToken);
                        var storedNotificationVotes = sentRecord.MatchEventVotes ?? await _notificationVoteRepository.GetAsync(yearDayKey);
                        var notificationVotes = storedNotificationVotes ?? new VotedRealPlayerWrapper() { Players = new() };
                        var notificationsToSend = new List<NotificationToSend>();
                        foreach (var player in liveVotes.Players)
                        {
                            var currentPlayer = notificationVotes.Players.FirstOrDefault(x => x.Key == player.Key) ?? VotedRealPlayer.Empty;
                            var notificationToSend = GetNotification(player, currentPlayer);
                            if (notificationToSend != null)
                                notificationsToSend.Add(notificationToSend);
                        }
                        if (notificationsToSend.Any())
                        {
                            sentRecord.MatchEventSequence++;
                            var notificationUsers = await _pushNotificationRepository.Where(x => x.MatchEvents || x.OpponentMatchEvents).ToListAsync();
                            foreach (var group in await _liveGroupRepository.ToListAsync())
                            {
                                var teams = new List<Team>();
                                var teamBaskets = new Dictionary<Team, string>();
                                var groupKey = group.Key;
                                var groupValue = await _groupRepository.GetAsync(groupKey!);
                                var groupName = string.IsNullOrWhiteSpace(groupValue?.Name) ? groupKey! : groupValue.Name;
                                if (groupValue == null) continue;
                                foreach (var basket in groupValue.Baskets ?? [])
                                {
                                    var currentYear = basket.Years?.FirstOrDefault(x => x.Year == actualYear);
                                    if (currentYear != null)
                                    {
                                        foreach (var team in currentYear.Teams ?? [])
                                        {
                                            var currentTeam = await _teamDayRepository.GetAsync(new TeamDayKey
                                            {
                                                BasketId = basket.Id,
                                                Day = liveDay.SerieADay,
                                                Email = team.Owner,
                                                Group = groupKey!,
                                                Year = actualYear
                                            });
                                            if (currentTeam != null)
                                            {
                                                currentTeam = currentTeam.Clone();
                                                currentTeam.AdditionalOwners = (currentTeam.AdditionalOwners ?? [])
                                                    .Concat(team.AdditionalOwners ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                                                teams.Add(currentTeam);
                                                teamBaskets[currentTeam] = basket.Id;
                                            }
                                        }
                                    }
                                }
                                var currentOpponents = new Dictionary<string, List<OpponentNotification>>(StringComparer.OrdinalIgnoreCase);
                                foreach (var league in group.Value?.Leagues ?? [])
                                {
                                    var currentCalendar = await _calendarRepository.GetAsync(new LeagueKey
                                    {
                                        Group = groupKey!,
                                        League = league.Id,
                                        Year = actualYear
                                    });
                                    var currentGames = currentCalendar?.Rounds?.SelectMany(x => x.Value ?? [])
                                        .Where(x => x.SerieADay == liveDay.SerieADay).SelectMany(x => x.Games ?? []);
                                    if (currentGames != null)
                                    {
                                        foreach (var game in currentGames.Where(g => g.Result?.IsCancelled != true))
                                        {
                                            AddOpponent(game.HomeOwner, game.AwayOwner);
                                            AddOpponent(game.AwayOwner, game.HomeOwner);

                                            void AddOpponent(string owner, string opponent)
                                            {
                                                if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(opponent)) return;
                                                if (!currentOpponents.ContainsKey(owner))
                                                    currentOpponents.Add(owner, []);
                                                currentOpponents[owner].Add(new OpponentNotification
                                                {
                                                    LeagueId = league.Id,
                                                    League = league.Name,
                                                    BasketIds = groupValue.Leagues?.FirstOrDefault(l => l.Id == league.Id)?.BasketsId ?? [],
                                                    GameId = game.Id,
                                                    Opponent = opponent
                                                });
                                            }
                                        }
                                    }
                                }
                                foreach (var notification in notificationsToSend)
                                {
                                    foreach (var team in teams.Where(x => x.ActivePlayers.Any(player => player.Key == notification.PlayerKey)))
                                    {
                                        QueueTeam(team, notification, $"[{groupName}]", null, PushNotificationKind.MatchEvents, $"own|{groupKey}");
                                        
                                        if (currentOpponents.ContainsKey(team.Owner))
                                        {
                                            var opponents = currentOpponents[team.Owner];
                                            foreach (var opponent in opponents)
                                            {
                                                if (!opponent.BasketIds.Contains(teamBaskets[team], StringComparer.OrdinalIgnoreCase)) continue;
                                                foreach (var opponentTeam in teams.Where(x => string.Equals(x.Owner, opponent.Opponent, StringComparison.OrdinalIgnoreCase) &&
                                                    opponent.BasketIds.Contains(teamBaskets[x], StringComparer.OrdinalIgnoreCase)))
                                                {
                                                    QueueTeam(opponentTeam, notification, $"[{groupName} - {opponent.League}]", $"({team.Name.Trim()})",
                                                        PushNotificationKind.OpponentMatchEvents, $"opponent|{groupKey}|{opponent.LeagueId}|{opponent.GameId}|{team.Owner}|{opponent.Opponent}");
                                                }
                                            }
                                        }
                                    }
                                }

                                void QueueTeam(Team currentTeam, NotificationToSend notification, string prefix, string? postfix, PushNotificationKind kind, string scope)
                                {
                                    var emails = new[] { currentTeam.Owner }.Concat(currentTeam.AdditionalOwners ?? [])
                                        .Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase);
                                    foreach (var email in emails)
                                    {
                                        var notificationUser = notificationUsers.FirstOrDefault(x => string.Equals(x.Key, email, StringComparison.OrdinalIgnoreCase));
                                        if (notificationUser?.Value?.IsEnabled(kind) == true)
                                        {
                                            var body = $"{prefix} {notification.Body}";
                                            if (!string.IsNullOrWhiteSpace(postfix)) body = $"{body} {postfix}";
                                            QueueNotification(sentRecord, yearDayKey, $"{scope}|{sentRecord.MatchEventSequence}|{notification.PlayerKey}", notificationUser.Key!, kind,
                                                new Notification
                                                {
                                                    Body = body,
                                                    Title = "FantaZone",
                                                    Icon = "https://fanta.zone/icon.png",
                                                    Badge = "https://fanta.zone/adaptive-icon.png",
                                                    Data = new NotificationData { Uri = "https://fanta.zone/live" }
                                                });
                                        }
                                    }
                                }
                            }
                        }
                        sentRecord.MatchEventVotes = JsonSerializer.Deserialize<VotedRealPlayerWrapper>(JsonSerializer.Serialize(liveVotes));
                        await SaveRecordAsync(yearDayKey, sentRecord, cancellationToken);
                        // Keep the legacy checkpoint readable during migration; dispatch relies
                        // exclusively on the checkpoint committed together with the outbox.
                        if (await _notificationVoteRepository.GetAsync(yearDayKey) == null)
                            await _notificationVoteRepository.InsertAsync(yearDayKey, liveVotes);
                        else
                            await _notificationVoteRepository.UpdateAsync(yearDayKey, liveVotes);
                    }
                    await SendLiveFormationChangeNotificationsAsync(actualYear, liveDay.SerieADay);
                }
                if (realCalendar.LiveDay is RealDay liveFormationDay && realCalendar.LiveFormationDeadline is DateTime formationDeadline)
                    await SendLiveChangesReminderAsync(actualYear, liveFormationDay.SerieADay, formationDeadline);
                if (realCalendar.NextDay?.Games.Any(t => t.Date != null && Math.Abs(FantacalcioTime.ItalyNow.Subtract(t.Date.Value).TotalHours) < 8) == true)
                {
                    var yearlyDayKey = new YearDayKey
                    {
                        Day = realCalendar.NextDay.SerieADay,
                        Year = actualYear
                    };
                    var reminderRecord = await GetRecordAsync(yearlyDayKey, cancellationToken);
                    if (!reminderRecord.DeploymentReminder)
                    {
                        var notificationUsers = await _pushNotificationRepository.Where(x => x.DeploymentReminder).ToListAsync();
                        foreach (var user in notificationUsers)
                        {
                            QueueNotification(reminderRecord, yearlyDayKey, "deployment", user.Key!, PushNotificationKind.DeploymentReminder, new Notification
                                {
                                    Body = "Ricorda di schierare la formazione!",
                                    Title = "FantaZone",
                                    Icon = "https://fanta.zone/icon.png",
                                    Badge = "https://fanta.zone/adaptive-icon.png",
                                    Tag = $"deployment-reminder-{yearlyDayKey.Year}-{yearlyDayKey.Day}",
                                    Data = new NotificationData
                                    {
                                        Uri = "https://fanta.zone/edit"
                                    }
                                });
                        }
                        reminderRecord.DeploymentReminder = true;
                        await SaveRecordAsync(yearlyDayKey, reminderRecord, cancellationToken);
                    }
                }
                if (realCalendar.LastDay is RealDay lastDay)
                    await SendEndDayNotificationsAsync(actualYear, lastDay.SerieADay);
            }
            await DispatchPendingAsync(cancellationToken);
        }
        private async Task SendEndDayNotificationsAsync(int actualYear, int serieADay)
        {
            var endDayKey = new YearDayKey { Year = actualYear, Day = serieADay };
            var alreadySent = await GetRecordAsync(endDayKey);
            if (alreadySent.EndDay)
                return;
            var endDayUsers = await _pushNotificationRepository.Where(x => x.EndDay).ToListAsync();
            var entries = new List<EndDayEntry>();
            var everythingReady = true;
            foreach (var group in await _groupRepository.ToListAsEntityAsync())
            {
                foreach (var league in group.Leagues?.Where(x => x.Years?.Exists(t => t.Year == actualYear) == true) ?? [])
                {
                    var calendar = await _calendarRepository.GetAsync(new LeagueKey
                    {
                        Group = group.Id,
                        League = league.Id,
                        Year = actualYear
                    });
                    var games = calendar?.Rounds?.SelectMany(x => x.Value ?? []).Where(x => x.SerieADay == serieADay).SelectMany(x => x.Games ?? []);
                    if (games == null)
                        continue;
                    var playableGames = games.Where(x => x.Result?.IsCancelled != true).ToList();
                    if (playableGames.Count == 0)
                        continue;
                    if (playableGames.Any(x => x.HasNoResult))
                    {
                        everythingReady = false;
                        continue;
                    }
                    var ownerEmails = BuildOwnerEmails(group, league, actualYear);
                    entries.Add(new EndDayEntry { GroupId = group.Id, League = league, Games = playableGames, OwnerEmails = ownerEmails });
                }
            }
            if (!everythingReady)
                return;
            if (endDayUsers.Count > 0 && entries.Count > 0)
            {
                foreach (var entry in entries)
                {
                    foreach (var game in entry.Games)
                    {
                        QueueEndDay(entry, game, game.HomeOwner, true);
                        QueueEndDay(entry, game, game.AwayOwner, false);
                    }
                }
                void QueueEndDay(EndDayEntry entry, CalendarGame game, string? owner, bool isHome)
                {
                    if (string.IsNullOrWhiteSpace(owner) || !entry.OwnerEmails.TryGetValue(owner, out var emails))
                        return;
                    foreach (var email in emails.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var user = endDayUsers.FirstOrDefault(x => string.Equals(x.Key, email, StringComparison.OrdinalIgnoreCase));
                        if (user?.Value == null)
                            continue;
                        QueueNotification(alreadySent, endDayKey, $"end|{entry.GroupId}|{entry.League.Id}|{game.Id}|{owner}",
                            user.Key!, PushNotificationKind.EndDay, BuildEndDayNotification(entry.League, serieADay, game, isHome));
                    }
                }
            }
            var sentRecord = alreadySent;
            sentRecord.EndDay = true;
            await SaveRecordAsync(endDayKey, sentRecord);
        }
        private async Task SendLiveChangesReminderAsync(int actualYear, int serieADay, DateTime deadline)
        {
            var remaining = deadline - FantacalcioTime.ItalyNow;
            if (remaining <= TimeSpan.Zero || remaining > TimeSpan.FromMinutes(120))
                return;
            var key = new YearDayKey { Year = actualYear, Day = serieADay };
            var alreadySent = await GetRecordAsync(key);
            if (alreadySent.LiveChangesReminder)
                return;
            var reminderUsers = await _pushNotificationRepository.Where(x => x.LiveChangesReminder).ToListAsync();
            if (reminderUsers.Count > 0)
            {
                var deadlineText = deadline.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                foreach (var group in await _groupRepository.ToListAsEntityAsync())
                {
                    foreach (var league in group.Leagues?.Where(x => x.Years?.Exists(t => t.Year == actualYear) == true) ?? [])
                    {
                        var annualLeague = league.Years!.First(x => x.Year == actualYear);
                        var maxChanges = annualLeague.Settings?.LiveFormationChanges ?? 0;
                        if (maxChanges <= 0)
                            continue;
                        var basketIds = league.BasketsId?.ToHashSet() ?? [];
                        foreach (var basket in group.Baskets?.Where(x => basketIds.Contains(x.Id)) ?? [])
                        {
                            foreach (var year in basket.Years?.Where(x => x.Year == actualYear) ?? [])
                            {
                                foreach (var team in year.Teams ?? [])
                                {
                                    if (string.IsNullOrWhiteSpace(team.Owner))
                                        continue;
                                    var teamDay = await _teamDayRepository.GetAsync(new TeamDayKey
                                    {
                                        Group = group.Id,
                                        BasketId = basket.Id,
                                        Email = team.Owner,
                                        Year = actualYear,
                                        Day = serieADay
                                    });
                                    var remainingChanges = maxChanges - (teamDay?.FormationChanges ?? 0);
                                    if (remainingChanges <= 0)
                                        continue;
                                    List<string> emails = team.AdditionalOwners != null
                                        ? [team.Owner, .. team.AdditionalOwners]
                                        : [team.Owner];
                                    foreach (var email in emails.Distinct(StringComparer.OrdinalIgnoreCase))
                                    {
                                        var user = reminderUsers.FirstOrDefault(x => string.Equals(x.Key, email, StringComparison.OrdinalIgnoreCase));
                                        if (user?.Value == null)
                                            continue;
                                        QueueNotification(alreadySent, key, $"changes|{group.Id}|{league.Id}|{basket.Id}|{team.Owner}", user.Key!,
                                            PushNotificationKind.LiveChangesReminder, BuildLiveChangesNotification(league, serieADay, remainingChanges, deadlineText),
                                            _timeProvider.GetUtcNow().Add(remaining));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            var record = alreadySent;
            record.LiveChangesReminder = true;
            await SaveRecordAsync(key, record);
        }

        private async Task SendLiveFormationChangeNotificationsAsync(int actualYear, int serieADay)
        {
            var key = new YearDayKey { Year = actualYear, Day = serieADay };
            var sentRecord = await GetRecordAsync(key);
            sentRecord.LiveFormationChangeNotifications ??= [];
            var processedChanges = sentRecord.LiveFormationChangeNotifications.ToHashSet(StringComparer.Ordinal);
            var notificationUsers = await _pushNotificationRepository.Where(x => x.LiveFormationChangeEvents).ToListAsync();
            var hasNewChange = false;

            foreach (var group in await _groupRepository.ToListAsEntityAsync())
            {
                if (string.IsNullOrWhiteSpace(group.Id))
                    continue;
                var groupName = string.IsNullOrWhiteSpace(group.Name) ? group.Id : group.Name;
                foreach (var basket in group.Baskets ?? [])
                {
                    if (string.IsNullOrWhiteSpace(basket.Id))
                        continue;
                    var basketYear = basket.Years?.FirstOrDefault(x => x.Year == actualYear);
                    foreach (var annualTeam in basketYear?.Teams ?? [])
                    {
                        if (string.IsNullOrWhiteSpace(annualTeam.Owner))
                            continue;
                        var team = await _teamDayRepository.GetAsync(new TeamDayKey
                        {
                            Group = group.Id,
                            BasketId = basket.Id,
                            Email = annualTeam.Owner,
                            Year = actualYear,
                            Day = serieADay
                        });
                        var history = team?.FormationChangeHistory;
                        if (team == null || history == null || history.Count == 0)
                            continue;

                        var changeIds = Enumerable.Range(0, history.Count)
                            .Select(index => (Index: index, Id: GetLiveFormationChangeId(group.Id, basket.Id, annualTeam.Owner, index)))
                            .Where(x => !processedChanges.Contains(x.Id))
                            .ToList();
                        if (changeIds.Count == 0)
                            continue;

                        var scoreLeagues = notificationUsers.Count == 0
                            ? []
                            : await GetLiveFormationScoreLeaguesAsync(group, basket, annualTeam.Owner, actualYear, serieADay);
                        var changeStates = BuildLiveFormationChangeStates(team);
                        var recipientEmails = new[] { annualTeam.Owner }
                            .Concat(annualTeam.AdditionalOwners ?? [])
                            .Concat(team.AdditionalOwners ?? [])
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase);
                        var recipients = notificationUsers
                            .Where(user => recipientEmails.Contains(user.Key, StringComparer.OrdinalIgnoreCase))
                            .Where(user => user.Value != null)
                            .ToList();
                        var scoresByChange = recipients.Count == 0
                            ? new Dictionary<int, List<LiveFormationScore>>()
                            : await CalculateLiveFormationScoreChangesAsync(
                                actualYear,
                                serieADay,
                                changeIds.Select(x => x.Index).ToList(),
                                changeStates,
                                scoreLeagues);

                        foreach (var (index, changeId) in changeIds)
                        {
                            if (recipients.Count > 0)
                            {
                                var change = history[index];
                                var notification = BuildLiveFormationChangeNotification(groupName, team, change, scoresByChange[index]);
                                foreach (var user in recipients)
                                {
                                    QueueNotification(sentRecord, key, $"formation|{changeId}", user.Key!, PushNotificationKind.LiveFormationChangeEvents, notification);
                                }
                            }

                            processedChanges.Add(changeId);
                            sentRecord.LiveFormationChangeNotifications.Add(changeId);
                            hasNewChange = true;
                        }
                    }
                }
            }

            if (!hasNewChange)
                return;
            await SaveRecordAsync(key, sentRecord);
        }

        private async Task<List<LiveFormationScoreLeague>> GetLiveFormationScoreLeaguesAsync(
            Group group,
            Basket basket,
            string owner,
            int actualYear,
            int serieADay)
        {
            var leagues = new List<LiveFormationScoreLeague>();
            foreach (var league in group.Leagues ?? [])
            {
                if (league.BasketsId?.Contains(basket.Id, StringComparer.OrdinalIgnoreCase) != true)
                    continue;
                var annualLeague = league.Years?.FirstOrDefault(x => x.Year == actualYear);
                if (annualLeague == null)
                    continue;
                var settings = annualLeague.Settings ?? LeagueSetting.Default;
                var leagueType = league.GetAnnualType(actualYear);
                if (leagueType == LeagueType.SuperLeague || settings.Formation == FormationType.Best)
                    continue;

                var calendar = await _calendarRepository.GetAsync(new LeagueKey
                {
                    Group = group.Id,
                    League = league.Id,
                    Year = actualYear
                });
                var day = calendar?.Rounds?.SelectMany(round => round.Value ?? [])
                    .FirstOrDefault(candidate => candidate.SerieADay == serieADay);
                var game = day?.Games?.FirstOrDefault(candidate =>
                    candidate.Result?.IsCancelled != true &&
                    (string.Equals(candidate.HomeOwner, owner, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(candidate.AwayOwner, owner, StringComparison.OrdinalIgnoreCase)));
                if (game == null)
                    continue;

                leagues.Add(new LiveFormationScoreLeague
                {
                    Name = string.IsNullOrWhiteSpace(league.Name) ? league.Id : league.Name,
                    Type = leagueType == LeagueType.Null ? LeagueType.League : leagueType,
                    Settings = settings,
                    IsHome = string.Equals(game.HomeOwner, owner, StringComparison.OrdinalIgnoreCase)
                });
            }
            return leagues;
        }

        private async Task<Dictionary<int, List<LiveFormationScore>>> CalculateLiveFormationScoreChangesAsync(
            int actualYear,
            int serieADay,
            List<int> changeIndexes,
            List<(Team Before, Team After)> changeStates,
            List<LiveFormationScoreLeague> leagues)
        {
            var scoresByChange = changeIndexes.ToDictionary(index => index, _ => new List<LiveFormationScore>());
            foreach (var league in leagues)
            {
                await _teamCalculator.BuildRequestAsync(actualYear, serieADay, league.Type, league.Settings, false, true, false);
                foreach (var index in changeIndexes)
                {
                    var teams = changeStates[index];
                    var before = _teamCalculator.CalculatePoint(teams.Before.ActivePlayers);
                    var after = _teamCalculator.CalculatePoint(teams.After.ActivePlayers);
                    if (league.IsHome)
                    {
                        before = AddHomeAdvantage(before, league.Settings.PointInHome);
                        after = AddHomeAdvantage(after, league.Settings.PointInHome);
                    }
                    scoresByChange[index].Add(new LiveFormationScore(league.Name, before.Value, after.Value));
                }
            }
            return scoresByChange;
        }

        private static List<(Team Before, Team After)> BuildLiveFormationChangeStates(Team currentTeam)
        {
            var history = currentTeam.FormationChangeHistory ?? [];
            var initialTeam = currentTeam.Clone();
            foreach (var change in history.AsEnumerable().Reverse())
                foreach (var playerChange in change.Players ?? [])
                    SetPlayerPosition(initialTeam, playerChange.Name, playerChange.FromPosition);

            var states = new List<(Team Before, Team After)>(history.Count);
            var beforeTeam = initialTeam;
            foreach (var change in history)
            {
                var afterTeam = beforeTeam.Clone();
                foreach (var playerChange in change.Players ?? [])
                    SetPlayerPosition(afterTeam, playerChange.Name, playerChange.ToPosition);
                states.Add((beforeTeam, afterTeam));
                beforeTeam = afterTeam;
            }
            return states;
        }

        private static void SetPlayerPosition(Team team, string playerName, FantaSoccerRole position)
        {
            var player = team.ActivePlayers.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, playerName, StringComparison.OrdinalIgnoreCase));
            if (player != null)
                player.Position = position;
        }

        private static Notification BuildLiveFormationChangeNotification(
            string groupName,
            Team team,
            LiveFormationChange change,
            List<LiveFormationScore> scores)
        {
            var changedPlayers = string.Join(", ", (change.Players ?? []).Select(player =>
                $"{player.Name} ({GetFormationPositionName(player.FromPosition)} → {GetFormationPositionName(player.ToPosition)})"));
            var scoreSummary = scores.Count == 0
                ? "Nessuna competizione a formazione manuale in questa giornata."
                : string.Join("; ", scores.Select(score =>
                    $"{score.League}: {FormatPoints(score.Before)} → {FormatPoints(score.After)} pt ({FormatDelta(score.After - score.Before)})"));
            var author = string.IsNullOrWhiteSpace(change.Author) ? string.Empty : $" di {change.Author}";
            return new Notification
            {
                Title = "FantaZone",
                Body = $"[{groupName}] {team.Name}: cambio live{author} — {changedPlayers}. {scoreSummary}",
                Icon = "https://fanta.zone/icon.png",
                Badge = "https://fanta.zone/adaptive-icon.png",
                Tag = $"live-formation-{Guid.NewGuid():N}",
                Data = new NotificationData { Uri = "https://fanta.zone/live" }
            };
        }

        private static Point AddHomeAdvantage(Point point, int homeAdvantage)
            => new(point.Value + homeAdvantage, point.DefensiveBonus, point.GoodPeople, point.OwnGoal);

        private static string GetLiveFormationChangeId(string groupId, string basketId, string owner, int index)
            => $"{groupId}:{basketId}:{owner}:{index}";

        private static string GetFormationPositionName(FantaSoccerRole position)
            => position switch
            {
                FantaSoccerRole.GoalKeeper => "POR",
                FantaSoccerRole.Defensor => "DIF",
                FantaSoccerRole.Midfielder => "CEN",
                FantaSoccerRole.Forward => "ATT",
                FantaSoccerRole.BackupGoalKeeper => "riserva POR",
                FantaSoccerRole.FirstBackupDefensor => "1ª riserva DIF",
                FantaSoccerRole.SecondBackupDefensor => "2ª riserva DIF",
                FantaSoccerRole.FirstBackupMidfielder => "1ª riserva CEN",
                FantaSoccerRole.SecondBackupMidfielder => "2ª riserva CEN",
                FantaSoccerRole.FirstBackupForward => "1ª riserva ATT",
                FantaSoccerRole.SecondBackupForward => "2ª riserva ATT",
                FantaSoccerRole.Tribune => "tribuna",
                _ => position.ToString()
            };

        private static string FormatPoints(float value)
            => value.ToString("0.#", CultureInfo.InvariantCulture);

        private static string FormatDelta(float value)
            => value > 0 ? $"+{FormatPoints(value)}" : FormatPoints(value);

        private sealed class LiveFormationScoreLeague
        {
            public string Name { get; set; } = string.Empty;
            public LeagueType Type { get; set; }
            public LeagueSetting Settings { get; set; } = LeagueSetting.Default;
            public bool IsHome { get; set; }
        }

        private sealed record LiveFormationScore(string League, float Before, float After);
        private static Notification BuildLiveChangesNotification(League league, int serieADay, int remainingChanges, string deadlineText)
        {
            var verb = remainingChanges == 1 ? "resta" : "restano";
            var noun = remainingChanges == 1 ? "cambio formazione" : "cambi formazione";
            return new Notification
            {
                Title = "FantaZone",
                Body = $"[{league.Name}] Ti {verb} {remainingChanges} {noun}: usali entro le {deadlineText}.",
                Icon = "https://fanta.zone/icon.png",
                Badge = "https://fanta.zone/adaptive-icon.png",
                Tag = $"live-changes-{serieADay}-{league.Id}",
                Data = new NotificationData
                {
                    Uri = "https://fanta.zone/live"
                }
            };
        }
        private static Notification BuildEndDayNotification(League league, int serieADay, CalendarGame game, bool isHome)
        {
            var opponentTeam = isHome ? game.Away : game.Home;
            var myGoals = isHome ? game.Result!.HomeGoal : game.Result!.AwayGoal;
            var opponentGoals = isHome ? game.Result!.AwayGoal : game.Result!.HomeGoal;
            var myPoints = isHome ? game.Result!.Home?.Value ?? 0 : game.Result!.Away?.Value ?? 0;
            var outcome = myGoals > opponentGoals ? "Hai vinto" : myGoals < opponentGoals ? "Hai perso" : "Hai pareggiato";
            var body = $"[{league.Name}] Giornata {serieADay}: {outcome} contro {opponentTeam} {myGoals}-{opponentGoals} ({myPoints.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} pt)";
            return new Notification
            {
                Title = "FantaZone",
                Body = body,
                Icon = "https://fanta.zone/icon.png",
                Badge = "https://fanta.zone/adaptive-icon.png",
                Tag = $"end-day-{serieADay}-{league.Id}",
                Data = new NotificationData
                {
                    Uri = "https://fanta.zone/calendar"
                }
            };
        }
        private static Dictionary<string, List<string>> BuildOwnerEmails(Group group, League league, int actualYear)
        {
            var basketIds = league.BasketsId?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            var ownerEmails = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var basket in group.Baskets?.Where(x => basketIds.Contains(x.Id)) ?? [])
            {
                foreach (var year in basket.Years?.Where(x => x.Year == actualYear) ?? [])
                {
                    foreach (var team in year.Teams ?? [])
                    {
                        if (string.IsNullOrWhiteSpace(team.Owner))
                            continue;
                        List<string> emails = team.AdditionalOwners != null
                            ? [team.Owner, .. team.AdditionalOwners]
                            : [team.Owner];
                        ownerEmails[team.Owner] = ownerEmails.GetValueOrDefault(team.Owner, []).Concat(emails)
                            .Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    }
                }
            }
            return ownerEmails;
        }
        private sealed class EndDayEntry
        {
            public string GroupId { get; set; } = string.Empty;
            public League League { get; set; } = null!;
            public List<CalendarGame> Games { get; set; } = [];
            public Dictionary<string, List<string>> OwnerEmails { get; set; } = [];
        }
        private sealed class OpponentNotification
        {
            public string LeagueId { get; set; }
            public string League { get; set; }
            public string Opponent { get; set; }
            public List<string> BasketIds { get; set; } = [];
            public string GameId { get; set; } = string.Empty;
        }
        private Task<PushNotificationSent> GetRecordAsync(YearDayKey key, CancellationToken cancellationToken = default)
            => _state.GetAsync(key, cancellationToken.CanBeCanceled ? cancellationToken : _executionCancellationToken);

        private Task SaveRecordAsync(YearDayKey key, PushNotificationSent record, CancellationToken cancellationToken = default)
            => _state.SaveAsync(key, record, cancellationToken.CanBeCanceled ? cancellationToken : _executionCancellationToken);

        private void QueueNotification(PushNotificationSent record, YearDayKey key, string scope, string email,
            PushNotificationKind kind, Notification notification, DateTimeOffset? expiresAt = null)
        {
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{key.Year}|{key.Day}|{scope}|{email.ToLowerInvariant()}")));
            record.PendingNotifications ??= [];
            if (record.PendingNotifications.Any(p => p.Id == id)) return;
            // Stable across retries: providers/browsers can replace a delivery whose
            // acceptance was followed by a crash before the receipt was persisted.
            notification = JsonSerializer.Deserialize<Notification>(JsonSerializer.Serialize(notification))!;
            notification.Tag = $"push-{id}";
            record.PendingNotifications.Add(new PendingPushNotification
            {
                Id = id, Email = email, Kind = kind, Notification = notification,
                ExpiresAt = expiresAt ?? _timeProvider.GetUtcNow().AddHours(48)
            });
        }

        private async Task DispatchPendingAsync(CancellationToken cancellationToken)
        {
            foreach (var key in await _state.GetPendingDaysAsync(cancellationToken))
            {
                var record = await GetRecordAsync(key, cancellationToken);
                if (record.PendingNotifications.Count == 0)
                {
                    await SaveRecordAsync(key, record, cancellationToken);
                    continue;
                }
                using var receiptLock = new SemaphoreSlim(1, 1);
                await Parallel.ForEachAsync(record.PendingNotifications.ToArray(),
                    new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = cancellationToken }, async (pending, token) =>
                {
                    var now = _timeProvider.GetUtcNow();
                    if (pending.NextAttemptAt > now) return;
                    var complete = false;
                    var completedEndpoints = new List<string>();
                    if (pending.ExpiresAt <= now)
                    {
                        await _logger.LogAsync<PushNotificationJob>($"Push delivery expired: {pending.Id} after {pending.Attempts} attempts.");
                        complete = true;
                    }
                    else
                    {
                        var settings = await _settings.GetExistingAsync(pending.Email, token);
                        if (settings?.IsEnabled(pending.Kind) != true)
                            complete = true;
                        else
                        {
                            var snapshot = PushNotificationSettingsService.Copy(settings);
                            snapshot.Subscriptions.RemoveAll(s => pending.CompletedEndpoints.Contains(s.Endpoint));
                            try
                            {
                                var result = await _pushNotificationService.SendNotificationAsync(pending.Email, snapshot, pending.Notification, token);
                                completedEndpoints = result.CompletedEndpoints;
                                complete = result.IsComplete;
                            }
                            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                            catch (Exception ex) { await _logger.LogErrorAsync<PushNotificationJob>(ex); }
                        }
                    }
                    await receiptLock.WaitAsync(token);
                    try
                    {
                        pending.CompletedEndpoints = pending.CompletedEndpoints.Concat(completedEndpoints).Distinct().ToList();
                        if (complete) record.PendingNotifications.Remove(pending);
                        else
                        {
                            pending.Attempts++;
                            pending.NextAttemptAt = now.AddMinutes(Math.Min(15, Math.Pow(2, Math.Min(pending.Attempts - 1, 4))));
                        }
                        // Provider requests are bounded/concurrent, but receipts are
                        // committed individually without racing document rewrites.
                        await SaveRecordAsync(key, record, token);
                    }
                    finally { receiptLock.Release(); }
                });
            }
        }
        private NotificationToSend? GetNotification(VotedRealPlayer live, VotedRealPlayer current)
        {
            StringBuilder message = new();
            var needsNotification = false;
            Check(current.Vote.Goal, live.Vote.Goal, "Goal", true);
            Check(current.Vote.Assist, live.Vote.Assist, "Assist", true);
            Check(current.Vote.Penalty, live.Vote.Penalty, "Rigore", true);
            Check(current.Vote.OwnGoal, live.Vote.OwnGoal, "Autogol", true);
            Check(current.Vote.WrongedPenalty, live.Vote.WrongedPenalty, "Rigore sbagliato", true);
            Check(current.Vote.SufferedGoal, live.Vote.SufferedGoal, "Goal subito", true);
            Check(current.Vote.StoppedPenalty, live.Vote.StoppedPenalty, "Rigore parato", true);
            Check(current.Vote.ManOfTheMatch ? 1 : 0, live.Vote.ManOfTheMatch ? 1 : 0, "Migliore in campo", true);
            Check(current.Vote.Status == Behaviour.YellowCard ? 1 : 0, live.Vote.Status == Behaviour.YellowCard ? 1 : 0, "Giallo", true);
            Check(current.Vote.Status == Behaviour.RedCard ? 1 : 0, live.Vote.Status == Behaviour.RedCard ? 1 : 0, "Rosso", true);
            void Check(float previous, float now, string action, bool isMaleAction)
            {
                if (now != previous && message.Length > 0)
                {
                    message.Append(", ");
                }
                if (now > previous)
                {
                    message.Append(action);
                    needsNotification = true;
                }
                if (now < previous)
                {
                    message.Append($"{action} {(isMaleAction ? "annullato" : "annullata")}");
                    needsNotification = true;
                }
            }
            if (needsNotification)
            {
                return new NotificationToSend
                {
                    Body = $"{live.Name}: {message}",
                    PlayerKey = live.Key
                };
            }
            return null;
        }
        private sealed class NotificationToSend
        {
            public string PlayerKey { get; set; }
            public string Body { get; set; }
        }
        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<PushNotificationJob>(exception);
    }
}
