namespace Fantasoccer.Application.Core.Football;

// Display/scoring window only. A recovery never reopens the original formation deadline.
public static class FootballLiveTiming
{
    public static bool IsRecoveryLive(bool wasPostponed, bool delayed, bool? isFinished, DateTimeOffset? kickoff, DateTimeOffset now) =>
        wasPostponed && !delayed && isFinished != true && kickoff is { } start && now >= start && now <= start.AddHours(10.25);
}
