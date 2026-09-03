# Partcounter R001.25 HF6 – Modbus Transport Hardening

## Anlass

Die R001.25-HF5-Prüfung zeigte unter wiederholter Last einen sporadischen NModbus-Fehler: Eine verspätete Antwort eines vorherigen Retry-Versuchs konnte vom folgenden Telegramm gelesen werden. NModbus meldete dann eine abweichende Transaction-ID. Zusätzlich konnte ein fehlgeschlagener Fleet-Poll die Verbindung nach Freigabe des Session-Locks schließen, obwohl ein paralleler Befehl sie inzwischen neu aufgebaut hatte.

## Korrekturen

- `LogoModbusClient` serialisiert Connect, Disconnect, FC03, FC06 und FC16 über ein gemeinsames Transport-Gate.
- Der NModbus-Transport akzeptiert ein begrenztes Fenster alter Transaction-IDs und liest bis zur passenden Antwort weiter.
- `MachineFleetService` invalidiert einen fehlgeschlagenen Poll-Transport noch innerhalb des stationsbezogenen Session-Gates.
- Asynchrone Transportaufrufe verwenden `ConfigureAwait(false)`, damit synchrone Abbaupfade nicht vom UI-Synchronisationskontext abhängen.
- Der automatisierte Stressmodus lädt die reine Live-Inbetriebnahmeansicht nicht und zeigt keine modalen Warnungen; der Layoutmodus prüft sie weiterhin visuell.
- Wiederholte WPF-Containerereignisse werden beim Anhängen der Maschinen-Kontextmenüs zusammengefasst; ein bereits korrekt zugeordnetes Menü wird wiederverwendet.

## Regressionstests

Die Integrationstests prüfen zusätzlich:

- parallele FC03-/FC06-Operationen auf einem gemeinsamen Client,
- eine gezielt um 1.750 ms verzögerte Antwort nach dem 1.500-ms-Empfangstimeout,
- FC16-Befehlswrite mit AckSequence-Rücklesung,
- Connect → Jobwrite → Statusread → Command → Reconnect,
- eindeutige Diagnose bei Protocol-V3-Mismatch.

## Schnittstellenstand

- Revision: R001.25 HF6
- Product Version: 0.1.25
- FileVersion: 0.1.25.6
- InformationalVersion: `0.1.25-r001.25-hf6-modbus-transport-hardening`
- Modbus TCP: Protocol V3, unveränderte Register HR1..HR13 und HR20..HR40
- LOGO!-Programm: `PARTCOUNTER_LOGO_V001_R001_25_HF3_4_TRANSFERREADY.lsc` unverändert

## Freigabegrenze

Die Software- und Loopback-Prüfung ersetzt nicht die M01-Abnahme mit realer LOGO!, realem LAN/WLAN-Pfad, I1-Zyklussignal und Q1-VE-Wechsler. Bis diese Prüfung protokolliert ist, bleibt das Gesamtpaket ein Release Candidate.
