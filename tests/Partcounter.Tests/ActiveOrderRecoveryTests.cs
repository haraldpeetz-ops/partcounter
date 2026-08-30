using Partcounter.Models;
using Partcounter.Services;
using Xunit;

namespace Partcounter.Tests;

public sealed class ActiveOrderRecoveryTests
{
    [Fact]
    public async Task RecoveryStore_RoundTripsCheckpoint()
    {
        var root = Path.Combine(Path.GetTempPath(), "PartcounterRecoveryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var db = Path.Combine(root, "recovery.db");
        var service = new ActiveOrderRecoveryService(db);
        await service.InitializeAsync();

        var checkpoint = CreateCheckpoint();
        await service.UpsertAsync(checkpoint);
        var loaded = Assert.Single(await service.LoadAsync());
        Assert.Equal(checkpoint.MachineNumber, loaded.MachineNumber);
        Assert.Equal(checkpoint.OrderNumber, loaded.OrderNumber);
        Assert.Equal(checkpoint.JobId, loaded.JobId);
        Assert.Equal(checkpoint.ScheduledHoldAfterVeNumber, loaded.ScheduledHoldAfterVeNumber);
        Assert.Equal(checkpoint.ManualVeReconfigurationPending, loaded.ManualVeReconfigurationPending);
        Assert.Equal(checkpoint.Phase, loaded.Phase);

        await service.DeleteAsync(checkpoint.MachineNumber);
        Assert.Empty(await service.LoadAsync());
    }

    [Fact]
    public void MachineState_RestoresLiveSnapshotPausedWithoutDuplicateCompletion()
    {
        var machine = new MachineState
        {
            Configuration = new MachineConfiguration(1, "M01", "127.0.0.1")
        };
        var checkpoint = CreateCheckpoint();
        var snapshot = new LogoSnapshot(
            CurrentParts: 256,
            TotalCycles: 20,
            CurrentVeNumber: 2,
            CompletedVes: 1,
            LastCompletedVeQuantity: 1024,
            StatusWord: (ushort)(ModbusRegisterMap.StatusReady | ModbusRegisterMap.StatusCompletionHoldArmed),
            AcknowledgedCommandSequence: 7,
            ActiveCavitiesEcho: 64,
            LastCompletedVeNumber: 1,
            CompletionSequence: 5,
            LogoHeartbeat: 10,
            ErrorCode: 0,
            LastCompletionReason: VeCompletionReason.AutomaticFull,
            ReadAtUtc: DateTime.UtcNow,
            HoldAfterVeNumberEcho: 2,
            JobIdEcho: checkpoint.JobId);

        var completions = 0;
        machine.VeCompleted += (_, _) => completions++;
        machine.RestoreRecoveredOrder(checkpoint, snapshot);

        Assert.Equal(ProductionOrderState.Paused, machine.OrderState);
        Assert.Equal((uint)1280, machine.OrderProducedQuantity);
        Assert.Equal((uint)1000, machine.CurrentVeTargetParts);
        Assert.Equal((ushort)2, machine.CurrentVeNumber);

        machine.ApplyLogoSnapshot(snapshot);
        Assert.Equal(0, completions);
    }

    [Fact]
    public void PendingFinalVe_RestoresCorrectOriginalVeTarget()
    {
        var machine = new MachineState
        {
            Configuration = new MachineConfiguration(1, "M01", "127.0.0.1")
        };
        var checkpoint = CreateCheckpoint() with
        {
            OrderTargetQuantity = 2500,
            StandardVeTarget = 1000,
            ScheduledHoldAfterVeNumber = 3
        };
        var snapshot = new LogoSnapshot(
            CurrentParts: 128,
            TotalCycles: 34,
            CurrentVeNumber: 3,
            CompletedVes: 2,
            LastCompletedVeQuantity: 1024,
            StatusWord: ModbusRegisterMap.StatusCompletionHoldArmed,
            AcknowledgedCommandSequence: 8,
            ActiveCavitiesEcho: 64,
            LastCompletedVeNumber: 2,
            CompletionSequence: 6,
            LogoHeartbeat: 11,
            ErrorCode: 0,
            LastCompletionReason: VeCompletionReason.AutomaticFull,
            ReadAtUtc: DateTime.UtcNow,
            HoldAfterVeNumberEcho: 3,
            JobIdEcho: checkpoint.JobId);

        machine.RestoreRecoveredOrder(checkpoint, snapshot);
        Assert.Equal((uint)452, machine.CurrentVeTargetParts);
    }

    [Fact]
    public void PendingActivation_IsDiscardableOnlyForProvablyIdleLogo()
    {
        var idle = Snapshot(jobId: 0, totalCycles: 0, currentParts: 0, completedVes: 0, statusWord: 0);
        Assert.True(RecoveryIdentityPolicy.IsProvablyIdleForPendingActivation(idle));

        Assert.False(RecoveryIdentityPolicy.IsProvablyIdleForPendingActivation(
            Snapshot(jobId: 999, totalCycles: 0, currentParts: 0, completedVes: 0, statusWord: 0)));
        Assert.False(RecoveryIdentityPolicy.IsProvablyIdleForPendingActivation(
            Snapshot(jobId: 0, totalCycles: 1, currentParts: 64, completedVes: 0, statusWord: 0)));
        Assert.False(RecoveryIdentityPolicy.IsProvablyIdleForPendingActivation(
            Snapshot(jobId: 0, totalCycles: 0, currentParts: 0, completedVes: 0, statusWord: ModbusRegisterMap.StatusAutomaticEnabled)));
    }

    [Fact]
    public void RecoveryBoundary_RequiresReconfigurationAtHeldBoundary()
    {
        var snapshot = Snapshot(jobId: 1, totalCycles: 100, currentParts: 0, completedVes: 10,
            statusWord: ModbusRegisterMap.StatusCompletionHoldActive | ModbusRegisterMap.StatusCompletionHoldArmed, holdAfterVe: 10);
        var decision = RecoveryBoundaryPolicy.Decide(snapshot, checkpointHold: 10, plannedHold: 11, orderCompleted: false);
        Assert.Equal(RecoveryBoundaryAction.ReconfigureHeldBoundary, decision.Action);
    }

    [Fact]
    public void RecoveryBoundary_RejectsPassedHoldWithoutLocalStop()
    {
        var snapshot = Snapshot(jobId: 1, totalCycles: 110, currentParts: 0, completedVes: 11,
            statusWord: ModbusRegisterMap.StatusCompletionHoldArmed, holdAfterVe: 10);
        var decision = RecoveryBoundaryPolicy.Decide(snapshot, checkpointHold: 10, plannedHold: 12, orderCompleted: false);
        Assert.Equal(RecoveryBoundaryAction.Reject, decision.Action);
        Assert.Contains("überschritten", decision.Error);
    }

    [Fact]
    public void RecoveryBoundary_FinalOrderRequiresActiveFinalHold()
    {
        var held = Snapshot(jobId: 1, totalCycles: 100, currentParts: 0, completedVes: 10,
            statusWord: ModbusRegisterMap.StatusCompletionHoldActive | ModbusRegisterMap.StatusCompletionHoldArmed, holdAfterVe: 10);
        Assert.Equal(RecoveryBoundaryAction.FinalHeldBoundary,
            RecoveryBoundaryPolicy.Decide(held, 10, 10, orderCompleted: true).Action);

        var notHeld = held with { StatusWord = ModbusRegisterMap.StatusCompletionHoldArmed };
        Assert.Equal(RecoveryBoundaryAction.Reject,
            RecoveryBoundaryPolicy.Decide(notHeld, 10, 10, orderCompleted: true).Action);
    }

    [Fact]
    public void RecoveryBoundary_AllowsUpdatedHoldEchoMatchingCurrentPlan()
    {
        var snapshot = Snapshot(jobId: 1, totalCycles: 50, currentParts: 128, completedVes: 5,
            statusWord: ModbusRegisterMap.StatusCompletionHoldArmed, holdAfterVe: 8);
        var decision = RecoveryBoundaryPolicy.Decide(snapshot, checkpointHold: 7, plannedHold: 8, orderCompleted: false);
        Assert.Equal(RecoveryBoundaryAction.ContinuePaused, decision.Action);
    }

    private static LogoSnapshot Snapshot(uint jobId, uint totalCycles, uint currentParts, ushort completedVes, ushort statusWord, ushort holdAfterVe = 0) => new(
        CurrentParts: currentParts,
        TotalCycles: totalCycles,
        CurrentVeNumber: 1,
        CompletedVes: completedVes,
        LastCompletedVeQuantity: 0,
        StatusWord: statusWord,
        AcknowledgedCommandSequence: 0,
        ActiveCavitiesEcho: 1,
        LastCompletedVeNumber: 0,
        CompletionSequence: 0,
        LogoHeartbeat: 1,
        ErrorCode: 0,
        LastCompletionReason: VeCompletionReason.Unknown,
        ReadAtUtc: DateTime.UtcNow,
        HoldAfterVeNumberEcho: holdAfterVe,
        JobIdEcho: jobId);

    private static ActiveOrderCheckpoint CreateCheckpoint() => new(
        MachineNumber: 1,
        OrderNumber: "AUF-RECOVERY-001",
        JobId: 123456u,
        ArticleNumber: "A-01",
        ArticleDescription: "Recovery Test",
        ToolNumber: "WZ-01",
        ActiveCavities: 64,
        StandardVeTarget: 1000,
        OrderTargetQuantity: 10000,
        OrderState: ProductionOrderState.Running,
        ScheduledHoldAfterVeNumber: 2,
        ManualVeReconfigurationPending: false,
        IsTemporarilyDisabled: false,
        LastKnownOrderProducedQuantity: 1024,
        LastKnownCurrentParts: 0,
        LastKnownTotalCycles: 16,
        LastKnownCurrentVeNumber: 2,
        LastKnownCompletedVes: 1,
        LastKnownLastCompletedVeQuantity: 1024,
        Phase: ActiveOrderCheckpointPhase.Active,
        UpdatedAtUtc: DateTime.UtcNow);
}
