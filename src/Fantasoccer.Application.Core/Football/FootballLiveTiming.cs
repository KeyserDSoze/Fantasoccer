namespace Fantasoccer.Application.Core.Football;

// Display/scoring window only. A recovery never reopens the original formation deadline.
public static class FootballLiveTiming
{
    public static DateTimeOffset DisplayStarts(DateTimeOffset firstKickoff) => firstKickoff.AddHours(-2);
    public static DateTimeOffset DisplayEnds(DateTimeOffset lastKickoff) => lastKickoff.AddMinutes(135).AddHours(14);
    public static bool IsPlaying(bool delayed, bool? finished, DateTimeOffset? kickoff, DateTimeOffset now) =>
        !delayed && finished != true && kickoff is { } start && now >= start && now < start.AddMinutes(finished == false ? 240 : 135);

    public static bool IsRecoveryLive(bool wasPostponed, bool delayed, bool? isFinished, DateTimeOffset? kickoff, DateTimeOffset now) =>
        wasPostponed && !delayed && isFinished != true && kickoff is { } start && now >= start && now <= start.AddHours(10.25);
}
