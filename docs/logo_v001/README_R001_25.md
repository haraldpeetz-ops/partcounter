# Partcounter LOGO! V001 – aktueller Werkstattstand R001.25

Für **Partcounter R001.25 / Protocol V3** sind ausschließlich die mit `R001_25` bezeichneten Dateien sowie die normativen Top-Level-Dokumente `../MODBUS_REGISTER_MAP.md` und `../LOGO_CONTROL_LOGIC.md` als aktuelle Bau-/Abnahmevorgabe zu verwenden.

## Aktuelle Dateien

- `LOGO_V001_VM_MAP_R001_25.csv` – verbindliche HR/VM-Belegung.
- `LOGO_V001_BLOCK_CONNECTIONS_R001_25.csv` – verbindliche FBD-Netzwerk-/Blockmatrix.
- `LOGO_V001_IO_WIRING_R001_25.csv` – elektrische Referenzverdrahtung M01.
- `LOGO_V001_TEST_CASES_R001_25.csv` – 60 technische Abnahmefälle.
- `LOGO_V001_STATION01_BUILD_SHEET_R001_25.md` – Schritt-für-Schritt-Aufbau in LOGO! Soft Comfort.
- `STATION01_PARTCOUNTER_LOGO_V001_R001_25.ini` – Stations-/Netzwerkreferenz M01.
- `LOGO_V001_R001_25_COMMAND_ACK_FLOW.svg` – grafischer Command/Ack-Ablauf, sofern vorhanden.
- `LOGO_V001_R001_25_FBD_OVERVIEW.svg` – deterministische technische Übersicht, sofern vorhanden.

## Historische Dateien

Dateien mit `R001_8` oder `R001_24` dokumentieren frühere Protocol-V2-Stände und dürfen **nicht** für eine neue R001.25-Station verwendet werden. Sie bleiben ausschließlich zur Revisionsnachverfolgung im Repository.

## Pflicht vor produktiver Nutzung

1. PC-Software muss `ProtocolVersion = 3` erwarten.
2. LOGO! muss HR20/VW38 = 3 melden.
3. HR13/VW24 `HoldAfterVeNumber` und HR38/VW74 Echo müssen funktionieren.
4. HR39/HR40 müssen die aktuell übernommene technische `JobId` spiegeln.
5. Statusbits V41.6/V41.7 müssen Armed/Active korrekt anzeigen.
6. Das reale CountGate muss bei `CompletionHoldActive` noch im selben lokalen Ablauf weitere I1-Flanken blockieren.
7. M01 muss die zutreffenden Fälle aus `LOGO_V001_TEST_CASES_R001_25.csv` bestanden haben.

Eine grüne PC-CI ersetzt die reale LOGO-/Ventil-/Maschinenabnahme nicht.
