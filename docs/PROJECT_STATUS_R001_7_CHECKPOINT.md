# Partcounter – Projekt-Checkpoint R001.7

Verbindlicher Entwicklungsstand für die Fortsetzung nach R001.6.

## Revisionsstand

- Partcounter R001.7
- Entwicklungszweig: `r001.7-logo-v001`
- Pull Request: #1
- Ziel: erste reale Siemens-LOGO!-Kopplung auf Basis des standardisierten `Partcounter_LOGO_V001`

## Wesentliche Änderungen gegenüber R001.6

### Modbus-Protokoll V2

R001.7 ersetzt das bisherige V1-Statusmodell durch eine LOGO!-hardwaregerechte Zyklusübertragung:

- LOGO! zählt `CurrentVECycles` und `TotalCycles` nativ.
- Zykluszähler werden als DWORD übertragen.
- Partcounter berechnet die Teilezahl auf dem PC aus `Zyklen × Kavitäten`.
- `LastCompletedVECycles` und `LastCompletedCavities` ermöglichen eine eindeutige Rekonstruktion der zuletzt abgeschlossenen VE.
- Statusblock erweitert auf HR20–HR37.
- ProtocolVersion = 2.

### Restart-sicherer Command-Handshake

Vor dem ersten Steuerbefehl nach Verbindung oder PC-Neustart liest Partcounter den von der LOGO! gemeldeten `AckSequence`-Wert. Die lokale CommandSequence wird auf diesen Wert synchronisiert und erst danach erhöht.

Damit wird verhindert, dass der erste Befehl nach einem PC-Neustart zufällig dieselbe Sequenznummer wie der letzte bereits von der LOGO! bearbeitete Befehl verwendet und deshalb als Duplikat verworfen wird.

### PC-seitige Parameterprüfung

Vor dem Modbus-Schreibvorgang werden mindestens geprüft:

- Kavitäten: 1–64
- TargetPartsPerVE > 0
- TargetCyclesPerVE: 1–999999
- ValvePulseMs: 50–5000 ms

Ungültige Auftragsparameter werden nicht an die LOGO! übertragen.

## LOGO!-V001 Engineering-Standard

Neu vorhanden:

- `docs/PARTCOUNTER_LOGO_V001_IMPLEMENTATION.md`
- `docs/LOGO_CONTROL_LOGIC.md`
- `docs/MODBUS_REGISTER_MAP.md`
- `docs/COMMISSIONING_TEST_PROTOCOL_R001_7.md`

Festgelegt sind:

- Standard-I/O I1/I2/I3 und Q1/Q2/Q3
- VM-/Holding-Register-Zuordnung
- interne Merker M1–M16
- Funktionsgruppen für Zyklus, Zähler, Befehlsdecoder, VE-Abschluss, Ventil und Heartbeat
- Zustandsmodell
- ErrorCode-Schema
- Restart-Verhalten
- 48 Prüfschritte für Inbetriebnahme und Abnahme

## Verbindliche Registerbasis V2

### PC → LOGO!

- HR1–HR12 / VW0–VW22
- ProtocolVersion, CommandSequence, CommandWord, Kavitäten, VE-Soll, Ventilzeit, Job-ID, Zielzyklen, PC-Heartbeat

### LOGO! → PC

- HR20–HR37 / VW38–VW72
- ProtocolVersion, StatusWord, CurrentVECycles, TotalCycles, VE-Zähler, LastCompletedVECycles, AckSequence, Kavitätenecho, CompletionSequence, LOGO!-Heartbeat, ErrorCode, CompletionReason, LastCompletedCavities

32-Bit-Werte werden High Word vor Low Word übertragen.

## Weiterhin enthaltener R001.6-Funktionsumfang

- Leitstand für bis zu 30 Spritzgussmaschinen
- Simulations- und Echtbetrieb
- Artikelstamm, Werkzeug, Kavitäten und VE-Mengen
- Auftragsmenge, Fortschritt und dynamische letzte VE
- Pause/Fortsetzen/Beenden
- Maschinenfilter und temporäre Deaktivierung
- Füllgrad-Ampel und Mini-Monitor
- VE-Historie
- Etikettendruck, QR-Code und Code 128
- SQLite-Datenhaltung
- ARBURG-ALS-Datei-/Hotfolder- und REST/JSON-Anbindung
- konfigurierbares ALS-Feld- und Maschinenalias-Mapping

## Nächster realer Arbeitsschritt

Für die konkrete Testmaschine müssen folgende elektrische Daten festgelegt bzw. bestätigt werden:

1. exakter LOGO!-Typ und Versorgungsspannung,
2. Signalart und Pegel des Zyklusimpulses an I1,
3. Ventilspulenspannung und benötigte Ausgangs-/Koppelrelais-Lösung,
4. vorhandene Endlagenrückmeldung an I2 ja/nein,
5. freizugebende Ventilimpulszeit.

Danach wird `Partcounter_LOGO_V001` in LOGO! Soft Comfort für die erste Station umgesetzt und anhand des R001.7-Inbetriebnahmeprotokolls geprüft.

## Sicherheitsgrenze

Partcounter und die Siemens LOGO! sind keine Sicherheitssteuerung. Maschinen-Safety, Not-Halt, Schutztürüberwachung und weitere sicherheitsgerichtete Funktionen bleiben vollständig in den dafür vorgesehenen sicheren Steuerungskreisen.
