using System.Printing;
using System.Windows.Documents;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class LabelPrintService
{
    private readonly LabelTemplateService _templates = new();
    private readonly LabelRenderService _renderer = new();
    private readonly LabelPrintSnapshotService _snapshots = new();

    public async Task<bool> PrintAsync(PackagingUnitRecord record, string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return false;

        try
        {
            var template = await _templates.ResolveTemplateAsync(record);

            // R001.18: Beim regulären Erst-/Produktionsdruck wird die tatsächlich verwendete
            // Vorlagendefinition unveränderlich pro VE archiviert. Ein Snapshot-Fehler darf
            // den Produktionsdruck selbst nicht blockieren; ältere VE fallen beim Reprint
            // kontrolliert auf das aktuelle Layout zurück.
            if (ShouldCaptureSnapshot(record))
            {
                try
                {
                    await _snapshots.SaveSnapshotIfMissingAsync(record, template);
                }
                catch
                {
                    // Der Druck selbst hat Vorrang. Die Reprintfunktion kennzeichnet später
                    // transparent, wenn für eine VE kein historischer Snapshot vorhanden ist.
                }
            }

            return PrintDocument(_renderer.BuildDocument(record, template), printerName);
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> PrintTemplateAsync(
        PackagingUnitRecord record,
        LabelTemplateDefinition template,
        string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return Task.FromResult(false);

        try
        {
            return Task.FromResult(PrintDocument(_renderer.BuildDocument(record, template), printerName));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<FixedDocument> BuildDocumentAsync(PackagingUnitRecord record)
    {
        var template = await _templates.ResolveTemplateAsync(record);
        return _renderer.BuildDocument(record, template);
    }

    public FixedDocument BuildDocument(PackagingUnitRecord record) =>
        _renderer.BuildDocument(record, LabelTemplateService.CreateLegacyCompatibleDefaultTemplate());

    private static bool ShouldCaptureSnapshot(PackagingUnitRecord record) =>
        record.Id.StartsWith("PC-", StringComparison.OrdinalIgnoreCase) &&
        (string.Equals(record.LabelStatus, "Pending", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(record.LabelStatus, "PendingPrinter", StringComparison.OrdinalIgnoreCase));

    private static bool PrintDocument(FixedDocument document, string printerName)
    {
        try
        {
            using var printServer = new LocalPrintServer();
            using var queue = printServer.GetPrintQueue(printerName.Trim());
            var writer = PrintQueue.CreateXpsDocumentWriter(queue);
            writer.Write(document.DocumentPaginator);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
