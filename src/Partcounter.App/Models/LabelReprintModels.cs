namespace Partcounter.Models;

public sealed record LabelReprintJournalEntry(
    long Id,
    string PackagingUnitId,
    int ReprintNumber,
    DateTime PrintedAtUtc,
    string PrinterName,
    string Reason,
    bool Successful,
    string ErrorMessage,
    string LayoutSource)
{
    public string PrintedAtLocalText => PrintedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
    public string ResultText => Successful ? "ERFOLGREICH" : "FEHLER";
}

public sealed record LabelReprintResult(
    bool Successful,
    int ReprintNumber,
    string PrinterName,
    string Reason,
    string ErrorMessage,
    DateTime AttemptedAtUtc,
    string LayoutSource,
    bool HistoricalSnapshotUsed);
