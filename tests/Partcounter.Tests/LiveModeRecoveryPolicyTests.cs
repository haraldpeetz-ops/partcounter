using Partcounter.Services;
using Xunit;

namespace Partcounter.Tests;

public sealed class LiveModeRecoveryPolicyTests
{
    [Fact]
    public void ExtractFailedMachineNumbers_MapsOnlyReportedRecoveryMachines()
    {
        var failed = LiveModeRecoveryPolicy.ExtractFailedMachineNumbers(new[]
        {
            "M01: Connection refused",
            "M07: JobIdEcho 0 != Recovery-JobId 1234.",
            "M01: second diagnostic detail"
        });

        Assert.Equal(new[] { 1, 7 }, failed.OrderBy(x => x).ToArray());
    }

    [Theory]
    [InlineData("M01: offline", 1)]
    [InlineData("M30: protocol mismatch", 30)]
    [InlineData("M7: timeout", 7)]
    public void TryExtractMachineNumber_AcceptsRecoveryPrefix(string text, int expected)
    {
        Assert.True(LiveModeRecoveryPolicy.TryExtractMachineNumber(text, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("offline")]
    [InlineData("MX1: timeout")]
    [InlineData("M00: invalid")]
    public void TryExtractMachineNumber_RejectsMalformedDiagnostics(string? text)
    {
        Assert.False(LiveModeRecoveryPolicy.TryExtractMachineNumber(text, out _));
    }

    [Fact]
    public void EmptyRecoveryErrorList_LeavesNoBlockedMachineNumbers()
    {
        Assert.Empty(LiveModeRecoveryPolicy.ExtractFailedMachineNumbers(Array.Empty<string>()));
    }
}
