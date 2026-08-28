# Partcounter R001.15 – Production Readiness

## Ziel

R001.15 erhöht die Betriebssicherheit des Partcounter-PC-Systems, ohne den produktionskritischen Modbus-/LOGO!-Kern oder die frei zugänglichen Bedienfunktionen zu verändern.

Unverändert frei zugänglich bleiben:

- Leitstand
- Artikelstamm
- VE-Historie

Die neuen Servicefunktionen befinden sich im bereits geschützten Bereich **Einstellungen / Druck** und erfordern damit weiterhin Admin-Freigabe.

## Neue Funktionen

### 1. Automatische tägliche Datenbanksicherung

Beim Programmstart prüft Partcounter, ob für den aktuellen Kalendertag bereits eine Sicherung existiert. Fehlt sie, wird automatisch eine konsistente SQLite-Sicherung erzeugt.

Speicherort:

`%LOCALAPPDATA%\Partcounter\Backups`

Dateischema:

`partcounter_YYYYMMDD_HHMMSS.db`

Es werden maximal die 30 jüngsten Sicherungen behalten. Ältere Sicherungen werden automatisch entfernt.

### 2. WAL-sichere Sicherung

Die Datenbank läuft weiterhin im SQLite-WAL-Modus. Die Sicherung wird nicht durch simples Kopieren der aktiven `.db`-Datei erzeugt, sondern über die SQLite-Backup-API. Dadurch wird ein konsistenter Datenbankzustand erzeugt, auch wenn Partcounter läuft.

Nach jeder Sicherung wird auf der Sicherungsdatei `PRAGMA quick_check` ausgeführt. Eine fehlerhafte Sicherung wird verworfen.

### 3. Manuelle Datenbankprüfung

Im geschützten Einstellungsbereich steht **Datenbank prüfen** zur Verfügung.

Geprüft werden:

- `PRAGMA quick_check`
- `PRAGMA foreign_key_check`

Das Ergebnis wird direkt im Servicebereich angezeigt. Auffällige Ergebnisse erzeugen zusätzlich eine Warnmeldung.

### 4. Manuelle Sicherung

Über **Sicherung jetzt erstellen** kann jederzeit zusätzlich eine konsistente Sicherung angelegt werden.

Der Button **Sicherungsordner öffnen** öffnet den Sicherungsordner direkt im Windows Explorer.

### 5. Diagnosepaket

Über **Diagnosepaket erstellen** wird ein ZIP-Paket für Support und Fehlersuche erzeugt.

Speicherort:

`%LOCALAPPDATA%\Partcounter\Diagnostics`

Enthalten sind:

- Revisions-/Assembly-/Runtime-Informationen
- Betriebssystem- und Prozessinformationen
- Ergebnis der SQLite-Integritätsprüfung
- Partcounter-Startprotokoll, sofern vorhanden
- die letzten 250 Ereigniseinträge aus der Events-Tabelle

Bewusst **nicht** enthalten sind:

- Settings-Tabelle
- Datenbanksicherung
- gespeicherte Update-/Authentifizierungsparameter als separater Export

Dadurch bleibt das Diagnosepaket deutlich datensparsamer als ein vollständiger Datenbankexport.

## Verzeichnisse

- Daten: `%LOCALAPPDATA%\Partcounter`
- Sicherungen: `%LOCALAPPDATA%\Partcounter\Backups`
- Diagnose: `%LOCALAPPDATA%\Partcounter\Diagnostics`
- Updates: `%LOCALAPPDATA%\Partcounter\Updates`

## Abnahmekriterien R001.15

1. Anwendung startet mit Versionsanzeige R001.15.
2. Leitstand, Artikelstamm und VE-Historie bleiben ohne Admin-Anmeldung zugänglich.
3. Einstellungen / Druck bleibt Admin-geschützt.
4. Beim ersten Start eines Kalendertages wird genau eine automatische Sicherung erzeugt.
5. Ein weiterer Start am selben Tag erzeugt keine zusätzliche automatische Tagessicherung.
6. Eine manuelle Sicherung kann jederzeit zusätzlich erzeugt werden.
7. Jede Sicherung besteht `PRAGMA quick_check`.
8. Bei mehr als 30 Sicherungen werden die ältesten Sicherungen entfernt.
9. Die manuelle Datenbankprüfung meldet `quick_check` und Fremdschlüsselstatus.
10. Das Diagnosepaket enthält keine Datenbanksicherung und keinen Settings-Export.
11. Der Modbus-Registerplan und Partcounter_LOGO_V001 bleiben unverändert.
12. GitHub Actions erzeugt Portable-, Single-File-, Engineering- und UpdatePackage-Artefakte für R001.15.

## Sicherheitsabgrenzung

Partcounter und Partcounter_LOGO_V001 sind weiterhin keine Safety-Steuerung. Not-Halt, Schutztüren, Maschinenfreigaben und andere sicherheitsgerichtete Funktionen verbleiben vollständig in den dafür vorgesehenen sicheren Maschinenkreisen.
