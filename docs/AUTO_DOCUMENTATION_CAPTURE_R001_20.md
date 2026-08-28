# Partcounter R001.20 – Automatische Dokumentationsaufnahme

Stand: 29.08.2026

## Ziel
R001.20 ersetzt den weitgehend manuellen Screenshot-Workflow der R001.19-Hilfe durch eine Ein-Klick-Aufnahme direkt aus der laufenden Partcounter-WPF-Oberfläche.

## Bedienung
1. Partcounter öffnen.
2. Optional die geschützten Administrationsbereiche einmal regulär freigeben, wenn diese im Screenshot-Paket enthalten sein sollen.
3. `Hilfe` öffnen.
4. Im linken Bereich `Screenshots automatisch erstellen` wählen.
5. Sicherheitsabfrage bestätigen.
6. Partcounter navigiert selbständig durch die verfügbaren Ansichten und rendert die PNG-Dateien.
7. Anschließend wird automatisch ein ZIP-Paket erstellt.

## Ausgabe
- PNG-Ordner: `%LOCALAPPDATA%\Partcounter\HelpScreenshots`
- ZIP-Pakete: `%LOCALAPPDATA%\Partcounter\DocumentationPackages`
- Manifest: `CAPTURE_MANIFEST.txt`

## Automatisch erfasste Ansichten
Der erste R001.20-Lauf zielt insbesondere auf die R001.19-Priorität-A-Bilder:
- Hilfezentrum
- Hauptnavigation / Leitstand
- Auftrag starten
- Artikelstamm
- VE-Historie
- Reprint-Dialog
- Reprint-/Snapshotstatus
- Maschinen / Modbus
- Etiketteneditor
- Etiketteneditor mit Bild-/Logo-Kontext
- Inbetriebnahme / Diagnose
- Live-Abnahme
- Rolloutstatus
- ARBURG ALS Aufträge
- ARBURG ALS Verbindung / Quelle
- ARBURG ALS Feldmapping
- kontrollierte ALS-Fehlerdiagnose
- Einstellungen / Druck
- Updatecenter
- Backup / Diagnose

## Datenschutz / Redaction
Die Aufnahme verwendet die echte Partcounter-Oberfläche, darf aber keine realen Zugangsdaten in die Dokumentation übernehmen.

Vor dem Rendern werden deshalb sensible gebundene Werte temporär unsichtbar geschaltet, unter anderem:
- Auftragsnummern
- Artikelnummern und Artikeltexte
- Werkzeugnummern
- Maschinennamen, soweit dynamisch gebunden
- IP-Adressen
- ALS-Dateipfade
- REST-URLs
- Benutzername / Passwort
- Bearer Token / API-Key
- Zertifikatpfade und Zertifikatpasswörter
- zusätzliche HTTP-Header und Request-Body
- Druckername
- Maschinen-Alias-Mapping

Die zugrunde liegenden ViewModels, Settings und Datenbankwerte werden dabei nicht verändert. Nach jedem Rendern wird die ursprüngliche UI-Darstellung wiederhergestellt.

## ALS-Fehlerbild
`74_als_fehlerdiagnose.png` wird ohne echten Fehler erzeugt. Partcounter ersetzt nur während des Renderns die sichtbare ALS-Statuszeile durch einen kontrollierten Dokumentationstext:

`Dokumentationstest: ALS-Quelle nicht erreichbar – Datei/Verzeichnis nicht gefunden. Bitte Pfad und Berechtigungen prüfen.`

Danach wird die echte WPF-Bindung sofort wiederhergestellt. Es wird weder eine falsche URL aufgerufen noch eine Netzwerkverbindung provoziert.

## Safety / Modbus
Die Dokumentationsaufnahme:
- sendet keine Modbus-Schreibbefehle,
- schaltet keinen LOGO!-Ausgang,
- startet keinen Auftrag,
- löst keinen VE-Wechsel aus,
- entsperrt keine geschützten Reiter selbständig.

## Hilfeintegration
Das Hilfezentrum sucht Screenshots ab R001.20 zuerst unter `%LOCALAPPDATA%\Partcounter\HelpScreenshots`. Dadurch werden frisch erzeugte Originalbilder sofort angezeigt. Eingebettete Screenshots aus dem Programm bleiben der zweite Fallback.
