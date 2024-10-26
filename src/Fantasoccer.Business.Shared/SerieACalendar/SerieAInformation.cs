using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace Fantasoccer.Business
{
    internal sealed class SerieAInformation : ISerieAInformation
    {
        private readonly HttpClient _serieAHttpClient;
        public SerieAInformation(IHttpClientFactory httpClientFactory)
        {
            _serieAHttpClient = httpClientFactory.CreateClient("seriea");
        }

        public async Task<RealCalendar> GetCalendarAsync(int min = 1, int max = 38)
        {
            var calendar = new RealCalendar
            {
                Days = new List<RealDay>()
            };
            var serieCalendar = await _serieAHttpClient.GetFromJsonAsync<CalendarRoot>("widget/category?lang=it&parent=264699&limit=50&order_index=ASC", default);
            foreach (var day in serieCalendar!.Data.Body.OrderBy(x => int.Parse(x.Order)))
            {
                var serieADay = int.Parse(day.Order);
                if (serieADay < min)
                    continue;
                if (serieADay > max)
                    continue;
                var theDay = await GetDayAsync(serieADay, day.Id);
                calendar.Days.Add(theDay);
            }
            return calendar;
        }
        private async Task<RealDay> GetDayAsync(int day, string dayId)
        {
            var response = await _serieAHttpClient.GetFromJsonAsync<SingleDaySerieA>($"stats/live/match?extra_link&lang=it&order=oldest&match_day_id={dayId}").NoContext();
            var realDay = new RealDay { Games = new List<RealGame>(), SerieADay = day, Year = FantacalcioTime.ActualYear };
            foreach (var match in response.Data)
            {
                realDay.Games.Add(new RealGame
                {
                    Home = new RealTeam { Name = match.HomeTeamName.ToLower().ToUpperCaseFirst() },
                    Away = new RealTeam { Name = match.AwayTeamName.ToLower().ToUpperCaseFirst() },
                    Date = FantacalcioTime.ToItalyNow(match.DateTime),
                    Delayed = FantacalcioTime.ItalyNow > match.DateTime.AddHours(3) && string.IsNullOrWhiteSpace(match.AwaySchema),
                    HomeGoal = match.HomeGoal,
                    AwayGoal = match.AwayGoal
                });
            }
            await Task.Delay(10);
            return realDay;
        }

        public async Task<RealRank> GetRankAsync()
        {
            var season = FantacalcioTime.LabelYear(FantacalcioTime.ItalyNow, '-', true);
            var rank = await _serieAHttpClient.GetFromJsonAsync<RankRoot>($"stats/Classificacompleta?CAMPIONATO=A&STAGIONE={season}&TURNO=UNICO&GIRONE=UNI", default);
            var realRank = new RealRank
            {
                Teams = [],
            };
            foreach (var team in rank.Data)
            {
                realRank.Teams.Add(new RealRankedTeam
                {
                    Name = team.Nome,
                    Goal = team.RETIFATTE,
                    SufferedGoal = team.RETISUBITE,
                    Defeats = team.Perse,
                    Draws = team.Pareggiate,
                    Victories = team.Vinte,
                    Point = team.PuntiCls,
                    Played = team.Giocate
                });
            }
            realRank.Teams = [.. realRank.Teams.OrderByDescending(x => x.Point)];
            return realRank;
        }

        private class CalendarDataBody
        {
            [JsonPropertyName("background_image")]
            public string BackgroundImage { get; set; }

            [JsonPropertyName("body")]
            public string Body { get; set; }

            [JsonPropertyName("category_status")]
            public string CategoryStatus { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("image")]
            public string Image { get; set; }

            [JsonPropertyName("logo")]
            public string Logo { get; set; }

            [JsonPropertyName("order")]
            public string Order { get; set; }

            [JsonPropertyName("overtitle")]
            public string Overtitle { get; set; }

            [JsonPropertyName("title")]
            public string Title { get; set; }

            [JsonPropertyName("url")]
            public string Url { get; set; }
        }

        private class CalendarData
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("uicode")]
            public string Uicode { get; set; }

            [JsonPropertyName("body")]
            public List<CalendarDataBody> Body { get; set; }

            [JsonPropertyName("data")]
            public CalendarData Data { get; set; }

            [JsonPropertyName("_limit")]
            public string Limit { get; set; }

            [JsonPropertyName("_total")]
            public int Total { get; set; }

            [JsonPropertyName("category_status")]
            public string CategoryStatus { get; set; }

            [JsonPropertyName("layout")]
            public string Layout { get; set; }


            [JsonPropertyName("link_text")]
            public string LinkText { get; set; }

            [JsonPropertyName("link_url")]
            public string LinkUrl { get; set; }

            [JsonPropertyName("order_index")]
            public string OrderIndex { get; set; }

            [JsonPropertyName("parent")]
            public string Parent { get; set; }

            [JsonPropertyName("title")]
            public string Title { get; set; }
        }

        private class CalendarRoot
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("message")]
            public string Message { get; set; }

            [JsonPropertyName("errors")]
            public List<object> Errors { get; set; }

            [JsonPropertyName("data")]
            public CalendarData Data { get; set; }

            [JsonPropertyName("code")]
            public int Code { get; set; }
        }
        private class CalendarSerieAGame
        {
            [JsonPropertyName("away_coach_id")]
            public int? AwayCoachId { get; set; }

            [JsonPropertyName("away_coach_image")]
            public string AwayCoachImage { get; set; }

            [JsonPropertyName("away_coach_name")]
            public string AwayCoachName { get; set; }

            [JsonPropertyName("away_coach_surname")]
            public string AwayCoachSurname { get; set; }

            [JsonPropertyName("away_goal")]
            public int AwayGoal { get; set; }

            [JsonPropertyName("away_netco_id")]
            public string AwayNetcoId { get; set; }

            [JsonPropertyName("away_penalty_goal")]
            public int AwayPenaltyGoal { get; set; }

            [JsonPropertyName("away_schema")]
            public string AwaySchema { get; set; }

            [JsonPropertyName("away_secondary_team_logo")]
            public string AwaySecondaryTeamLogo { get; set; }

            [JsonPropertyName("away_team_active")]
            public int AwayTeamActive { get; set; }

            [JsonPropertyName("away_team_logo")]
            public string AwayTeamLogo { get; set; }

            [JsonPropertyName("away_team_name")]
            public string AwayTeamName { get; set; }

            [JsonPropertyName("away_team_short_name")]
            public string AwayTeamShortName { get; set; }

            [JsonPropertyName("away_team_url")]
            public string AwayTeamUrl { get; set; }

            [JsonPropertyName("broadcasters")]
            public string Broadcasters { get; set; }

            [JsonPropertyName("category_status")]
            public string CategoryStatus { get; set; }

            [JsonPropertyName("championship_background_image")]
            public string ChampionshipBackgroundImage { get; set; }

            [JsonPropertyName("championship_category_id")]
            public int ChampionshipCategoryId { get; set; }

            [JsonPropertyName("championship_category_status")]
            public object ChampionshipCategoryStatus { get; set; }

            [JsonPropertyName("championship_image")]
            public string ChampionshipImage { get; set; }

            [JsonPropertyName("championship_metadata")]
            public string ChampionshipMetadata { get; set; }

            [JsonPropertyName("championship_title")]
            public string ChampionshipTitle { get; set; }

            [JsonPropertyName("date_time")]
            public DateTime DateTime { get; set; }

            [JsonPropertyName("georule_id")]
            public int GeoruleId { get; set; }

            [JsonPropertyName("highlight")]
            public string Highlight { get; set; }

            [JsonPropertyName("home_coach_id")]
            public int? HomeCoachId { get; set; }

            [JsonPropertyName("home_coach_image")]
            public string HomeCoachImage { get; set; }

            [JsonPropertyName("home_coach_name")]
            public string HomeCoachName { get; set; }

            [JsonPropertyName("home_coach_surname")]
            public string HomeCoachSurname { get; set; }

            [JsonPropertyName("home_goal")]
            public int HomeGoal { get; set; }

            [JsonPropertyName("home_netco_id")]
            public string HomeNetcoId { get; set; }

            [JsonPropertyName("home_penalty_goal")]
            public int HomePenaltyGoal { get; set; }

            [JsonPropertyName("home_schema")]
            public string HomeSchema { get; set; }

            [JsonPropertyName("home_secondary_team_logo")]
            public string HomeSecondaryTeamLogo { get; set; }

            [JsonPropertyName("home_team_active")]
            public int HomeTeamActive { get; set; }

            [JsonPropertyName("home_team_logo")]
            public string HomeTeamLogo { get; set; }

            [JsonPropertyName("home_team_name")]
            public string HomeTeamName { get; set; }

            [JsonPropertyName("home_team_short_name")]
            public string HomeTeamShortName { get; set; }

            [JsonPropertyName("home_team_ticket_url")]
            public string HomeTeamTicketUrl { get; set; }

            [JsonPropertyName("home_team_url")]
            public string HomeTeamUrl { get; set; }

            [JsonPropertyName("id_category")]
            public int IdCategory { get; set; }

            [JsonPropertyName("live_timing")]
            public string LiveTiming { get; set; }

            [JsonPropertyName("match_day_category_status")]
            public string MatchDayCategoryStatus { get; set; }

            [JsonPropertyName("match_day_id_category")]
            public int MatchDayIdCategory { get; set; }

            [JsonPropertyName("match_day_order")]
            public string MatchDayOrder { get; set; }

            [JsonPropertyName("match_day_title")]
            public string MatchDayTitle { get; set; }

            [JsonPropertyName("match_hm")]
            public string MatchHm { get; set; }

            [JsonPropertyName("match_id")]
            public int MatchId { get; set; }

            [JsonPropertyName("match_lineup_pdf")]
            public string MatchLineupPdf { get; set; }

            [JsonPropertyName("match_name")]
            public string MatchName { get; set; }

            [JsonPropertyName("match_program_pdf")]
            public string MatchProgramPdf { get; set; }

            [JsonPropertyName("match_report")]
            public int MatchReport { get; set; }

            [JsonPropertyName("match_report_pdf")]
            public string MatchReportPdf { get; set; }

            [JsonPropertyName("match_status")]
            public int MatchStatus { get; set; }

            [JsonPropertyName("minutes_played")]
            public string MinutesPlayed { get; set; }

            [JsonPropertyName("netco_id")]
            public string NetcoId { get; set; }

            [JsonPropertyName("play_phase")]
            public string PlayPhase { get; set; }

            [JsonPropertyName("player_head_shot")]
            public string PlayerHeadShot { get; set; }

            [JsonPropertyName("player_id")]
            public int PlayerId { get; set; }

            [JsonPropertyName("player_image")]
            public string PlayerImage { get; set; }

            [JsonPropertyName("player_match_card")]
            public string PlayerMatchCard { get; set; }

            [JsonPropertyName("player_medium_shot")]
            public string PlayerMediumShot { get; set; }

            [JsonPropertyName("player_name")]
            public string PlayerName { get; set; }

            [JsonPropertyName("player_short_name")]
            public string PlayerShortName { get; set; }

            [JsonPropertyName("player_slug")]
            public string PlayerSlug { get; set; }

            [JsonPropertyName("player_surname")]
            public string PlayerSurname { get; set; }

            [JsonPropertyName("player_surname_full")]
            public string PlayerSurnameFull { get; set; }

            [JsonPropertyName("referee")]
            public string Referee { get; set; }

            [JsonPropertyName("round_category_status")]
            public object RoundCategoryStatus { get; set; }

            [JsonPropertyName("round_id_category")]
            public int RoundIdCategory { get; set; }

            [JsonPropertyName("round_title")]
            public string RoundTitle { get; set; }

            [JsonPropertyName("season_title")]
            public string SeasonTitle { get; set; }

            [JsonPropertyName("slug")]
            public string Slug { get; set; }

            [JsonPropertyName("ticket_url")]
            public string TicketUrl { get; set; }

            [JsonPropertyName("unknown_datetime")]
            public int UnknownDatetime { get; set; }

            [JsonPropertyName("venue_background_image")]
            public string VenueBackgroundImage { get; set; }

            [JsonPropertyName("venue_id")]
            public int VenueId { get; set; }

            [JsonPropertyName("venue_image")]
            public string VenueImage { get; set; }

            [JsonPropertyName("venue_name")]
            public string VenueName { get; set; }

            [JsonPropertyName("venue_plan_image")]
            public string VenuePlanImage { get; set; }

            [JsonPropertyName("weather")]
            public string Weather { get; set; }
        }

        private class SingleDaySerieA
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("message")]
            public string Message { get; set; }

            [JsonPropertyName("errors")]
            public List<object> Errors { get; set; }

            [JsonPropertyName("data")]
            public List<CalendarSerieAGame> Data { get; set; }

            [JsonPropertyName("code")]
            public int Code { get; set; }
        }

        public class RankDatum
        {
            [JsonPropertyName("CAMPIONATO")]
            public string CAMPIONATO { get; set; }

            [JsonPropertyName("CODSQUADRA")]
            public string CODSQUADRA { get; set; }

            [JsonPropertyName("GIRONE")]
            public string GIRONE { get; set; }

            [JsonPropertyName("Giocate")]
            public int Giocate { get; set; }

            [JsonPropertyName("GiocateCasa")]
            public int GiocateCasa { get; set; }

            [JsonPropertyName("GiocateFuori")]
            public int GiocateFuori { get; set; }

            [JsonPropertyName("Nome")]
            public string Nome { get; set; }

            [JsonPropertyName("NomeCompleto")]
            public string NomeCompleto { get; set; }

            [JsonPropertyName("NomeSintetico")]
            public string NomeSintetico { get; set; }

            [JsonPropertyName("Pareggiate")]
            public int Pareggiate { get; set; }

            [JsonPropertyName("PareggiateCasa")]
            public int PareggiateCasa { get; set; }

            [JsonPropertyName("PareggiateFuori")]
            public int PareggiateFuori { get; set; }

            [JsonPropertyName("Perse")]
            public int Perse { get; set; }

            [JsonPropertyName("PerseCasa")]
            public int PerseCasa { get; set; }

            [JsonPropertyName("PerseFuori")]
            public int PerseFuori { get; set; }

            [JsonPropertyName("PosCls")]
            public int PosCls { get; set; }

            [JsonPropertyName("PuntiCls")]
            public int PuntiCls { get; set; }

            [JsonPropertyName("PuntiClsCasa")]
            public int PuntiClsCasa { get; set; }

            [JsonPropertyName("PuntiClsFuori")]
            public int PuntiClsFuori { get; set; }

            [JsonPropertyName("PuntiPen")]
            public int PuntiPen { get; set; }

            [JsonPropertyName("RETIFATTE")]
            public int RETIFATTE { get; set; }

            [JsonPropertyName("RETISUBITE")]
            public int RETISUBITE { get; set; }

            [JsonPropertyName("RetiFatteCasa")]
            public int RetiFatteCasa { get; set; }

            [JsonPropertyName("RetiFatteFuori")]
            public int RetiFatteFuori { get; set; }

            [JsonPropertyName("RetiSubiteCasa")]
            public int RetiSubiteCasa { get; set; }

            [JsonPropertyName("RetiSubiteFuori")]
            public int RetiSubiteFuori { get; set; }

            [JsonPropertyName("STAGIONE")]
            public string STAGIONE { get; set; }

            [JsonPropertyName("TURNO")]
            public string TURNO { get; set; }

            [JsonPropertyName("Vinte")]
            public int Vinte { get; set; }

            [JsonPropertyName("VinteCasa")]
            public int VinteCasa { get; set; }

            [JsonPropertyName("VinteFuori")]
            public int VinteFuori { get; set; }

            [JsonPropertyName("do_deleted")]
            public int DoDeleted { get; set; }

            [JsonPropertyName("do_inserted")]
            public int DoInserted { get; set; }

            [JsonPropertyName("do_loaded")]
            public int DoLoaded { get; set; }

            [JsonPropertyName("do_updated")]
            public int DoUpdated { get; set; }

            [JsonPropertyName("team_active")]
            public string TeamActive { get; set; }

            [JsonPropertyName("team_image")]
            public string TeamImage { get; set; }

            [JsonPropertyName("team_image_secondary")]
            public string TeamImageSecondary { get; set; }

            [JsonPropertyName("team_slug")]
            public string TeamSlug { get; set; }
        }

        public class RankRoot
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("message")]
            public string Message { get; set; }

            [JsonPropertyName("errors")]
            public List<object> Errors { get; set; }

            [JsonPropertyName("data")]
            public List<RankDatum> Data { get; set; }

            [JsonPropertyName("code")]
            public int Code { get; set; }
        }

    }
}
