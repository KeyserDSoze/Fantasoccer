using System.Text;
using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Engine.GroupsManager
{
    public class PushNotificationJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<Group, string> _groupRepository;
        private readonly IRepository<LiveGroup, string> _liveGroupRepository;
        private readonly IRepository<Team, TeamDayKey> _teamDayRepository;
        private readonly IRepository<RealCalendar, int> _serieACalendar;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IRepository<Calendar, LeagueKey> _calendarRepository;
        private readonly IRepository<PushNotificationSettings, string> _pushNotificationRepository;
        private readonly IRepository<PushNotificationSent, YearDayKey> _pushNotificationSentRepository;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _liveVoteRepository;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _notificationVoteRepository;
        public PushNotificationJob(IFantasoccerLogger logger,
            IRepository<Group, string> groupRepository,
            IRepository<LiveGroup, string> liveGroupRepository,
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory,
            IRepository<Team, TeamDayKey> teamDayRepository,
            IRepository<RealCalendar, int> serieACalendar,
            IPushNotificationService pushNotificationService,
            IRepository<Calendar, LeagueKey> calendarRepository,
            IRepository<PushNotificationSettings, string> pushNotificationRepository,
            IRepository<PushNotificationSent, YearDayKey> pushNotificationSentRepository)
        {
            _logger = logger;
            _groupRepository = groupRepository;
            _liveGroupRepository = liveGroupRepository;
            _teamDayRepository = teamDayRepository;
            _serieACalendar = serieACalendar;
            _pushNotificationService = pushNotificationService;
            _calendarRepository = calendarRepository;
            _pushNotificationRepository = pushNotificationRepository;
            _pushNotificationSentRepository = pushNotificationSentRepository;
            _liveVoteRepository = factory.Create("live")!;
            _notificationVoteRepository = factory.Create("notification")!;
        }
        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
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
                        var storedNotificationVotes = await _notificationVoteRepository.GetAsync(yearDayKey);
                        var notificationVotes = storedNotificationVotes ?? new VotedRealPlayerWrapper() { Players = new() };
                        var toNotifyPlayers = new List<VotedRealPlayer>();
                        var notificationsToSend = new List<NotificationToSend>();
                        foreach (var player in liveVotes.Players)
                        {
                            var currentPlayer = notificationVotes.Players.FirstOrDefault(x => x.Key == player.Key) ?? VotedRealPlayer.Empty;
                            var notificationToSend = GetNotification(player, currentPlayer);
                            if (notificationToSend != null)
                                notificationsToSend.Add(notificationToSend);
                        }
                        if (storedNotificationVotes == null)
                            await _notificationVoteRepository.InsertAsync(yearDayKey, liveVotes);
                        else
                            await _notificationVoteRepository.UpdateAsync(yearDayKey, liveVotes);
                        if (notificationsToSend.Any())
                        {
                            var notificationUsers = await _pushNotificationRepository.Where(x => x.MatchEvents || x.OpponentMatchEvents).ToListAsync();
                            foreach (var group in await _liveGroupRepository.ToListAsync())
                            {
                                var teams = new List<Team>();
                                var groupKey = group.Key;
                                var groupValue = await _groupRepository.GetAsync(groupKey!);
                                foreach (var basket in groupValue!.Baskets)
                                {
                                    var currentYear = basket.Years.FirstOrDefault(x => x.Year == actualYear);
                                    if (currentYear != null)
                                    {
                                        foreach (var team in currentYear.Teams)
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
                                                teams.Add(currentTeam);
                                        }
                                    }
                                }
                                var currentOpponents = new Dictionary<string, List<OpponentNotification>>();
                                foreach (var league in group.Value!.Leagues)
                                {
                                    var currentCalendar = await _calendarRepository.GetAsync(new LeagueKey
                                    {
                                        Group = groupKey!,
                                        League = league.Id,
                                        Year = actualYear
                                    });
                                    var currentDay = currentCalendar?.Rounds.SelectMany(x => x.Value).FirstOrDefault(x => x.SerieADay == liveDay.SerieADay);
                                    if (currentDay != null)
                                    {
                                        foreach (var game in currentDay.Games)
                                        {
                                            AddOpponent(game.HomeOwner, game.AwayOwner);
                                            AddOpponent(game.AwayOwner, game.HomeOwner);

                                            void AddOpponent(string owner, string opponent)
                                            {
                                                var notificationUser = notificationUsers.FirstOrDefault(x => x.Key == opponent);
                                                if (notificationUser != null && notificationUser.Value?.OpponentMatchEvents == true)
                                                {
                                                    if (!currentOpponents.ContainsKey(owner))
                                                        currentOpponents.Add(owner, []);
                                                    currentOpponents[owner].Add(new OpponentNotification
                                                    {
                                                        League = league.Name,
                                                        Owner = owner,
                                                        Opponent = opponent
                                                    });
                                                }
                                            }
                                        }
                                    }
                                }
                                var sendTasks = new List<ValueTask>();
                                var semaphore = new SemaphoreSlim(10);

                                foreach (var notification in notificationsToSend)
                                {
                                    foreach (var team in teams.Where(x => x.Players.Any(t => t.Key == notification.PlayerKey)))
                                    {
                                        await semaphore.WaitAsync();
                                        sendTasks.Add(SendNotificationWithSemaphoreAsync(team, notification, null, null, x => x?.Value?.MatchEvents == true, semaphore));
                                        
                                        if (currentOpponents.ContainsKey(team.Owner))
                                        {
                                            var opponents = currentOpponents[team.Owner];
                                            foreach (var opponent in opponents)
                                            {
                                                var opponentTeam = teams.FirstOrDefault(x => x.Owner == opponent.Opponent);
                                                if (opponentTeam != null)
                                                {
                                                    await semaphore.WaitAsync();
                                                    sendTasks.Add(SendNotificationWithSemaphoreAsync(opponentTeam, notification, $"[{opponent.League}]", $"({team.Name.Trim()})", x => x?.Value?.OpponentMatchEvents == true, semaphore));
                                                }
                                            }
                                        }
                                    }
                                }

                                await Task.WhenAll(sendTasks.Select(t => t.AsTask()));
                                
                                var stats = _pushNotificationService.GetStatistics();
                                await _logger.LogAsync<PushNotificationJob>($"Push notification job completed: {stats.TotalSucceeded} sent, {stats.TotalFailed} failed, {stats.TotalDiscarded} discarded after {3} retries").NoContext();
                            }
                            async ValueTask SendNotificationAsync(Team currentTeam, NotificationToSend notification, string? prefix, string? postfix, Func<Entity<PushNotificationSettings, string>?, bool> predicate)
                            {
                                List<string> emails = currentTeam.AdditionalOwners != null ? [currentTeam.Owner, .. currentTeam.AdditionalOwners] : [currentTeam.Owner];
                                foreach (var email in emails)
                                {
                                    var notificationUser = notificationUsers.FirstOrDefault(x => x.Key == email);
                                    if (notificationUser?.Value != null && predicate(notificationUser))
                                    {
                                        var body = !string.IsNullOrWhiteSpace(prefix) ? $"{prefix} {notification.Body}" : notification.Body;
                                        if (!string.IsNullOrWhiteSpace(postfix))
                                            body = $"{body} {postfix}";
                                        await _pushNotificationService.SendNotificationAsync(email,
                                            notificationUser.Value,
                                            new Notification
                                            {
                                                Body = body,
                                                Title = "FantaZone",
                                                Icon = "https://fanta.zone/icon.png",
                                                Badge = "https://fanta.zone/adaptive-icon.png",
                                                Tag = $"match-event-{notification.PlayerKey}",
                                                Data = new NotificationData
                                                {
                                                    Uri = "https://fanta.zone/live"
                                                }
                                            });
                                    }
                                }
                            }

                            async ValueTask SendNotificationWithSemaphoreAsync(Team currentTeam, NotificationToSend notification, string? prefix, string? postfix, Func<Entity<PushNotificationSettings, string>?, bool> predicate, SemaphoreSlim semaphore)
                            {
                                try
                                {
                                    await SendNotificationAsync(currentTeam, notification, prefix, postfix, predicate);
                                }
                                finally
                                {
                                    semaphore.Release();
                                }
                            }
                            await _logger.LogAsync<PushNotificationJob>($"Notification parsate.").NoContext();
                        }
                    }
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
                    if (!await _pushNotificationSentRepository.ExistAsync(yearlyDayKey))
                    {
                        var notificationUsers = await _pushNotificationRepository.Where(x => x.DeploymentReminder).ToListAsync();
                        var reminderTasks = new List<ValueTask>();
                        var semaphore = new SemaphoreSlim(10);

                        foreach (var user in notificationUsers)
                        {
                            await semaphore.WaitAsync();
                            reminderTasks.Add(SendReminderWithSemaphoreAsync(user, semaphore));
                        }

                        await Task.WhenAll(reminderTasks.Select(t => t.AsTask()));

                        async ValueTask SendReminderWithSemaphoreAsync(Entity<PushNotificationSettings, string> user, SemaphoreSlim sem)
                        {
                            try
                            {
                                await _pushNotificationService.SendNotificationAsync(user.Key!, user.Value!, new Notification
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
                            finally
                            {
                                sem.Release();
                            }
                        }

                        await _pushNotificationSentRepository.InsertAsync(yearlyDayKey, new PushNotificationSent
                        {
                            DeploymentReminder = true,
                            EndDay = false
                        });
                    }
                }
                if (realCalendar.LastDay is RealDay lastDay)
                    await SendEndDayNotificationsAsync(actualYear, lastDay.SerieADay);
            }
        }
        private async Task SendEndDayNotificationsAsync(int actualYear, int serieADay)
        {
            var endDayKey = new YearDayKey { Year = actualYear, Day = serieADay };
            var alreadySent = await _pushNotificationSentRepository.GetAsync(endDayKey);
            if (alreadySent?.EndDay == true)
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
                    var day = calendar?.Rounds.SelectMany(x => x.Value).FirstOrDefault(x => x.SerieADay == serieADay);
                    if (day == null)
                        continue;
                    var playableGames = day.Games.Where(x => x.Result?.IsCancelled != true).ToList();
                    if (playableGames.Count == 0)
                        continue;
                    if (playableGames.Any(x => x.HasNoResult))
                    {
                        everythingReady = false;
                        continue;
                    }
                    var ownerEmails = BuildOwnerEmails(group, league, actualYear);
                    entries.Add(new EndDayEntry { League = league, Games = playableGames, OwnerEmails = ownerEmails });
                }
            }
            if (!everythingReady)
                return;
            if (endDayUsers.Count > 0 && entries.Count > 0)
            {
                var sendTasks = new List<ValueTask>();
                var semaphore = new SemaphoreSlim(10);
                foreach (var entry in entries)
                {
                    foreach (var game in entry.Games)
                    {
                        await QueueEndDayAsync(entry, game, game.HomeOwner, true);
                        await QueueEndDayAsync(entry, game, game.AwayOwner, false);
                    }
                }
                await Task.WhenAll(sendTasks.Select(t => t.AsTask()));

                async Task QueueEndDayAsync(EndDayEntry entry, CalendarGame game, string? owner, bool isHome)
                {
                    if (string.IsNullOrWhiteSpace(owner) || !entry.OwnerEmails.TryGetValue(owner, out var emails))
                        return;
                    foreach (var email in emails)
                    {
                        var user = endDayUsers.FirstOrDefault(x => string.Equals(x.Key, email, StringComparison.OrdinalIgnoreCase));
                        if (user?.Value == null)
                            continue;
                        await semaphore.WaitAsync();
                        sendTasks.Add(SendEndDayWithSemaphoreAsync(user, BuildEndDayNotification(entry.League, serieADay, game, isHome), semaphore));
                    }
                }
            }
            var sentRecord = alreadySent ?? new PushNotificationSent();
            sentRecord.EndDay = true;
            if (await _pushNotificationSentRepository.ExistAsync(endDayKey))
                await _pushNotificationSentRepository.UpdateAsync(endDayKey, sentRecord);
            else
                await _pushNotificationSentRepository.InsertAsync(endDayKey, sentRecord);
        }
        private async Task SendLiveChangesReminderAsync(int actualYear, int serieADay, DateTime deadline)
        {
            var remaining = deadline - FantacalcioTime.ItalyNow;
            if (remaining <= TimeSpan.Zero || remaining > TimeSpan.FromMinutes(120))
                return;
            var key = new YearDayKey { Year = actualYear, Day = serieADay };
            var alreadySent = await _pushNotificationSentRepository.GetAsync(key);
            if (alreadySent?.LiveChangesReminder == true)
                return;
            var reminderUsers = await _pushNotificationRepository.Where(x => x.LiveChangesReminder).ToListAsync();
            if (reminderUsers.Count > 0)
            {
                var deadlineText = deadline.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                var sendTasks = new List<ValueTask>();
                var semaphore = new SemaphoreSlim(10);
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
                                    foreach (var email in emails)
                                    {
                                        var user = reminderUsers.FirstOrDefault(x => string.Equals(x.Key, email, StringComparison.OrdinalIgnoreCase));
                                        if (user?.Value == null)
                                            continue;
                                        await semaphore.WaitAsync();
                                        sendTasks.Add(SendEndDayWithSemaphoreAsync(user, BuildLiveChangesNotification(league, serieADay, remainingChanges, deadlineText), semaphore));
                                    }
                                }
                            }
                        }
                    }
                }
                await Task.WhenAll(sendTasks.Select(t => t.AsTask()));
            }
            var record = alreadySent ?? new PushNotificationSent();
            record.LiveChangesReminder = true;
            if (await _pushNotificationSentRepository.ExistAsync(key))
                await _pushNotificationSentRepository.UpdateAsync(key, record);
            else
                await _pushNotificationSentRepository.InsertAsync(key, record);
        }
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
        private async ValueTask SendEndDayWithSemaphoreAsync(Entity<PushNotificationSettings, string> user, Notification notification, SemaphoreSlim semaphore)
        {
            try
            {
                await _pushNotificationService.SendNotificationAsync(user.Key!, user.Value!, notification);
            }
            finally
            {
                semaphore.Release();
            }
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
            var basketIds = league.BasketsId?.ToHashSet() ?? [];
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
                        ownerEmails[team.Owner] = emails;
                    }
                }
            }
            return ownerEmails;
        }
        private sealed class EndDayEntry
        {
            public League League { get; set; } = null!;
            public List<CalendarGame> Games { get; set; } = [];
            public Dictionary<string, List<string>> OwnerEmails { get; set; } = [];
        }
        private sealed class OpponentNotification
        {
            public string League { get; set; }
            public string Owner { get; set; }
            public string Opponent { get; set; }
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
