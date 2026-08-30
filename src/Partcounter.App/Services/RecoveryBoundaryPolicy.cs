using Partcounter.Models;

namespace Partcounter.Services;

public enum RecoveryBoundaryAction
{
    ContinuePaused = 0,
    ReconfigureHeldBoundary = 1,
    FinalHeldBoundary = 2,
    Reject = 3
}

public sealed record RecoveryBoundaryDecision(RecoveryBoundaryAction Action, string? Error = null);

public static class RecoveryBoundaryPolicy
{
    public static RecoveryBoundaryDecision Decide(
        LogoSnapshot snapshot,
        ushort checkpointHold,
        ushort plannedHold,
        bool orderCompleted)
    {
        var holdEcho = snapshot.HoldAfterVeNumberEcho;
        if (holdEcho == 0)
            return Reject("LOGO! meldet keinen HoldAfterVE-Echo.");

        if (holdEcho != checkpointHold && holdEcho != plannedHold)
            return Reject($"HoldAfterVE-Echo {holdEcho} passt weder zu Checkpoint {checkpointHold} noch zur aktuellen Planung {plannedHold}.");

        var holdActive = (snapshot.StatusWord & ModbusRegisterMap.StatusCompletionHoldActive) != 0;
        if (snapshot.CompletedVes > holdEcho)
            return Reject($"LOGO! hat den geplanten Grenzhalt VE {holdEcho} überschritten (CompletedVEs={snapshot.CompletedVes}).");

        if (snapshot.CompletedVes == holdEcho)
        {
            if (!holdActive)
                return Reject($"VE {holdEcho} ist abgeschlossen, aber CompletionHoldActive ist nicht gesetzt.");
            return new RecoveryBoundaryDecision(orderCompleted
                ? RecoveryBoundaryAction.FinalHeldBoundary
                : RecoveryBoundaryAction.ReconfigureHeldBoundary);
        }

        if (holdActive)
            return Reject($"CompletionHoldActive ist vor der geplanten Grenze VE {holdEcho} aktiv (CompletedVEs={snapshot.CompletedVes}).");

        if (orderCompleted)
            return Reject("PC-Auftragsmenge ist vollständig, obwohl die geplante LOGO!-Hold-Grenze noch nicht erreicht wurde.");

        return new RecoveryBoundaryDecision(RecoveryBoundaryAction.ContinuePaused);
    }

    private static RecoveryBoundaryDecision Reject(string message) =>
        new(RecoveryBoundaryAction.Reject, message);
}
