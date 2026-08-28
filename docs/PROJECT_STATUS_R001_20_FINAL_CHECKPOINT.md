# Partcounter R001.20 – Finaler Sitzungscheckpoint

Stand: 29.08.2026
Branch: `r001.20-auto-doc-capture`
Version: `0.1.20`

## Aktueller Gesamtstand

R001.20 ist der derzeit aktuelle Entwicklungs- und Buildstand von Partcounter. Der vollständige Windows-.NET-8-Build wurde erfolgreich validiert.

GitHub Actions Run: `33216353376`
Ergebnis: `success`

Erfolgreich geprüft/erzeugt:
- Restore
- Release-Build
- Portable Folder win-x64
- SingleFile win-x64
- Engineering-Paket
- UpdatePackage

## Neu in R001.20 – Auto Documentation Capture

- automatische Erzeugung echter PNG-Screenshots direkt aus der laufenden WPF-Anwendung
- Schaltfläche `Screenshots automatisch erstellen` im Hilfezentrum
- automatisches Durchschalten der verfügbaren Partcounter-Bereiche
- Ablage unter `%LOCALAPPDATA%\Partcounter\HelpScreenshots`
- automatisches ZIP-Paket unter `%LOCALAPPDATA%\Partcounter\DocumentationPackages`
- `CAPTURE_MANIFEST.txt` mit erstellten und ausgelassenen Bildern
- integrierte Hilfe lädt automatisch erzeugte Screenshots bevorzugt aus dem lokalen Screenshotordner
- keine manuelle Einzelzuordnung erforderlich

## Automatisch vorgesehene/erzeugbare Screenshots

Unter anderem:
- `00_hilfezentrum.png`
- `03_hauptnavigation.png`
- `10_leitstand_uebersicht.png`
- `11_auftrag_starten.png`
- `20_artikelstamm.png`
- `30_ve_historie.png`
- `31_reprint_dialog.png`
- `32_reprint_snapshot_status.png`
- `40_maschinen_modbus.png`
- `50_etiketteneditor_gesamt.png`
- `52_etiketteneditor_bild.png`
- `60_inbetriebnahme_gesamt.png`
- `61_live_abnahme.png`
- `62_rollout_30.png`
- `70_als_auftraege.png`
- `71_als_verbindung.png`
- `72_als_feldmapping.png`
- `74_als_fehlerdiagnose.png`
- `80_druckeinstellungen.png`
- `82_updatecenter.png`
- `83_backup_diagnose.png`

## Datenschutz/Sicherheit der Dokumentationsaufnahme

- keine Modbus-Schreibbefehle durch die Dokumentationsfunktion
- keine automatische Entsperrung geschützter Admin-Bereiche
- bekannte sensible UI-Bindungen werden beim Rendern temporär redigiert
- reale Auftrags-/Artikelnummern, IP-Adressen, ALS-Pfade, REST-URLs, Benutzer-/Passwortfelder, Tokens, API-Keys, Zertifikatsdaten und Druckernamen sollen nicht in die Dokumentationsscreenshots übernommen werden
- die zugrunde liegenden Daten/Einstellungen werden nicht verändert
- der Reprint-Dialog wird mit einem vollständig künstlichen Demo-VE-Datensatz erzeugt
- `74_als_fehlerdiagnose.png` verwendet nur eine temporäre UI-Dokumentationsmeldung; es wird kein echter ALS-/Netzwerkfehler provoziert

## R001.19 – professionelle Hilfe weiterhin enthalten

- stark erweiterte integrierte Hilfe
- formatierte WPF-Dokumentdarstellung
- kontextbezogenes F1
- `Bereichshilfe (F1)`
- Hilfe für Leitstand, Artikelstamm, VE-Historie, Reprint, Maschinen/Modbus, Etiketteneditor, Inbetriebnahme, ARBURG ALS, Einstellungen und Fehlersuche
- Screenshot-Slots je Hilfethema
- Suchfunktion und Funktionsabhängigkeiten

## R001.18 – weiterhin enthalten

- ARBURG-ALS-OneWay-Binding-Hotfix für schreibgeschützte Mappingfelder
- Etiketten-Reprint aus VE-Historie
- Nachdruckjournal
- historischer Layout-Snapshot pro regulär gedruckter VE
- eingebettete Bilder/Logos im Snapshot
- SHA-256-Integritätsprüfung vor historischem Reprint
- klar protokollierter Fallback für ältere VEs ohne Snapshot

## Frühere stabile Funktionen weiterhin enthalten

- R001.16 Live-Abnahmemessung
- R001.15 Produktionsbackup/Diagnose
- Firmenbranding/Logo
- Benutzer-/Admintrennung
- Etiketteneditor mit Bild-/Logo-Unterstützung
- VE-Historie
- Artikelstamm
- Leitstand
- ARBURG ALS Integration
- Modbus Protocol V2
- `Partcounter_LOGO_V001`

## Nächster sinnvoller Einstieg

Beim nächsten Termin auf diesem Branch weiterarbeiten.

Empfohlener Testablauf:
1. R001.20 SingleFile starten.
2. Hilfezentrum öffnen.
3. Geschützte Bereiche bei Bedarf regulär freigeben.
4. `Screenshots automatisch erstellen` ausführen.
5. Ergebnisordner/ZIP und `CAPTURE_MANIFEST.txt` prüfen.
6. Prüfen, ob alle gewünschten Screenshots sinnvoll gerendert und sensible Daten redigiert wurden.
7. Danach können die automatisch erzeugten Originalbilder als Basis für ein bebildertes PDF-Benutzerhandbuch verwendet werden.

## Referenz

Aktiver Branch: `r001.20-auto-doc-capture`
Letzter vollständig validierter Release-Stand: `R001.20 / 0.1.20`
