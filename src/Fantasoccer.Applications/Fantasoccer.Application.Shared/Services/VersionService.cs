namespace Fantasoccer.Application.Shared
{
    public sealed class VersionService
    {
        public string Version { get; set; }
        public VersionService()
        {
            Version = "0.5.1-beta";
        }
    }
}
