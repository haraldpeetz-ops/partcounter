using Partcounter.Services;
using Xunit;

namespace Partcounter.Tests;

public sealed class HelpResourceTests
{
    [Fact]
    public void CurrentHelpDatabase_LoadsR00125RecoveryAndOrderSourceTopics()
    {
        var help = new PartcounterHelpService();

        Assert.NotEmpty(help.Topics);
        Assert.NotNull(help.Find("START-01"));
        Assert.NotNull(help.Find("SOURCE-01"));
        Assert.NotNull(help.Find("RECOVERY-01"));
        Assert.NotNull(help.Find("RECOVERY-02"));
        Assert.NotNull(help.Find("RECOVERY-03"));
        Assert.NotNull(help.Find("BOUNDARY-01"));
        Assert.NotNull(help.Find("MODBUS-03"));
        Assert.NotNull(help.Find("RECOVERY-04"));
    }

    [Fact]
    public void HelpTopicIds_AreUnique()
    {
        var help = new PartcounterHelpService();
        var duplicates = help.Topics
            .GroupBy(topic => topic.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void RecoverySearch_FindsTheCurrentTopic()
    {
        var help = new PartcounterHelpService();
        var matches = help.Filter("JobId Recovery", null);

        Assert.Contains(matches, topic => topic.Id == "RECOVERY-01" || topic.Id == "RECOVERY-03");
    }
}
