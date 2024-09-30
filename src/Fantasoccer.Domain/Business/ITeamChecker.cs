namespace Fantasoccer.Business
{
    public interface ITeamChecker
    {
        TeamResponse Validate(Team team);
    }
}
