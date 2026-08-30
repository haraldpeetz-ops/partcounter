using Partcounter.Models;

namespace Partcounter.Services;

public sealed record OperatingModeActivationPlan(
    IReadOnlyList<MachineState> LiveMachines,
    IReadOnlyList<MachineState> SimulationOrdersToDiscard,
    IReadOnlyList<int> DisabledRecoveryMachineNumbers)
{
    public int AdministrativelyDisabledCount { get; init; }
}

/// <summary>
/// Pure planning logic for the Simulation -> Echtbetrieb transition.
/// It deliberately separates administratively disabled stations from runtime-temporarily-disabled machines.
/// Recovery orders are never treated as disposable simulation orders.
/// </summary>
public static class OperatingModeActivationPolicy
{
    public static OperatingModeActivationPlan Build(
        IEnumerable<MachineState> machines,
        IEnumerable<int> startupRecoveryMachineNumbers)
    {
        ArgumentNullException.ThrowIfNull(machines);
        ArgumentNullException.ThrowIfNull(startupRecoveryMachineNumbers);

        var machineList = machines.ToList();
        var recovery = startupRecoveryMachineNumbers.ToHashSet();

        var liveMachines = machineList
            .Where(machine => machine.Configuration.Enabled)
            .OrderBy(machine => machine.Configuration.MachineNumber)
            .ToList();

        var simulationOrdersToDiscard = machineList
            .Where(machine => machine.IsActiveOrder && !recovery.Contains(machine.Configuration.MachineNumber))
            .OrderBy(machine => machine.Configuration.MachineNumber)
            .ToList();

        var disabledRecoveryMachines = machineList
            .Where(machine => recovery.Contains(machine.Configuration.MachineNumber) && !machine.Configuration.Enabled)
            .Select(machine => machine.Configuration.MachineNumber)
            .OrderBy(machineNumber => machineNumber)
            .ToList();

        return new OperatingModeActivationPlan(
            liveMachines,
            simulationOrdersToDiscard,
            disabledRecoveryMachines)
        {
            AdministrativelyDisabledCount = machineList.Count - liveMachines.Count
        };
    }
}
