# Partcounter – Session Checkpoint 2026-08-26

**Gesicherter Entwicklungsstand:** R001.10  
**Aktiver Branch:** `r001.10-label-editor`  
**Aktiver Pull Request:** #4 – `R001.10: WYSIWYG Label Designer`  
**Status:** finaler Windows-CI-Build erfolgreich

## 1. Hardware – Referenzstation 01 eingefroren

- Siemens LOGO! `6ED1052-2MD08-0BA2` / LOGO! 12/24RCEo
- Versorgung 24 V DC
- I1 = 24-V-DC-Zyklusimpuls
- positive Flanke = genau ein Produktionszyklus
- Q1 = 24-V-Koppel-/Interface-Relais für kleines Festo-Pneumatikventil
- Ventilspule 24 V DC
- Ventilimpuls 50…5000 ms, 10-ms-Raster, Standard 750 ms
- I2-Endlagenrückmeldung an Station 01 nicht vorhanden
- Endlagenüberwachung OFF, I2 für spätere Stationen vorbereitet
- Spulenstrom/Leistung/Entstörung bleiben als Inbetriebnahmeprüfpunkte offen und blockieren die Software nicht
- Partcounter/LOGO! sind keine Sicherheitssteuerung

## 2. LOGO! / Modbus – eingefrorener V2-Stand

- PC = Modbus TCP Client
- LOGO! = Modbus TCP Server
- TCP Port 502
- ProtocolVersion 2
- PC→LOGO HR1–HR12
- LOGO→PC HR20–HR37
- ActiveCavities 1…64
- TargetCyclesPerVE 1…32767
- TotalCycles bis 999999 je LOGO!-Auftrag
- Sequenzen/Heartbeats 1…32767 mit Wrap auf 1
- Ventilzeit HR7 in 10-ms-Einheiten
- restart-sichere CommandSequence/AckSequence-Synchronisation
- LOGO V001 Engineering-/Build-Satz vorhanden
- native `.lsc` wird morgen in LOGO! Soft Comfort aufgebaut und real getestet

## 3. R001.9 – Inbetriebnahme & Diagnose enthalten

- eigener Reiter `Inbetriebnahme / Diagnose`
- Live-Verbindungsstatus und letzte Antwort
- PC-Heartbeat / LOGO-Heartbeat
- CommandSequence / AckSequence
- StatusWord mit Klartextbits
- ErrorCode-Klartext
- aktuelle VE, TotalCycles, CompletedVEs, ActiveCavitiesEcho
- read-only Modbus-Testprobe
- Hardware-/Freigabeprofil je Maschine
- 16 geführte Kernprüfungen mit SQLite-Persistenz
- CSV-Abnahmeprotokoll
- Rolloutübersicht für alle 30 Maschinen

## 4. R001.10 – WYSIWYG Etiketteneditor

- eigener Reiter `Etiketteneditor`
- Drag-&-Drop / Mausverschiebung auf WYSIWYG-Canvas
- X/Y/Breite/Höhe millimetergenau
- freie Etikettengröße 20…500 mm
- Presets A5 quer, A6 quer, 100×50, 100×100, 150×100
- statischer Text
- dynamischer Text
- QR-Code
- Code128
- Rechtecke / Rahmen
- Linien
- Schriftart, Schriftgröße, fett, kursiv, unterstrichen, Ausrichtung
- Vorlagenverwaltung in SQLite (`LabelTemplates`)
- genau eine globale Standardvorlage
- genau eine aktive Artikelvorlage je Artikelnummer
- Auflösung beim Produktionsdruck: Artikelvorlage → Standardvorlage → interner Fallback
- bisheriges festes Layout wird automatisch als `Partcounter Standard` erzeugt
- Editorvorschau und Produktionsdruck verwenden dieselbe Renderengine
- Testdruck direkt aus der aktuellen Editor-Arbeitskopie

## 5. R001.10 CI / Artefakte

Finaler R001.10 GitHub-Actions-Lauf: erfolgreich.

Erfolgreich:
- Restore
- Release Build
- Portable win-x64 Publish
- Single-File win-x64 Publish
- Engineering-/Dokumentationspaket
- alle Artifact Uploads

Artefakte:
- `Partcounter_R001_10_Portable_Folder_win-x64`
- `Partcounter_R001_10_SingleFile_win-x64`
- `Partcounter_R001_10_Engineering`

## 6. Exakter Wiedereinstieg morgen

### Hardware-Pilot Maschine 01
1. `Partcounter_LOGO_V001` in LOGO! Soft Comfort anhand des Engineeringpakets aufbauen.
2. Offline-Simulation der Kernlogik.
3. LOGO! 24 V versorgen.
4. I1-Zyklusflanke prüfen.
5. Zähler / VE-Abschluss prüfen.
6. Modbus TCP koppeln.
7. Heartbeats / AckSequence / StatusWord prüfen.
8. Q1 zunächst nur am Koppelrelais messen.
9. Ventilimpulse 50 / 750 / 5000 ms prüfen.
10. Danach Festo-Ventil anschließen und realen VE-Wechsel testen.
11. Abnahme im R001.10-Inbetriebnahmezentrum dokumentieren.

### Software
- R001.10 bleibt der gesicherte Ausgangspunkt.
- Hardwarelogik vor dem Pilotversuch nicht mehr verändern, außer ein realer Testbefund erfordert es.
- Etiketteneditor kann parallel getestet und später um Bilder/Logos, DataMatrix/GS1, Vorlagenimport/-export und Vorlagenrevisionen erweitert werden.

## 7. Freigabestatus

- PC-Software: Build-grün, bereit für Pilotversuch
- LOGO-Engineering: vollständig spezifiziert, native Soft-Comfort-Datei noch real aufzubauen
- Hardware Station 01: Konzept eingefroren, reale Inbetriebnahme ausstehend
- 30-Maschinen-Rollout: erst nach erfolgreichem Pilot / Golden Master

Dieser Checkpoint ist der verbindliche Wiedereinstiegspunkt für die nächste Partcounter-Sitzung.
