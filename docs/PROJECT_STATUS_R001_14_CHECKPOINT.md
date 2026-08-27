# Partcounter – Projektstatus R001.14 Checkpoint

**Stand:** 2026-08-27 22:08 Europe/Berlin  
**Branch:** `r001.14-about-update-help`  
**Basis:** R001.13 company branding  
**Status:** Windows CI vollständig grün nach Hotfixes

## R001.14 – neue Funktionen

### Frei zugängliche Über-Funktion
- ohne Admin-Anmeldung erreichbar
- zeigt Programmrevision und Assembly-Version
- Programmierer: Harald Peetz
- Technologiehinweise: C#, .NET 8, WPF, SQLite, NModbus
- Modbus Protocol V2 / Partcounter_LOGO_V001
- Windows-/Runtime-/Architektur-/Programmpfad-/Datenbankinformationen
- kleiner proprietärer Lizenzhinweis
- Systeminfo kann kopiert werden

### Integrierte Hilfe
- frei zugänglich
- Aufruf per Schaltfläche und F1
- sehr detaillierte Themenstruktur
- pro Thema: Beschreibung, Voraussetzungen, Abhängigkeiten, Folgewirkungen, Suchbegriffe
- direkte Navigation zwischen zusammenhängenden Funktionen
- beschreibt u. a. die komplette Kette:
  Artikelstamm -> Auftrag -> Zielzyklen -> Modbus -> LOGO! -> I1-Zählung -> VE-Abschluss -> Q1 -> CompletionSequence -> VE-Historie -> Etikettenvorlage -> Druck
- bestehende technische Dokumentation bleibt zusätzlich erhalten

### Update-Center
- Admin-geschützt
- Updatequellen:
  - Netzwerk-/UNC-Pfad
  - USB-Stick / Wechseldatenträger
  - lokale Update-ZIP
- standardisiertes Updateformat mit:
  - `partcounter-update.json`
  - `payload/`
  - `payload-sha256.txt`
- prüft Produkt, Version, Architektur und SHA-256 jeder Payload-Datei
- Staging vor Installation
- Backup vorhandener Programmdateien
- Update wird nach Beenden der laufenden Instanz angewendet
- Neustart der neuen `Partcounter.exe`
- Best-Effort-Rollback aus Backup bei Installationsfehler
- `%LOCALAPPDATA%\Partcounter` bleibt außerhalb des Payloads; Datenbank, Admin-Zugang, Branding und benutzerspezifische Daten werden nicht absichtlich überschrieben

## Build / Release

Der abschließende GitHub-Actions-Lauf war vollständig erfolgreich:
- Restore: success
- Build Release: success
- Publish portable folder win-x64: success
- Publish single-file win-x64: success
- Engineering package: success
- standardized update package: success
- Upload aller Artefakte: success

Erfolgreicher CI-Run: `33110331504`

### R001.14 Artefakte
- `Partcounter_R001_14_Portable_Folder_win-x64`
- `Partcounter_R001_14_SingleFile_win-x64`
- `Partcounter_R001_14_Engineering`
- `Partcounter_R001_14_UpdatePackage`

## Frühere Funktionen bleiben erhalten
- Leitstand für bis zu 30 Maschinen
- Artikelstamm
- VE-Historie
- Siemens LOGO! / Modbus TCP Protocol V2
- Partcounter_LOGO_V001 Engineering-Stand
- Auftragsstart, Pause, Fortsetzen, Ende
- VE-Rundung auf volle Werkzeugzyklen
- automatische VE-Wechsel / Q1
- Etikettendruck
- WYSIWYG Etiketteneditor
- Bilder/Logos im Etiketteneditor
- Firmenlogo im Leitstand, admin-konfigurierbar
- Admin-/Bediener-Trennung
- ARBURG ALS
- Inbetriebnahme-/Diagnosezentrum
- Rolloutstatus für 30 Maschinen

## Berechtigungen
Frei zugänglich:
- Leitstand
- Artikelstamm
- VE-Historie
- Hilfe
- Über

Admin-geschützt:
- Maschinen / Modbus
- Etiketteneditor
- Inbetriebnahme / Diagnose
- Rolloutstatus 30 Maschinen
- ARBURG ALS
- Einstellungen / Druck
- Firmenbranding konfigurieren
- Simulation <-> Echtbetrieb
- Update-Center

## Nächster Wiedereinstieg
Bei der nächsten Sitzung von diesem Checkpoint und Branch weiterarbeiten.
Empfohlener nächster Schritt: R001.14 auf einem echten Windows-PC testen, insbesondere Über-Fenster, F1-Hilfe und Update-Center mit einem Testpaket. Danach ggf. R001.15 für UX-Feinschliff, Hilfe-Vervollständigung oder signierte Updatepakete beginnen.

## Nicht ändern ohne bewusste Revision
- Modbus Protocol V2 Registermapping
- LOGO!-VM-Zuordnung
- Partcounter_LOGO_V001 Grundlogik
- bestehende Produktions-/VE-Logik
