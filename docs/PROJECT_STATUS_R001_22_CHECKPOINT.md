# Partcounter R001.22 – Projektcheckpoint

Stand: 29.08.2026  
Branch: `r001.22-admin-hub`  
Version: `0.1.22`

## Ziel dieser Revision

R001.22 fasst alle systemverändernden und administrativen Funktionen unter einem einzigen geschützten Hauptreiter zusammen. Der reguläre Produktionsanwender erhält dadurch eine klar reduzierte Bedienoberfläche, während die administrativen Funktionen fachlich getrennt erhalten bleiben.

## Normale Anwenderbereiche

Ohne Admin-Freigabe bleiben direkt zugänglich:

- Leitstand
- Artikelstamm
- VE-Historie
- Bedienung & Support / Hilfe

## Neuer Hauptreiter Administration

Der Hauptreiter zeigt seinen Schutzstatus unmittelbar sichtbar:

- `🔒 Administration` = gesperrt
- `🔓 Administration · <Bereich>` = für die laufende Sitzung entsperrt

Beim Öffnen eines gesperrten Administrationsbereichs wird die bestehende Admin-Authentifizierung verwendet. Wird die Administration wieder gesperrt, verlässt Partcounter einen geöffneten Administrationsbereich und kehrt in einen normalen Anwenderbereich zurück.

## Administrations-Unterbereiche

Die bisherigen Funktionen bleiben getrennt und werden unter Administration als eigene Unterreiter geführt:

1. Betriebsart
2. Zugriff / Sicherheit
3. Maschinen / Modbus
4. Etiketteneditor
5. Inbetriebnahme / Diagnose
6. Rolloutstatus 30 Maschinen
7. ARBURG ALS
8. Einstellungen / Druck

## Simulation / Echtbetrieb

Das Umschalten der Betriebsart ist ab R001.22 ausschließlich eine Adminfunktion.

- Der Umschalter wird aus der normalen Kopfzeile ausgeblendet.
- Der normale Benutzer sieht weiterhin die aktuelle Betriebsart.
- Umschalten ist nur unter `Administration → Betriebsart` möglich.
- Vor dem Wechsel wird eine ausdrückliche Bestätigung verlangt.
- Echtbetrieb weist darauf hin, dass anschließend reale Modbus-TCP-Kommunikation zu den freigegebenen Siemens-LOGO!-Stationen erfolgt.

## Kompatibilität

Für bestehende F1-Hilfe und automatische Dokumentationsaufnahme bleiben interne, unsichtbare Navigationsaliasse erhalten. Sie leiten nach erfolgreicher Admin-Freigabe auf den entsprechenden Unterbereich des neuen Administration-Hubs weiter. Die alten separaten Adminreiter werden dem Anwender nicht mehr als Hauptreiter angezeigt.

## Versionsführung

Die Assembly-Version ist `0.1.22`, FileVersion `0.1.22.0`, Revision `R001.22`. Die in R001.21 eingeführte zentrale Versionsquelle `AppVersionInfo` bleibt verbindlich.

## Noch bewusst zurückgestellt

Erst nach Abschluss der funktionalen Fehlerbereinigung und Praxistests:

- finales Partcounter-Programmicon
- gemeinsamer Paketinstaller mit Auswahl `Portable / Einzelplatz / Engineering`

## Prüfpunkte für den Praxistest

- Startanzeige zeigt R001.22.
- `🔒 Administration` ist im Hauptreiter sichtbar.
- Klick auf Administration verlangt bei gesperrtem Zustand das Admin-Passwort.
- Nach Freigabe erscheint `🔓 Administration`.
- Alle acht Unterbereiche sind vorhanden und funktional getrennt.
- Nach `Administration jetzt sperren` ist der Adminbereich nicht mehr zugänglich.
- Der Umschalter Simulation/Echtbetrieb ist im normalen Header nicht sichtbar.
- Die aktuelle Betriebsart bleibt im normalen Header sichtbar.
- Umschalten ist ausschließlich unter Administration → Betriebsart möglich.
- F1-Hilfe und automatische Dokumentationsaufnahme erreichen die verschobenen Adminbereiche weiterhin.
- Branding, Updatecenter, Backup/Diagnose und Druckeinstellungen sind unter Einstellungen / Druck vollständig vorhanden.
