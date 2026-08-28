# Partcounter R001.18 – Projektcheckpoint

Stand: 28.08.2026
Branch: `r001.18-label-snapshot-als-hotfix`
Version: `0.1.18`

## Enthalten

- ARBURG-ALS-Feldmapping-Hotfix für den WPF-Fehler bei `AlsFieldMapping.Required`.
- Read-only Mapping-Felder werden vor DataContext-Bindung explizit auf `OneWay` gesetzt.
- Historischer Etiketten-Layout-Snapshot pro regulär gedruckter VE.
- Vollständige Vorlagendefinition inklusive eingebetteter Logos/Bilder.
- SHA-256-Integritätsprüfung des archivierten Layouts.
- Nachdruck aus Original-Snapshot, wenn vorhanden.
- Transparenter Fallback auf aktuelles Layout für ältere VE ohne Snapshot.
- Layoutquelle im Nachdruckjournal.
- R001.17 Reprint-Funktion, R001.16 Live-Abnahme und alle vorherigen Funktionen bleiben erhalten.
- Modbus Protocol V2 und Partcounter_LOGO_V001 unverändert.

## Validierung

Der dedizierte Workflow `.github/workflows/build-r00118.yml` erzeugt Release-Build, Portable Folder, SingleFile, Engineering und UpdatePackage für Windows x64.
