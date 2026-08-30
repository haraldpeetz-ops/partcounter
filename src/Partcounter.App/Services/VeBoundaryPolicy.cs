namespace Partcounter.Services;

public sealed record VeBoundaryPlan(
    uint TargetParts,
    uint TargetCycles,
    uint EffectiveQuantity,
    ushort HoldAfterVeNumber);

public static class VeBoundaryPolicy
{
    public static VeBoundaryPlan Plan(
        ushort currentVeNumber,
        uint producedBeforeCurrentVe,
        uint orderTargetQuantity,
        uint standardVeTarget,
        ushort activeCavities)
    {
        if (currentVeNumber is 0 or > ModbusRegisterMap.MaxVeNumber)
            throw new ArgumentOutOfRangeException(nameof(currentVeNumber));
        if (orderTargetQuantity == 0)
            throw new ArgumentOutOfRangeException(nameof(orderTargetQuantity));
        if (standardVeTarget == 0)
            throw new ArgumentOutOfRangeException(nameof(standardVeTarget));
        if (activeCavities is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(activeCavities));
        if (producedBeforeCurrentVe >= orderTargetQuantity)
            throw new ArgumentOutOfRangeException(nameof(producedBeforeCurrentVe), "Der Auftrag ist bereits vollständig produziert.");

        var remaining = (ulong)orderTargetQuantity - producedBeforeCurrentVe;
        var currentTarget = (uint)Math.Min((ulong)standardVeTarget, remaining);
        var currentCycles = CeilingDivide(currentTarget, activeCavities);
        if (currentCycles is 0 or > ModbusRegisterMap.MaxTargetCyclesPerVe)
            throw new InvalidOperationException($"Das VE-Ziel benötigt {currentCycles:N0} Zyklen; zulässig sind maximal {ModbusRegisterMap.MaxTargetCyclesPerVe:N0}.");

        var effectiveCurrent = checked((uint)(currentCycles * activeCavities));
        ulong stepsUntilBoundary;

        if (currentTarget < standardVeTarget)
        {
            stepsUntilBoundary = 1;
        }
        else
        {
            var standardCycles = CeilingDivide(standardVeTarget, activeCavities);
            if (standardCycles is 0 or > ModbusRegisterMap.MaxTargetCyclesPerVe)
                throw new InvalidOperationException($"Die Standard-VE benötigt {standardCycles:N0} Zyklen; zulässig sind maximal {ModbusRegisterMap.MaxTargetCyclesPerVe:N0}.");

            var effectiveStandard = standardCycles * activeCavities;
            stepsUntilBoundary = ((remaining - standardVeTarget) / effectiveStandard) + 1;
        }

        var boundary = (ulong)currentVeNumber + stepsUntilBoundary - 1;
        if (boundary > ModbusRegisterMap.MaxVeNumber)
            throw new InvalidOperationException($"Der Auftrag würde die LOGO!-VE-Grenze {ModbusRegisterMap.MaxVeNumber:N0} überschreiten. Auftrag aufteilen.");

        return new VeBoundaryPlan(
            currentTarget,
            (uint)currentCycles,
            effectiveCurrent,
            (ushort)boundary);
    }

    private static ulong CeilingDivide(uint numerator, ushort denominator) =>
        ((ulong)numerator + denominator - 1UL) / denominator;
}
