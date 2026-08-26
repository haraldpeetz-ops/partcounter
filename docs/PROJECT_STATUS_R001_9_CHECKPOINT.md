# Partcounter – Projekt-Checkpoint R001.9

**Revision:** R001.9  
**Arbeitstitel:** Commissioning & Diagnostics  
**Branch:** `r001.9-commissioning-diagnostics`  
**Pull Request:** #3

## Ausgangsbasis

R001.9 basiert vollständig auf R001.8 mit:

- Modbus ProtocolVersion 2
- standardisiertem `Partcounter_LOGO_V001`
- Referenzmaschine 01: Siemens `6ED1052-2MD08-0BA2`
- I1 = 24-V-Zyklusimpuls
- Q1 = 24-V-Koppel-/Interface-Relais für Festo-Ventil
- I2 optional, Station 01 ohne Endlagenrückmeldung
- Ventilimpuls 50…5000 ms im 10-ms-Raster
- lokaler LOGO!-Zählung und lokalem VE-Wechsel

## Neu in R001.9

### Inbetriebnahme- und Diagnosezentrum

Neuer Haupttab `Inbetriebnahme / Diagnose` mit drei Bereichen:

1. Live-Diagnose
2. Hardware / Freigabe
3. Prüfablauf / Abnahme

### Live-Kommunikationsdiagnose

Der vorhandene `MachineFleetService` veröffentlicht einen zentralen read-only Diagnose-Snapshot. Dadurch verwendet das Diagnosezentrum dieselben realen Kommunikationssessions wie der Produktionsleitstand.

Angezeigt werden:

- ConnectionState
- letzter gültiger Snapshot-Zeitpunkt
- PC Heartbeat
- LOGO Heartbeat
- lokale CommandSequence
- LOGO AckSequence
- Sequenz-Synchronisationsstatus
- StatusWord
- dekodierte Statusbits
- ErrorCode mit Klartext
- CurrentParts
- TotalCycles
- CurrentVENumber
- CompletedVEs
- ActiveCavitiesEcho

### Direkte read-only Modbus-Leseprobe

Für eine ausgewählte Maschine kann eine separate Status-Leseprobe durchgeführt werden.

Eigenschaften:

- Timeout 4 Sekunden
- liest nur den Statusblock
- keine Parameterwrites
- keine CommandSequence-Änderung
- kein Q1-Schalten
- kein manueller VE-Wechsel

Damit kann Netzwerk/ProtocolVersion geprüft werden, bevor der reguläre Echtbetrieb gestartet wird.

### Hardware- und Freigabeprofile

Neue SQLite-Persistenz:

- Tabelle `CommissioningProfiles`
- Tabelle `CommissioningChecks`

Gespeichert werden unter anderem:

- LOGO!-Bestellnummer und Typ
- Versorgung
- I1-Signaldefinition
- Q1/Ventildefinition
- Interface-Relais ja/nein
- Endlagenüberwachung ja/nein
- Standard-Ventilimpuls
- Freigabestatus
- Notizen

Freigabestatus:

- NotTested
- InTest
- ReleasedWithConditions
- Released
- Blocked

### Geführte Kernabnahme

16 persistente Prüfpositionen:

- Hardwareidentifikation
- Netzwerk
- ProtocolVersion
- Heartbeats
- Command/Ack
- I1 elektrisch
- I1 Zählung
- Pause/Fortsetzen bei I1 HIGH
- 64-fach-Rundung
- Q1-Koppelrelais
- Q1-Pulszeiten
- automatischer VE-Wechsel
- manueller VE-Wechsel
- PC/WLAN-Ausfall
- Power-Cycle
- Etikett/VE-Historie

Ergebnisse:

- offen
- bestanden
- nicht bestanden
- nicht anwendbar

Notizen und Prüfdaten werden je Maschine gespeichert.

### Protokollexport

Maschinenbezogenes CSV-Protokoll nach:

```text
Dokumente\Partcounter\Inbetriebnahme
```

Enthält:

- Maschinenidentifikation
- Hardwareprofil
- Freigabestatus
- Live-Diagnosedaten
- vollständige Prüfliste

### 30-Maschinen-Rolloutübersicht

Zusätzlicher Tab `Rolloutstatus 30 Maschinen`.

Pro Station werden angezeigt:

- Name
- IP/Port/Unit-ID
- Live-Verbindung
- letzte Antwort
- Freigabestatus
- Prüffortschritt
- Hardwareprofil
- ErrorCode

Gesamtübersicht ist als CSV exportierbar.

### UI-Revisionsbereinigung

Das Hauptfenster überschreibt ältere statische Statusbindungen und zeigt im Kopfbereich verbindlich:

- `R001.9 · SIMULATION`
- `R001.9 · ECHTBETRIEB MODBUS TCP`

## Build

R001.9 verwendet weiterhin .NET 8 / WPF und erzeugt über GitHub Actions:

- `Partcounter_R001_9_Portable_Folder_win-x64`
- `Partcounter_R001_9_SingleFile_win-x64`
- `Partcounter_R001_9_Commissioning_Engineering`

Das LOGO!-Engineeringpaket aus R001.8 wird weiterhin in die Windows-Ausgaben integriert.

## Nächster realer Meilenstein

Die Softwareseite für die Pilot-Inbetriebnahme ist mit R001.9 vorbereitet.

Nächste praktische Schritte:

1. `Partcounter_LOGO_V001` in LOGO! Soft Comfort für Station 01 aufbauen.
2. Offline-Simulation durchführen.
3. LOGO! mit 24 V versorgen.
4. I1 prüfen.
5. Q1 zunächst am Koppelrelais messen.
6. Modbus TCP koppeln.
7. R001.9-Diagnosezentrum verwenden.
8. 16 Kernprüfungen und erweitertes 75-Punkte-Hardwareprotokoll durchführen.
9. Station 01 als Golden Master freigeben.
10. danach Rollout auf M02–M30.

## Sicherheitsgrenze

Partcounter und LOGO! sind keine Sicherheitssteuerung. Maschinen-Safety verbleibt vollständig außerhalb von Partcounter.
