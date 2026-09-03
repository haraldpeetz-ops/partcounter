namespace Partcounter.Services;

/// <summary>
/// Parses the stable Mxx-prefix produced by MainViewModel recovery diagnostics.
/// Keeping this mapping pure makes the HF4 rule testable: only machines that actually
/// failed reconciliation remain blocked while the global live-mode fleet stays active.
/// </summary>
public static class LiveModeRecoveryPolicy
{
    public static IReadOnlySet<int> ExtractFailedMachineNumbers(IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var failed = new HashSet<int>();
        foreach (var error in errors)
        {
            if (TryExtractMachineNumber(error, out var machineNumber))
                failed.Add(machineNumber);
        }

        return failed;
    }

    public static bool TryExtractMachineNumber(string? error, out int machineNumber)
    {
        machineNumber = 0;
        if (string.IsNullOrWhiteSpace(error) || error.Length < 4 || error[0] != 'M')
            return false;

        var colon = error.IndexOf(':');
        if (colon <= 1)
            return false;

        var numberText = error.AsSpan(1, colon - 1).Trim();
        return int.TryParse(numberText, out machineNumber) && machineNumber > 0;
    }
}
