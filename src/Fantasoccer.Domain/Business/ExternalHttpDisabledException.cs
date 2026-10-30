namespace Fantasoccer.Business;

// A local safety switch, not a failure of the external provider. Keep it distinguishable at the HTTP boundary.
public sealed class ExternalHttpDisabledException : HttpRequestException
{
    public ExternalHttpDisabledException() : base("External data fetching is disabled in this environment.") { }
}
