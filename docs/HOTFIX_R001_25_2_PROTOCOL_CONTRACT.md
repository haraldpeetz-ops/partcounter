# Partcounter R001.25 Hotfix 2 – Protocol-V3-Vertrag und Verbindungsdiagnose

## Anlass

Der Integrationsaudit PC ↔ Siemens LOGO! vom 03.09.2026 hat zwei konkrete PC-seitige Vertragsabweichungen sowie eine Diagnoseverbesserung identifiziert:

1. Ein vollständiges Auftragstelegramm schrieb HR12 / VW22 `PcHeartbeat` mit `0`, obwohl Protocol V3 für den aktiven Kommunikationsbetrieb `1..32767` vorsieht.
2. `LogoModbusClient` akzeptierte `HoldAfterVeNumber = 0`, obwohl ein produktiver V3-Auftrag zwingend eine lokale VE-Grenze `1..32767` benötigt.
3. Ein nicht erreichbarer TCP-Endpunkt konnte dem Bediener nur eine generische Socket-/Offline-Meldung liefern.

## Korrekturen HF2

- Vollständige Job-Telegramme überschreiben den PC-Heartbeat nicht mehr mit 0. Vor dem ersten Heartbeat wird sicher der Wert 1 verwendet; danach bleibt der zuletzt gesendete gültige Heartbeat erhalten.
- Der Payload-Builder validiert `PcHeartbeat` strikt auf `1..32767`.
- Produktive Job-Telegramme mit `HoldAfterVeNumber = 0` werden PC-seitig vor jedem Modbus-Schreiben abgelehnt.
- `CommandSequence` wird bereits beim Aufbau des vollständigen V3-Payloads auf `1..32767` geprüft.
- TCP-Verbindungsaufbau besitzt einen begrenzten 2,5-s-Timeout mit eindeutiger Meldung aus Maschinenname, IP und Port.
- Protocol-Mismatch nennt jetzt Soll-/Ist-Version sowie HR20/VW38 und den betroffenen Endpunkt.
- Neue Regressionstests prüfen gültigen HR1..HR13-Payload, nichtnull Heartbeat sowie die Ablehnung von Heartbeat=0 und HoldAfterVE=0.

## Versionsstand

- Produkt: Partcounter
- Revision: R001.25 HF2
- Product Version: 0.1.25
- FileVersion: 0.1.25.2
- InformationalVersion: `0.1.25-r001.25-hf2-protocol-contract`
- LOGO!-Schnittstelle: Modbus TCP Protocol V3

## LOGO!-Seite

Am validierten FBD-/VM-Graphen der LOGO!-HF3.4 ist für diese Korrekturen keine Funktionsänderung erforderlich. Die V3-Registermatrix bleibt unverändert:

- PC → LOGO!: HR1..HR13 / VW0..VW24
- LOGO! → PC: HR20..HR40 / VW38..VW78
- Standard: TCP 502, Unit ID 1

Die reale Station muss weiterhin in LOGO! Soft Comfort bzw. im Gerät mit aktivem Modbus-Zugriff und Modbus-TCP-Server projektiert sein. Für M01 gilt als Referenz `192.168.50.101/24`, Port 502; für die Inbetriebnahme kann die Client-Freigabe zunächst auf alle Verbindungsanfragen gesetzt werden.

## Freigabegrenze

HF2 beseitigt die erkannten PC-seitigen Protocol-V3-Vertragsfehler. Die Production-Baseline bleibt trotzdem an die reale M01-Abnahme gekoppelt, insbesondere T01 Protocol-V3-Handshake, Q1-Puls, Command/Ack, Completion-Hold, Reconnect und Power-Cycle/Retentivität.
