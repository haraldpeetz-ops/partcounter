# Partcounter R001.13 – Firmenlogo / Leitstand-Branding

## Ziel

R001.13 ergänzt Partcounter um ein frei konfigurierbares Firmenlogo in der Kopfzeile des Leitstands.
Die Funktion ist rein PC-seitig. LOGO!, Modbus Protocol V2, Zählung, VE-Wechsel und Auftragslogik werden nicht verändert.

## Bedienung

Die Konfiguration liegt im bereits geschützten Bereich **Einstellungen / Druck**.
Der normale Produktionsbetrieb bleibt ohne Anmeldung verfügbar.

Nach Admin-Entsperrung stehen bereit:

- Firmenlogo auswählen / ersetzen
- Vorschau des aktuell gespeicherten Logos
- Anzeige des ursprünglichen Dateinamens
- Firmenlogo entfernen

## Darstellung

Das Logo erscheint links oben neben dem PARTCOUNTER-Schriftzug.
Es wird proportional auf maximal ca. 190 × 52 Pixel skaliert.
Ist kein Logo gespeichert, wird der Logobereich vollständig ausgeblendet und die bisherige Kopfzeile bleibt erhalten.

## Unterstützte Formate

- PNG
- JPG / JPEG
- BMP
- GIF
- TIFF

Maximale Dateigröße: 10 MB.
Für Firmenlogos wird PNG mit transparentem Hintergrund empfohlen.

## Speicherung

Die ausgewählte Originaldatei wird nicht nur referenziert. Partcounter kopiert sie in:

`%LOCALAPPDATA%\Partcounter\Branding`

Der gespeicherte Dateiname und der ursprüngliche Dateiname werden zusätzlich in der bestehenden SQLite-Settings-Tabelle hinterlegt.
Dadurch bleibt das Logo verfügbar, auch wenn die ursprünglich ausgewählte Datei später verschoben, umbenannt oder gelöscht wird.

## Sicherheit

Die Logoauswahl befindet sich im Admin-geschützten Einstellungsbereich aus R001.12.
Damit können Bediener Leitstand, Artikelstamm und VE-Historie frei verwenden, das Firmenbranding jedoch nicht unbeabsichtigt verändern.

## Kompatibilität

- basiert auf R001.12
- R001.11 Bild-/Logo-Unterstützung im Etiketteneditor bleibt erhalten
- bestehende Etikettenvorlagen bleiben unverändert
- Modbus Register Map V2 unverändert
- Partcounter_LOGO_V001 unverändert
