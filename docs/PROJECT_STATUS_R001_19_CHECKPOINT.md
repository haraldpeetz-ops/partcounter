# Partcounter R001.19 – Projektcheckpoint

Stand: 28.08.2026
Branch: `r001.19-help-professional`
Version: `0.1.19`

## Ziel der Revision
R001.19 baut die bisherige integrierte Hilfe zu einer professionellen Bedienungs-, Inbetriebnahme- und Fehlersuchhilfe aus und bereitet die systematische Ergänzung echter Partcounter-Screenshots vor.

## Neu in R001.19

- neue ausführliche Hilfedatenbank `PARTCOUNTER_HILFE_R001_19.md`
- Hilfe für Schnellstart, Sicherheit, Bedienoberfläche, Leitstand, Auftragslogik, Füllgrad, Artikelstamm, VE-Historie, Reprint, Layout-Snapshot, Maschinen/Modbus, Etiketteneditor, Inbetriebnahme, Rollout, ARBURG ALS, Druck, Updates, Backup und Fehlersuche
- kontextbezogenes **F1**: aktueller Hauptbereich bestimmt automatisch das Hilfethema
- zusätzliche Kopf-Schaltfläche `Bereichshilfe (F1)`
- separate Schaltfläche `Hilfe` für das vollständige Hilfezentrum
- formatierte WPF-Dokumentdarstellung statt reinem Textfeld
- Überschriften, nummerierte Abläufe, Aufzählungen, Code-Darstellung sowie farblich hervorgehobene WICHTIG-/WARNUNG-/PRAXIS-/TIPP-Blöcke
- Screenshot-Metadaten pro Hilfethema
- automatische Anzeige eingebetteter PNG/JPG-Originalscreenshots aus `Help/Screenshots`
- aussagekräftiger Screenshot-Platzhalter, solange ein Bild noch nicht vorhanden ist
- Aufnahmeanweisung direkt im Hilfethema und per Zwischenablage kopierbar
- detaillierter Screenshot-Aufnahmeplan für das spätere bebilderte PDF-Handbuch
- Versionsangaben im Über-Fenster auf R001.19 vereinheitlicht
- `ProfessionalHelpBootstrap` hält Titel und Betriebsstatus nach Umschalten Simulation/Echtbetrieb auf R001.19

## Screenshot-Konzept

Die Hilfe enthält bereits feste Screenshot-Dateinamen. Originalbilder werden später unter
`src/Partcounter.App/Help/Screenshots/` hinterlegt. Fehlt ein Bild, bleibt das Hilfethema vollständig nutzbar und zeigt die konkrete Aufnahmeanweisung.

Priorität A umfasst die wichtigsten Produktions- und Engineering-Ansichten, darunter Leitstand, Auftrag, Artikelstamm, VE-Historie/Reprint, Maschinen/Modbus, Etiketteneditor, Inbetriebnahme, ARBURG ALS und Einstellungen.

## Beibehalten aus R001.18

- ARBURG-ALS-OneWay-Binding-Hotfix
- Etiketten-Reprint und Druckjournal
- SHA-256-geprüfte historische Etiketten-Layout-Snapshots
- Live-Abnahme R001.16
- Produktionsbackup/Diagnose R001.15
- Firmenbranding, Benutzer-/Admintrennung, Bilder/Logos im Etiketteneditor
- Modbus Protocol V2 und `Partcounter_LOGO_V001`

## Validierung

Der erste R001.19-CI-Lauf hat eine lokale C#-Namenskollision (`stack`) im erweiterten Update-/Hilfe-Bootstrap gefunden. Diese wurde anschließend behoben (`directStack`, `scrollStack`, `updatePanelStack`). Dieser Checkpoint triggert den Windows-Release-Build auf dem korrigierten Stand.

## Nächster Schritt nach grünem Build

1. R001.19 auf dem realen Windows-PC starten.
2. F1 in mehreren Hauptbereichen testen.
3. Hilfezentrum auf Lesbarkeit und Fenster-Skalierung prüfen.
4. Priorität-A-Screenshots aus `HELP_SCREENSHOT_CAPTURE_PLAN_R001_19.md` aufnehmen.
5. Screenshots in kleinen Paketen in die Hilfe integrieren.
6. Danach das bebilderte PDF-Benutzerhandbuch aus dem gleichen Inhaltsstand erzeugen.
