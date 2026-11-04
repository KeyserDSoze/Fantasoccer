namespace Fantasoccer.Application.Core.Operations;

// How many runs of each job the administrators' log page shows. Every job has its own share: a job that runs every minute
// must not push out the runs of one that runs once a day. Failures of a job are kept in view on their own, up to the same number.
public static class SqlJobLogPolicy
{
    public static int Limit(string job) => job switch
    {
        // Every minute / every few minutes: the last run says little, twenty show the trend.
        "formations" or "live" or "live-projections" or "postponed-results" or "calendar" or "official" or "statistics" or "results" => 20,
        // A few runs a day, or retried until they succeed: a few days of history.
        "chances" or "hall" => 30,
        // Once a day or a year: the last ten are plenty.
        _ => 10,
    };

    // Per source of operational failures (imports, job runners, push phases): twenty each.
    public const int FailureSourceLimit = 20;
}
