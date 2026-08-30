using Partcounter.Models;
using Partcounter.Services;
using Xunit;

namespace Partcounter.Tests;

public sealed class OperatingModeActivationPolicyTests
{
    [Fact]
    public void DisabledConfiguration_IsExcludedFromFleetButDoesNotBlockNormalLiveMode()
    {
        var enabled = NewMachine(1, enabled: true);
        var disabled = NewMachine(2, enabled: false);

        var plan = OperatingModeActivationPolicy.Build(new[] { enabled, disabled }, Array.Empty<int>());

        Assert.Single(plan.LiveMachines);
        Assert.Equal(1, plan.LiveMachines[0].Configuration.MachineNumber);
        Assert.Equal(1, plan.AdministrativelyDisabledCount);
        Assert.Empty(plan.DisabledRecoveryMachineNumbers);
    }

    [Fact]
    public void ActiveSimulationOrder_IsMarkedForControlledDiscard()
    {
        var machine = NewMachine(1, enabled: true);
        machine.StartOrder(NewArticle(), "SIM-1", 1000);

        var plan = OperatingModeActivationPolicy.Build(new[] { machine }, Array.Empty<int>());

        Assert.Single(plan.SimulationOrdersToDiscard);
        Assert.Same(machine, plan.SimulationOrdersToDiscard[0]);
    }

    [Fact]
    public void RecoveryOrder_IsNeverMarkedAsDisposableSimulationOrder()
    {
        var machine = NewMachine(1, enabled: true);
        machine.RestoreRecoveredOrder(NewCheckpoint(1));

        var plan = OperatingModeActivationPolicy.Build(new[] { machine }, new[] { 1 });

        Assert.Empty(plan.SimulationOrdersToDiscard);
        Assert.Empty(plan.DisabledRecoveryMachineNumbers);
    }

    [Fact]
    public void DisabledRecoveryMachine_IsExplicitBlocker()
    {
        var machine = NewMachine(7, enabled: false);
        machine.RestoreRecoveredOrder(NewCheckpoint(7));

        var plan = OperatingModeActivationPolicy.Build(new[] { machine }, new[] { 7 });

        Assert.Empty(plan.LiveMachines);
        Assert.Equal(new[] { 7 }, plan.DisabledRecoveryMachineNumbers);
    }

    private static MachineState NewMachine(int number, bool enabled) => new()
    {
        Configuration = new MachineConfiguration(number, $"M{number:00}", $"192.168.50.{100 + number}", 502, 1, enabled)
    };

    private static ArticleDefinition NewArticle() =>
        new(1, "A-1", "Test", "WZ-1", 4, 1000, true);

    private static ActiveOrderCheckpoint NewCheckpoint(int machineNumber) => new(
        machineNumber,
        "REC-1",
        1234,
        "A-1",
        "Test",
        "WZ-1",
        4,
        1000,
        5000,
        ProductionOrderState.Paused,
        5,
        false,
        false,
        0,
        0,
        0,
        1,
        0,
        0,
        ActiveOrderCheckpointPhase.Active,
        DateTime.UtcNow);
}
