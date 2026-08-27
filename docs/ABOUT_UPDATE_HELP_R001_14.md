# Partcounter R001.14 – Über, Update-Center und integrierte Hilfe

## Über-Funktion

Die frei zugängliche Funktion **Über** zeigt:

- Partcounter Revision und Assembly-Version
- Build-/Informational-Version
- Programmierer: Harald Peetz
- Technologie: C#, .NET 8, WPF, SQLite, NModbus
- Modbus Protocol Version
- LOGO!-Programmstand
- Windows-/OS-Information
- .NET Runtime
- Prozess-/OS-Architektur
- Computername
- Programm-, Daten- und Datenbankpfad
- kurzen proprietären Lizenzhinweis

Die Systeminformation kann in die Zwischenablage kopiert werden.

## Integrierte Hilfe

Die Hilfe ist ohne Admin-Anmeldung über **Hilfe (F1)** oder die Taste **F1** erreichbar.

Sie enthält einen strukturierten Themenkatalog mit:

- Volltextsuche
- Kategorien
- ausführlicher Beschreibung je Programmfunktion
- expliziten direkten Abhängigkeiten
- expliziten Folgewirkungen / aufbauenden Funktionen
- anklickbaren Sprüngen zwischen verknüpften Themen

Die Hilfedatei ist als Embedded Resource Bestandteil der EXE und wird parallel im Engineering-Paket ausgeliefert.

## Update-Center

Das Update-Center liegt im bereits Admin-geschützten Bereich **Einstellungen / Druck**.

Unterstützte Bezugswege:

1. konfigurierter Netzwerk-/UNC-Ordner,
2. USB-Stick über lokale Dateiauswahl,
3. beliebige lokale ZIP-Datei.

Alle Bezugswege verwenden dasselbe Updateformat und denselben Prüf-/Installationsweg.

### Updatepaketformat

Ein gültiges Paket enthält im ZIP-Wurzelverzeichnis:

- `partcounter-update.json`
- `payload-sha256.txt`
- `payload/`

Manifest-Beispiel:

```json
{
  "schemaVersion": 1,
  "product": "Partcounter",
  "version": "0.1.14",
  "revision": "R001.14",
  "architecture": "win-x64",
  "payloadRoot": "payload/",
  "createdAtUtc": "2026-08-27T00:00:00Z",
  "releaseNotes": "..."
}
```

Partcounter prüft vor Freigabe:

- ZIP-Struktur,
- SchemaVersion,
- Product = Partcounter,
- Zielversion,
- Architektur win-x64,
- Vorhandensein von `Partcounter.exe`,
- SHA-256 jeder Payload-Datei.

### Installationsablauf

1. Paket prüfen.
2. Payload in `%LOCALAPPDATA%\Partcounter\Updates\Staging` entpacken.
3. Schreibbarkeit des aktuellen Installationsordners prüfen.
4. lokalen PowerShell-Installationsprozess erzeugen; Skripte aus dem Updatepaket selbst werden nicht ausgeführt.
5. laufenden Partcounter-Prozess beenden.
6. zu ersetzende Dateien nach `%LOCALAPPDATA%\Partcounter\Updates\Backups` sichern.
7. neuen Payload kopieren.
8. Partcounter neu starten.
9. Ablauf in `%LOCALAPPDATA%\Partcounter\Updates\update.log` protokollieren.

Die SQLite-Datenbank, Admin-Zugangsdaten, Brandingdateien und andere Benutzerdaten in `%LOCALAPPDATA%\Partcounter` sind nicht Bestandteil des Payload und bleiben erhalten.

## Einschränkung

Ein In-Place-Update benötigt Schreibrechte auf den Ordner, in dem Partcounter aktuell ausgeführt wird. Für Portable-/Single-File-Ausgaben in benutzereigenen Ordnern ist dies typischerweise gegeben. Bei einem schreibgeschützten Installationspfad meldet Partcounter den Zustand vor dem Beenden der Anwendung.
