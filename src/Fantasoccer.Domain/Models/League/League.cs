namespace Fantasoccer
{
    public record League(string Name, bool IsMain, int Participant, List<AnnualLeague> Years);
}