namespace BotFramework.Contracts.Operations;

public interface IEconomySimulationService
{
    EconomySimulationReport Simulate(EconomySimulationRequest request);
}
