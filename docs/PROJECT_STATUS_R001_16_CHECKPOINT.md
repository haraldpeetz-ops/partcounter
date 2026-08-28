# Partcounter – Projektstatus R001.16

Stand: 28.08.2026

Branch: `r001.16-live-commissioning`

Basis: `r001.15-production-readiness`

## Implementiert

- neue read-only Live-Abnahmemessung im bereits admin-geschützten Bereich `Inbetriebnahme / Diagnose`
- Standardauswahl Referenzmaschine M01
- Echtbetriebs-Preflight ohne automatische Betriebsmodus-Umschaltung
- 750-ms-Mitschnitt der bestehenden Fleet-/Modbus-V2-Diagnose
- Heartbeat-, Command/Ack-, Status-, Fehler-, Zähler- und VE-Evidenz
- Erkennung von Kommunikationsabbruch und Wiederkehr
- automatische Zusammenfassung der Messserie
- CSV-Export unter `Dokumente\Partcounter\Inbetriebnahme`
- optionale Übernahme der Messdaten in bestehende Prüfnotizen
- keine automatische Änderung von BESTANDEN/NICHT BESTANDEN
- kein zweiter PLC-Steuerpfad, keine direkte Q1-Ansteuerung aus dem Messmodul
- Version 0.1.16 / R001.16
- R001.16 Build-, Portable-, SingleFile-, Engineering- und Update-Paketdefinition

## Unverändert

- Modbus Protocol V2
- `Partcounter_LOGO_V001`
- bestehende Auftrags-/VE-Steuerlogik
- R001.15 Backup-/Produktionsbereitschaftsfunktionen
- R001.14 Update-/Hilfe-/Über-Funktionen
- R001.13 Firmenlogo
- R001.12 Operator/Admin-Trennung
- R001.11 Bild-/Logo-Unterstützung im Etiketteneditor

## Nächster Validierungsschritt

1. Windows-CI muss Restore, Release-Build und beide Publish-Varianten erfolgreich abschließen.
2. Danach reale Inbetriebnahme an M01 mit LOGO!-V001 und Modbus V2.
3. Elektrische Q1-/Ventilimpulsdauer und mechanischer Kistenwechsel vor Ort messen.
4. WLAN-/PC-Ausfalltest durchführen und lokale LOGO!-Weiterzählung bestätigen.
