# Partcounter – Projekt-Checkpoint R001.8

**Revision:** R001.8  
**Schwerpunkt:** baubarer LOGO!-Engineeringstand für Referenzmaschine 01  
**LOGO!-Programm:** `Partcounter_LOGO_V001`

## Ausgangsbasis

R001.8 baut vollständig auf R001.7 / Modbus ProtocolVersion 2 auf. Die Referenzhardware ist inzwischen festgelegt:

- Siemens LOGO! `6ED1052-2MD08-0BA2` / LOGO! 12/24RCEo
- Versorgung 24 V DC
- I1 = 24-V-DC-Zyklusimpuls
- Q1 = Verpackungswechsler
- Q1 schaltet standardmäßig ein 24-V-Koppel-/Interface-Relais
- Koppelrelais schaltet das kleine 24-V-Festo-Pneumatikventil
- keine Endlagenrückmeldung an Station 01
- I2 bleibt für spätere Stationen optional vorbereitet
- Ventilimpuls 50…5000 ms im 10-ms-Raster
- Standardwert 750 ms

Spulenstrom, Spulenleistung und integrierte Entstörung des Festo-Ventils bleiben offen und werden erst bei der physischen Inbetriebnahme dokumentiert. Diese Werte sind kein Entwicklungsblocker.

## Neu in R001.8

R001.8 erzeugt aus der bisherigen Funktionsspezifikation einen reproduzierbaren LOGO!-Build-Satz.

Neu im Repository:

- `docs/logo_v001/LOGO_V001_STATION01_BUILD_SHEET_R001_8.md`
- `docs/logo_v001/LOGO_V001_BLOCK_CONNECTIONS_R001_8.csv`
- `docs/logo_v001/LOGO_V001_VM_MAP_R001_8.csv`
- `docs/logo_v001/LOGO_V001_TEST_CASES_R001_8.csv`
- `docs/logo_v001/STATION01_PARTCOUNTER_LOGO_V001.ini`

## Build Sheet

Das Build Sheet enthält:

- konkrete Hardwarebelegung der ersten Station
- I1- und Q1-Verdrahtungsprinzip
- Koppelrelais als verbindlichen Referenzstandard
- vollständige PC→LOGO!- und LOGO!→PC-VM-Zuordnung
- CommandWord-Bits
- Betriebsgrenzen
- Zielblockliste B001…B028
- Kernverbindungen der Funktionsnetze
- VE-Abschluss-/Snapshot-Ablauf
- Reset- und Ventilablauf
- optional deaktivierte Endlagenlogik
- StatusWord und ErrorCodes
- definierte Soft-Comfort-Simulationstests
- reale Inbetriebnahmereihenfolge

## Modbus V2 – verbindliche Basis

### PC → LOGO!

HR1–HR12 / VW0–VW22

### LOGO! → PC

HR20–HR37 / VW38–VW72

Wichtige Werte:

```text
ProtocolVersion          = 2
ActiveCavities            = 1…64
TargetCyclesPerVE         = 1…32767
TotalCycles               = bis 999999 je LOGO!-Auftrag
ValvePulseMs              = 50…5000
ValvePulse10Ms            = ValvePulseMs / 10
CommandSequence           = 1…32767
```

## Station-01-Testwerte

### Basistest

```text
Kavitäten:         4
VE-Soll:           20 Teile
Zielzyklen:        5
Ventilimpuls:      750 ms
HR7:               75
```

Erwartung: Nach fünf positiven I1-Flanken genau ein VE-Abschluss und genau ein 750-ms-Q1-Impuls.

### Rundungstest

```text
Kavitäten:         64
VE-Soll:           1000
Zielzyklen:        16
Effektive VE:      1024 Teile
Mehrmenge:         24 Teile
```

## Native LOGO!-Projektdatei

Eine native `.lsc`-Datei wird nicht künstlich erzeugt. Das LOGO!-Soft-Comfort-Projektformat ist proprietär; eine nicht in LOGO! Soft Comfort selbst erzeugte Datei wäre nicht zuverlässig validierbar.

Der R001.8-Build-Satz ist deshalb bewusst so aufgebaut, dass das Projekt in LOGO! Soft Comfort reproduzierbar erstellt und anschließend als echte Siemens-Projektdatei gespeichert wird.

## Nächster Schritt

1. `Partcounter_LOGO_V001` anhand des R001.8-Build-Sheets in LOGO! Soft Comfort anlegen.
2. Blocknummern nach realer LSC-Erstellung zurückdokumentieren.
3. Tests T01–T24 ausführen.
4. LOGO! zunächst ohne angeschlossenes Ventil auf 24 V testen.
5. Q1 mit Koppelrelais messen.
6. Ventil anschließend über Koppelrelais anschließen.
7. reale I1-Maschinenflanke prüfen.
8. Partcounter über Modbus TCP ankoppeln.
9. Abnahmeprotokoll vollständig durchführen.

## Versionsstand PC

Die Assembly-Version wurde auf `0.1.8` / R001.8 angehoben.

## Safety

Partcounter und die Siemens LOGO! sind keine Sicherheitssteuerung. Maschinen-Safety, Not-Halt, Schutztüren und andere sicherheitsgerichtete Funktionen verbleiben vollständig in den vorhandenen sicheren Maschinenkreisen.
