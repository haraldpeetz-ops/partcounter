# Partcounter – Projekt-Checkpoint R001.7

Verbindlicher Entwicklungsstand für die Fortsetzung nach R001.6.

## Revisionsstand

- Partcounter R001.7
- Entwicklungszweig: `r001.7-logo-v001`
- Pull Request: #1
- Ziel: erste reale Siemens-LOGO!-Kopplung auf Basis des standardisierten `Partcounter_LOGO_V001`
- Protokoll: Modbus V2 / ProtocolVersion 2

## Referenzmaschine 01 – Hardware festgelegt

Verbindliche Hardwarebasis der ersten realen Teststation:

- Siemens LOGO! `6ED1052-2MD08-0BA2`
- Bezeichnung: LOGO! 12/24RCEo, LOGO! 8.4, ohne Display
- Versorgung: 24 V DC
- Zyklussignal: I1, 24 V DC, positive Flanke = ein Produktionszyklus
- VE-Wechsler: Q1, LOGO!-Relaisausgang, Ventilspule 24 V DC
- Endlagenrückmeldung: an Station 01 nicht vorhanden; I2 bleibt optional vorbereitet
- Endlagenüberwachung für Station 01: OFF
- Ventilimpuls einstellbar: 50…5000 ms in 10-ms-Schritten
- Standardimpuls: 750 ms
- HR7 überträgt die Ventilzeit in 10-ms-Einheiten; 750 ms = 75

Das technische Datenblatt der verwendeten LOGO!-Variante nennt vier Relaisausgänge, keine interne Kurzschlussabsicherung, maximal 10 A ohmsche und 3 A induktive Kontaktlast. Für Q1 ist deshalb eine externe Absicherung verbindlich. Für die industrielle Standardausführung wird ein 24-V-Koppel-/Interface-Relais zwischen LOGO! und Ventil bevorzugt; eine direkte Ventilansteuerung wird erst nach Prüfung der realen Spulenstromaufnahme freigegeben.

Die Hardwaredetails stehen in `docs/REFERENCE_MACHINE_01_HARDWARE.md`.

## Wesentliche Änderungen gegenüber R001.6

### LOGO!-hardwaregerechtes Modbus-Protokoll V2

R001.7 ersetzt das bisherige V1-Statusmodell durch eine Zyklusübertragung, die auf den realen LOGO!-Zähler- und VM-Möglichkeiten basiert:

- LOGO! zählt `CurrentVECycles` und `TotalCycles` nativ.
- `CurrentVECycles` und `TotalCycles` werden als DWord-Werte über VM/Modbus bereitgestellt.
- Partcounter berechnet die Teilezahl auf dem PC aus `Zyklen × Kavitäten`.
- `LastCompletedVECycles`, `LastCompletedCavities` und `LastCompletionReason` werden beim Abschluss gespeichert und bleiben bis zur nächsten VE stabil.
- Statusblock erweitert auf HR20–HR37.
- `TargetCyclesPerVE` liegt auf VD18 / HR10-HR11 und wird dem Schwellwert des VE-Zählers zugeordnet.

### Verbindliche Betriebsgrenzen V2

- Kavitäten: 1–64
- TargetCyclesPerVE / CurrentVECycles: 1–32767 je VE
- TotalCycles: bis 999999 je LOGO!-Auftrag
- CompletedVEs: bis 32767 je LOGO!-Auftrag
- CommandSequence / CompletionSequence / Heartbeats: 1–32767, danach Wrap auf 1
- Ventilimpuls: 50–5000 ms in 10-ms-Schritten
- HR7 überträgt den Ventilwert in 10-ms-Einheiten; 750 ms entsprechen Registerwert 75

Die VE-Grenze von 32767 wurde bewusst gewählt, damit der abgeschlossene VE-Zyklusstand innerhalb der LOGO! ohne riskante 32-Bit-Analogarithmetik sicher gepuffert werden kann. Der Gesamtzykluszähler bleibt ein nativer DWord-Zähler bis 999999.

### Restart-sicherer Command-Handshake

Vor dem ersten Steuerbefehl nach Verbindung oder PC-Neustart liest Partcounter den von der LOGO! gemeldeten `AckSequence`-Wert. Die lokale CommandSequence wird auf diesen Wert synchronisiert und erst danach erhöht.

Damit wird verhindert, dass der erste Befehl nach einem PC-Neustart zufällig dieselbe Sequenznummer wie der letzte bereits bearbeitete LOGO!-Befehl verwendet und deshalb als Duplikat verworfen wird.

CommandSequence und PC-Heartbeat bleiben im positiven LOGO!-16-Bit-Bereich und springen nach 32767 kontrolliert auf 1 zurück.

### PC-seitige Parameterprüfung

Vor dem Modbus-Schreiben werden mindestens geprüft:

- Kavitäten 1–64
- TargetPartsPerVE > 0
- TargetCyclesPerVE 1–32767
- ValvePulseMs 50–5000 ms
- ValvePulseMs muss durch 10 teilbar sein

Ungültige Auftragsparameter werden nicht an die LOGO! übertragen.

## LOGO!-V001 Engineering-Standard

Verbindliche Unterlagen:

- `docs/PARTCOUNTER_LOGO_V001_IMPLEMENTATION.md`
- `docs/LOGO_CONTROL_LOGIC.md`
- `docs/MODBUS_REGISTER_MAP.md`
- `docs/COMMISSIONING_TEST_PROTOCOL_R001_7.md`
- `docs/REFERENCE_MACHINE_01_HARDWARE.md`

Der Implementierungsstandard ist bis auf Block-/VM-Ebene ausgearbeitet. Enthalten sind unter anderem:

- Standard-I/O I1/I2/I3 und Q1/Q2/Q3
- VM-/Holding-Register-Zuordnung
- reale Zyklus-Flankenerkennung vor der Zählfreigabe
- nativer VE-Zähler und Gesamtzykluszähler
- Sample-and-Hold über Analog-Watchdog für `LastCompletedVECycles`
- gespeicherter Abschlussgrund automatisch/manuell
- gespeicherte Kavitätenzahl der letzten VE
- verzögerter VE-Zählerreset nach Snapshot
- restart-sicherer CommandSequence/AckSequence-Decoder
- Heartbeat-Überwachung
- Ventilzeit mit fester 10-ms-Zeitbasis
- optionale Endlagenüberwachung: Station 01 OFF, für spätere Maschinen aktivierbar
- Fehlercodes und optionaler Endlagentimeout

## Verbindliche Registerbasis V2

### PC → LOGO!

- HR1–HR12 / VW0–VW22
- ProtocolVersion, CommandSequence, CommandWord, Kavitäten, VE-Soll, Ventilzeit, Job-ID, Zielzyklen, PC-Heartbeat

### LOGO! → PC

- HR20–HR37 / VW38–VW72
- ProtocolVersion, StatusWord, CurrentVECycles, TotalCycles, VE-Zähler, LastCompletedVECycles, AckSequence, Kavitätenecho, LastCompletedVENumber, CompletionSequence, LOGO!-Heartbeat, ErrorCode, CompletionReason, LastCompletedCavities

DWord-Werte werden High Word vor Low Word übertragen.

## Inbetriebnahme-/Abnahmestandard

`COMMISSIONING_TEST_PROTOCOL_R001_7.md` enthält aktuell **75 Prüfpunkte** und ist auf Referenzmaschine 01 spezialisiert. Abgedeckt werden unter anderem:

- exakte LOGO!-Identifikation und 24-V-Versorgung
- Q1-Absicherung, Ventilspulendaten und Entstörung
- Modbus-/VM-Zuordnung und DWord-Reihenfolge
- TargetCycles-Mapping auf VD18
- CommandSequence-/Heartbeat-Wrap 32767 → 1
- 24-V-Zyklusflanke und Pause bei anstehendem I1
- 1/2/4/8/16/32/64 Kavitäten
- Rundung auf vollständige Werkzeugzyklen
- VE-Grenzwerte 32767 / 32768
- TotalCycles über 32767
- stabiler Snapshot abgeschlossener VE-Daten
- Ventilzeit 50/250/750/2500/5000 ms und 10-ms-Raster
- Endlagenüberwachung Station 01 deaktiviert und optionale I2-Funktion vorbereitet
- manueller und automatischer Wechsel
- dynamische letzte Teil-VE
- PC-/LAN-/WLAN-Ausfall und Wiederverbindung
- LOGO!-Power-Cycle und Retentivität
- Etikettierung und VE-Historie
- Langzeit-/Mehrfach-VE-Test

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

Die bisher offenen Grunddaten der Testmaschine sind jetzt weitgehend festgelegt. Für die endgültige Q1-Verdrahtungsfreigabe fehlt nur noch:

1. Nennstrom oder Nennleistung der 24-V-Ventilspule,
2. vorhandene integrierte Freilauf-/Entstörbeschaltung ja/nein.

Anschließend kann `Partcounter_LOGO_V001` konkret für die LOGO! `6ED1052-2MD08-0BA2` in LOGO! Soft Comfort aufgebaut und anhand des 75-Punkte-R001.7-Inbetriebnahmeprotokolls geprüft werden.

## Sicherheitsgrenze

Partcounter und die Siemens LOGO! sind keine Sicherheitssteuerung. Maschinen-Safety, Not-Halt, Schutztürüberwachung und weitere sicherheitsgerichtete Funktionen bleiben vollständig in den dafür vorgesehenen sicheren Steuerungskreisen.
