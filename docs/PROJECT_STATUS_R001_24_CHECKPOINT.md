# Partcounter R001.24 – Projektcheckpoint

Stand: 30.08.2026  
Branch: `r001.24-adaptive-ui-logo-guide`  
Version: `0.1.24` / FileVersion `0.1.24.0`

## Ziel der Revision

R001.24 beseitigt die historische Abhängigkeit der WPF-Oberfläche von großen Desktopauflösungen und macht die Bedienung auf Notebook-/kleinen Arbeitsflächen sowie bei Windows-DPI-Skalierung systematisch prüfbar. Zusätzlich enthält R001.24 einen vollständigen Siemens-LOGO!-Engineering-Leitfaden für das aktuell von Partcounter verwendete Modbus-TCP-Protokoll V2.

## Adaptive UI – umgesetzte Maßnahmen

- zentrale `AdaptiveUiService`-Schicht für alle WPF-Fenster,
- explizites `PerMonitorV2`-DPI-Manifest,
- `asInvoker`, damit die DPI-Lösung keine Adminrechte verlangt,
- Begrenzung aller Fenster auf die reale Windows-Arbeitsfläche,
- automatische Entschärfung historisch zu großer Mindestbreiten/-höhen,
- DataGrid horizontal/vertikal scrollbar,
- Touch-/Trackpad-Panning für ScrollViewer,
- konservative Kompression großer fester Grid-Spalten,
- temporäre Auflösung übergroßer fester Inhaltsbreiten,
- adaptive Leitstand-Kachelspalten 1…5,
- kompakte/vertikale Kopfzeile auf kleinen Arbeitsflächen,
- automatische Multi-Resolution-Layoutprüfung als dauerhafte CI-Freigabebedingung.

## Validierter Code-Stand

Der erste vollständige R001.24-Codebuild wurde in GitHub Actions Run `33279312648` auf Commit `93074ecc04f9d5d59a541e8d5fe08505be7c2acc` validiert.

Ergebnis:

- Release Build: PASS,
- Portable Publish: PASS,
- SingleFile Publish: PASS,
- realer WPF-Simulations-Stresstest: PASS,
- Multi-Resolution WPF Layout Smoke Test: PASS.

### Layout-Test

Die veröffentlichte WPF-Anwendung wurde durch alle sichtbaren Haupt- und verschachtelten Unterreiter geschaltet. Getestete logische Viewports:

- 800×500,
- 1024×600,
- 1280×720,
- 1366×768,
- 1600×900,
- 1920×1080.

Ergebnis: **keine nicht-scrollbaren Überläufe erkannt**.

Damit ist auch 1024×600 und sogar 800×500 als automatischer geometrischer Härtungstest enthalten – deutlich unterhalb typischer heutiger Notebookauflösungen.

### Stresstest R001.24

Der R001.24-Code bestand zusätzlich erneut:

- 30 Maschinen,
- 1.920.000 simulierte Sollteile,
- 1.920 VE-Ereignisse,
- 1.920/1.920 VE-Datensätze persistiert,
- SQLite quick_check: ok,
- 15.000 proALPHA-Parserdatensätze,
- 5.000 ALS-Parserdatensätze,
- keine Fehler/kein Absturz.

## Dauerhafte Qualitätsregel

Künftige Partcounter-Revisionen müssen weiterhin sowohl den realen WPF-Stresstest als auch den Multi-Resolution-Layout-Smoke-Test bestehen. Neue Views/Fenster gelten nicht als abgeschlossen, wenn sie auf einem der definierten Viewports einen nicht scrollbar erreichbaren Inhalt erzeugen.

## Siemens LOGO! – vollständiger Engineering-Stand

Hauptdokument:

`PARTCOUNTER_LOGO_V001_R001_24_COMPLETE_ENGINEERING_GUIDE.md`

Zusätzliche Aufbauunterlagen:

- `logo_v001/LOGO_V001_VM_MAP_R001_24.csv`
- `logo_v001/LOGO_V001_IO_WIRING_R001_24.csv`
- `logo_v001/LOGO_V001_BLOCK_CONNECTIONS_R001_24.csv`
- `logo_v001/LOGO_V001_TEST_CASES_R001_24.csv`

Der Leitfaden basiert auf dem tatsächlich in R001.24 verwendeten `ModbusRegisterMap` und nicht auf alten vereinfachten Vorversionen.

### Aktuelles Partcounter-Modbus-Protokoll V2

PC -> LOGO!: HR1…HR12 / VW0…VW22.  
LOGO! -> PC: HR20…HR37 / VW38…VW72.  
LOGO! ist Modbus-TCP-Server, Partcounter ist Client/Master. Standard: TCP 502, Unit ID 1.

Der Plan umfasst:

- IP-/Modbus-Server-Konfiguration,
- Client-IP-Beschränkung,
- Holding-Register-/VM-Zuordnung,
- 16-/32-Bit-Reihenfolge,
- CommandWord-/StatusWord-Bits,
- lokale Network Inputs/Outputs,
- Network Analog Inputs/Outputs,
- Parameter-VM-Mapping,
- CurrentVECycles/TotalCycles,
- Snapshot vor VE-Reset,
- CompletionReason und LastCompletedCavities,
- Ventilimpuls Q1,
- CompletedVEs/CompletionSequence,
- CommandSequence/AckSequence einschließlich Wrap,
- LOGO-Heartbeat,
- PC-Heartbeat-Diagnose ohne Produktionsstopp,
- Parameter-/ErrorCode-Prüfung,
- Hardwareverdrahtung I1/Q1/Koppelrelais,
- Inbetriebnahmereihenfolge,
- Fehlerdiagnose,
- 25 konkrete Abnahme-/Testfälle.

## Safety

Partcounter und die Siemens LOGO! sind keine Sicherheitssteuerung. Not-Halt, Schutztür, Maschinenfreigaben und andere sicherheitsgerichtete Funktionen bleiben vollständig außerhalb dieser Logik in den vorgesehenen sicheren Maschinenkreisen.

## Noch nicht als Herstellerdatei erzeugt

Die Unterlagen bilden einen realen, reproduzierbaren Funktionsplan/Netzplan. Eine proprietäre LOGO!-Soft-Comfort-Projektdatei (`.lsc`) wird nicht synthetisch erzeugt, weil Blockparameter und Geräteprojekt in LOGO! Soft Comfort selbst angelegt und anschließend auf der realen Hardware simuliert/abgenommen werden sollen. Die R001.24-Blockmatrix ist so aufgebaut, dass dieser Aufbau Schritt für Schritt durchgeführt und abgehakt werden kann.
