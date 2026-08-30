using Partcounter.Services;
using Xunit;

namespace Partcounter.Tests;

public sealed class VeBoundaryPolicyTests
{
    [Fact]
    public void ExactTwoVeOrder_HoldsAtFinalVe()
    {
        var plan = VeBoundaryPolicy.Plan(1, 0, 2000, 1000, 1);
        Assert.Equal((uint)1000, plan.TargetParts);
        Assert.Equal((uint)1000, plan.TargetCycles);
        Assert.Equal((ushort)2, plan.HoldAfterVeNumber);
    }

    [Fact]
    public void SixtyFourCavities_SchedulesHoldBeforePartialVe()
    {
        var plan = VeBoundaryPolicy.Plan(1, 0, 2500, 1000, 64);
        Assert.Equal((uint)1000, plan.TargetParts);
        Assert.Equal((uint)16, plan.TargetCycles);
        Assert.Equal((uint)1024, plan.EffectiveQuantity);
        Assert.Equal((ushort)2, plan.HoldAfterVeNumber);
    }

    [Fact]
    public void PartialFinalVe_IsHeldAtItsOwnCompletion()
    {
        var plan = VeBoundaryPolicy.Plan(3, 2048, 2500, 1000, 64);
        Assert.Equal((uint)452, plan.TargetParts);
        Assert.Equal((uint)8, plan.TargetCycles);
        Assert.Equal((uint)512, plan.EffectiveQuantity);
        Assert.Equal((ushort)3, plan.HoldAfterVeNumber);
    }

    [Fact]
    public void LongUniformOrder_RemainsAutonomousUntilRealBoundary()
    {
        var plan = VeBoundaryPolicy.Plan(1, 0, 10000, 1000, 8);
        Assert.Equal((ushort)10, plan.HoldAfterVeNumber);
    }

    [Fact]
    public void CavityOverfill_CanMoveBoundaryEarlier()
    {
        var plan = VeBoundaryPolicy.Plan(1, 0, 2000, 1000, 64);
        Assert.Equal((ushort)1, plan.HoldAfterVeNumber);
    }

    [Fact]
    public void LogoTotalCycleLimit_IsRejectedBeforeOrderStart()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            VeBoundaryPolicy.Plan(1, 0, 1_000_000, 1000, 1));
        Assert.Contains("999.999", ex.Message);
    }

    [Fact]
    public void ExactLogoTotalCycleLimit_IsAccepted()
    {
        var capacity = VeBoundaryPolicy.CalculateOrderCapacity(999_999, 1000, 1);
        Assert.Equal((uint)999_999, capacity.RequiredTotalCycles);
        Assert.Equal((ushort)1000, capacity.RequiredPackagingUnits);
    }

    [Fact]
    public void OrdersBeyondLogoVeRange_AreRejectedBeforeStart()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            VeBoundaryPolicy.Plan(1, 0, 40000, 1, 1));
        Assert.Contains("Auftrag aufteilen", ex.Message);
    }
}
