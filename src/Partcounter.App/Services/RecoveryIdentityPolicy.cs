using Partcounter.Models;

namespace Partcounter.Services;

public static class RecoveryIdentityPolicy
{
    public static bool IsProvablyIdleForPendingActivation(LogoSnapshot snapshot) =>
        snapshot.JobIdEcho == 0 &&
        snapshot.TotalCycles == 0 &&
        snapshot.CurrentParts == 0 &&
        snapshot.CompletedVes == 0 &&
        (snapshot.StatusWord & ModbusRegisterMap.StatusAutomaticEnabled) == 0;
}
