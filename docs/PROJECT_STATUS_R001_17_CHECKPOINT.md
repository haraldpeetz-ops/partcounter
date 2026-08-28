# Partcounter R001.17 – Projektcheckpoint

Stand: 28.08.2026
Branch: `r001.17-label-reprint`
Version: `0.1.17`

## Neu in R001.17

- Etiketten-Nachdruck direkt in der frei zugänglichen VE-Historie.
- Sichtbare Bedienleiste `Etikett nachdrucken…` und `Druckjournal anzeigen`.
- Nachdruck nur für VE-Datensätze, die als bereits gedruckt markiert sind.
- Nachdruckdialog mit Pflichtgrund und optionaler Bemerkung.
- Wiederverwendung des historischen `PackagingUnitRecord`; keine neue VE, keine neue VE-ID und keine Mengenänderung.
- Separate SQLite-Tabelle `LabelReprintJournal`.
- Laufende Nachdrucknummer pro VE.
- Protokollierung von Zeitpunkt, Drucker, Grund, Erfolg und Fehlertext.
- Zusätzliche Events `LABEL_REPRINT_OK` / `LABEL_REPRINT_ERROR`.
- Nachdruckjournal-Fenster je VE.
- Bestehende Modbus-V2-/LOGO-V001-/VE-Wechsler-Logik bleibt unverändert.

## Wichtige Abgrenzung

R001.17 reproduziert die historischen VE-Daten. Das Rendering verwendet das aktuell für den Datensatz aufgelöste Etikettenlayout. Ein unveränderlicher Layout-Snapshot des ursprünglichen Drucks wird in R001.17 noch nicht archiviert.

## Dateien

- `src/Partcounter.App/Models/LabelReprintModels.cs`
- `src/Partcounter.App/Services/LabelReprintService.cs`
- `src/Partcounter.App/Services/LabelReprintBootstrap.cs`
- `src/Partcounter.App/Views/LabelReprintDialog.cs`
- `src/Partcounter.App/Views/LabelReprintJournalWindow.cs`
- `docs/LABEL_REPRINT_R001_17.md`
- `src/Partcounter.App/App.xaml.cs` – Reprint-Modul eingebunden
- `src/Partcounter.App/Partcounter.App.csproj` – Version 0.1.17

## Validierungsstatus

- Quellintegration abgeschlossen.
- Temporärer CI-PR #5 wurde ausschließlich zur Windows-CI-Validierung geöffnet und anschließend ohne Merge geschlossen.
- GitHub startete für diesen PR keinen Workflow-Lauf.
- Die bestehende Workflowdatei konnte in dieser Sitzung durch den Connector nicht auf R001.17 umgestellt werden.
- Ein neuer R001.17-Binärbuild ist deshalb noch ausstehend; der letzte vollständig CI-validierte Binärstand bleibt R001.16.

## Nächster Schritt

1. R001.17 auf Windows/.NET 8 kompilieren.
2. Reprint mit realem Drucker testen.
3. Erfolgs- und Fehlerfall im Nachdruckjournal prüfen.
4. Danach SingleFile, Portable, Engineering und UpdatePackage als R001.17 veröffentlichen.
