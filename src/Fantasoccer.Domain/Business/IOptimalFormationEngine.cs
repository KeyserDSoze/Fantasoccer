namespace Fantasoccer.Business
{
    public interface IOptimalFormationEngine
    {
        OptimalFormationResult Calculate(OptimalFormationInput input);
    }
}
