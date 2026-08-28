namespace Partcounter.Models;

public sealed record LabelPrintSnapshot(
    string PackagingUnitId,
    string TemplateId,
    string TemplateName,
    DateTime TemplateUpdatedAtUtc,
    string DefinitionSha256,
    DateTime CapturedAtUtc,
    LabelTemplateDefinition Template)
{
    public string CapturedAtLocalText => CapturedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
    public string ShortHash => DefinitionSha256.Length <= 12 ? DefinitionSha256 : DefinitionSha256[..12];
    public string DisplayText => $"{TemplateName} · {TemplateId} · Snapshot {CapturedAtLocalText} · SHA256 {ShortHash}…";
}
