# Partcounter R001.21 – Bedienung, Support und Versionsführung

Stand: 29.08.2026
Branch: `r001.21-support-version-core`
Version: `0.1.21`

## Ziel

R001.21 baut den Bedien- und Supportbereich aus und beseitigt die bisherige Versionsdrift zwischen Assembly, Fenstertitel, Statusanzeige, Updatebereich und älteren Bootstrap-Bausteinen.

## Zentrale Versionsquelle

Neu ist `AppVersionInfo`.

Die sichtbare Partcounter-Revision wird aus der tatsächlich laufenden Assembly-Version abgeleitet:

- `0.1.21` -> `R001.21`
- Fenstertitel -> `Partcounter R001.21`
- Simulation -> `R001.21 · SIMULATION`
- Echtbetrieb -> `R001.21 · ECHTBETRIEB MODBUS TCP`
- Supportinfo und Supportpaket übernehmen dieselbe Quelle.

Damit darf bei neuen Releases nicht mehr an mehreren UI-Stellen manuell eine Revision nachgeführt werden.

`VersionUiService` normalisiert zusätzlich bekannte alte sichtbare Kennzeichnungen nach dem Laden eines WPF-Fensters. Historische Versionsangaben innerhalb fachlicher Hilfe-/Dokumentationstexte bleiben unverändert, weil sie den Entstehungsstand einer Funktion dokumentieren.

## Neuer Bereich „Bedienung & Support“

Der Hauptkopf erhält einen direkten Einstieg `Bedienung & Support`.

Der neue Supportbereich bündelt:

- aktuelle Revision, Version und Buildinformation
- Betriebszustand Simulation/Echtbetrieb
- Windows/.NET/Architektur
- SQLite-Integritätsprüfung
- letzte Sicherung
- manuelle Datensicherung
- aktuelles Support-/Diagnosepaket
- Supportinfo für Zwischenablage
- direkter Zugriff auf Daten-, Sicherungs-, Diagnose- und Screenshotordner
- Startprotokoll öffnen
- direkte geführte Hilfe zu Leitstand, VE-Historie/Reprint, Modbus, ARBURG ALS, Etiketteneditor, Inbetriebnahme und Einstellungen

## Supportpaket

Der neue `SupportDiagnosticService` verwendet den vorhandenen Diagnosekern, normalisiert aber Paketname und Manifest auf die tatsächlich laufende Programmversion.

Beispiel:

`Partcounter_Diagnose_R001_21_20260829_223500.zip`

Das Supportpaket enthält weiterhin keine Datenbanksicherung und keine Settings-Tabelle. Passwörter, Tokens oder API-Keys werden nicht aus den Einstellungen exportiert.

## Bestehende Hilfe

Die professionelle F1-Kontexthilfe aus R001.19 und die automatische Screenshot-Erfassung aus R001.20 bleiben vollständig erhalten. Der neue Supportbereich ergänzt diese Funktionen um eine betriebliche Erstdiagnose und schnellere Navigation.

## Release-Regel ab R001.21

Vor einem neuen Partcounter-Release muss mindestens geprüft werden:

1. `<Version>` im Projekt wurde erhöht.
2. `<FileVersion>` entspricht dem Release.
3. `<InformationalVersion>` nennt Revision/Branch korrekt.
4. Fenstertitel zeigt die daraus abgeleitete Revision.
5. Statuszeile zeigt dieselbe Revision.
6. Über-Fenster zeigt dieselbe Revision.
7. Hilfe-/Supportfenster zeigt dieselbe Revision.
8. Update-Manifest enthält dieselbe Version/Revision.
9. Supportpaket enthält dieselbe Version/Revision.
10. Windows-Release-Build läuft erfolgreich durch.

## Bewusst noch nicht Teil von R001.21

- finaler Paketinstaller
- Auswahl Portable / Einzelplatz / Engineering im Installer
- finales Partcounter-Programmicon

Diese Punkte werden erst umgesetzt, wenn die aktuellen Funktionen und Bugs praktisch validiert sind.
