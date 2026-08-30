namespace Partcounter.Services;

public sealed record VeBoundaryPlan(
    uint TargetParts,
    uint TargetCycles,
    uint EffectiveQuantity,
    ushort HoldAfterVeNumber);

public sealed record OrderCapacityPlan(
    uint RequiredTotalCycles,
    ushort RequiredPackagingUnits);

public static class VeBoundaryPolicy
{
    public static VeBoundaryPlan Plan(
        ushort currentVeNumber,
        uint producedBeforeCurrentVe,
        uint orderTargetQuantity,
        uint standardVeTarget,
        ushort activeCavities)
    {
        ValidateBasicInputs(currentVeNumber, producedBeforeCurrentVe, orderTargetQuantity, standardVeTarget, activeCavities);

        if (currentVeNumber == 1 && producedBeforeCurrentVe == 0)
            _ = CalculateOrderCapacity(orderTargetQuantity, standardVeTarget, activeCavities);

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

    public static OrderCapacityPlan CalculateOrderCapacity(
        uint orderTargetQuantity,
        uint standardVeTarget,
        ushort activeCavities)
    {
        if (orderTargetQuantity == 0)
            throw new ArgumentOutOfRangeException(nameof(orderTargetQuantity));
        if (standardVeTarget == 0)
            throw new ArgumentOutOfRangeException(nameof(standardVeTarget));
        if (activeCavities is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(activeCavities));

        ulong remaining = orderTargetQuantity;
        ulong totalCycles = 0;
        uint ves = 0;

        while (remaining > 0)
        {
            var target = (uint)Math.Min((ulong)standardVeTarget, remaining);
            var cycles = CeilingDivide(target, activeCavities);
            if (cycles is 0 or > ModbusRegisterMap.MaxTargetCyclesPerVe)
                throw new InvalidOperationException($"Eine VE benötigt {cycles:N0} Zyklen; zulässig sind maximal {ModbusRegisterMap.MaxTargetCyclesPerVe:N0}.");

            totalCycles += cycles;
            if (totalCycles > ModbusRegisterMap.MaxTotalCyclesPerJob)
                throw new InvalidOperationException($"Der Auftrag benötigt {totalCycles:N0} LOGO!-Zyklen und überschreitet die freigegebene Grenze von {ModbusRegisterMap.MaxTotalCyclesPerJob:N0}. Auftrag segmentieren.");

            ves++;
            if (ves > ModbusRegisterMap.MaxVeNumber)
                throw new InvalidOperationException($"Der Auftrag benötigt mehr als {ModbusRegisterMap.MaxVeNumber:N0} VE. Auftrag aufteilen.");

            var effectiveQuantity = cycles * activeCavities;
            remaining = remaining > effectiveQuantity ? remaining - effectiveQuantity : 0;
        }

        return new OrderCapacityPlan((uint)totalCycles, (ushort)ves);
    }

    private static void ValidateBasicInputs(
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
    }

    private static ulong CeilingDivide(uint numerator, ushort denominator) =>
        ((ulong)numerator + denominator - 1UL) / denominator;
}
