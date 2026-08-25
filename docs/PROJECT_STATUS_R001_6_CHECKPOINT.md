# Partcounter – Projekt-Checkpoint R001.6

Gesicherter Stand zur Fortsetzung am nächsten Arbeitstag.

## Aktueller Revisionsstand

- Partcounter R001.6
- letzter vollständig grüner Windows-CI-Stand: Commit `91e718f48824b656d4cd3aa1f0b6a74820b12de2`
- Release-Build: erfolgreich
- Portable Publish win-x64: erfolgreich
- Single-File Publish win-x64: erfolgreich

## Enthaltener Funktionsstand

- Leitstand für bis zu 30 Spritzgussmaschinen
- Siemens LOGO! über Modbus TCP
- feste Maschinen-/Modbus-Konfiguration im Programm
- Artikelstamm mit Werkzeug, Kavitätenzahl 1–64 und Standard-VE-Menge
- Aufrundung auf vollständige Werkzeugzyklen
- Auftragsmenge, Auftragsfortschritt und letzte Teil-VE
- Auftrag starten, pausieren, fortsetzen und beenden
- Rechtsklick-Kontextmenü auf Maschinenkacheln
- temporäres Deaktivieren / Reaktivieren von Maschinen
- Maschinen ohne Auftrag standardmäßig ausblendbar
- Füllgrad-Ampel: normal / Vorwarnung / kritisch / VE voll
- automatische Fokusführung bei voller VE
- Always-on-Top-Mini-Monitor bei minimiertem Hauptfenster
- automatische VE-Historie
- automatischer Etikettendruck bei VE-Abschluss
- QR-Code- und Code-128-Vorbereitung/Erzeugung
- SQLite-Datenhaltung
- ARBURG-ALS-Schnittstelle mit Datei-/Hotfolder-Import und REST/JSON-Modus
- konfigurierbares ALS-Feldmapping und Maschinenalias-Mapping
- Authentifizierung für REST: Basic, Bearer, API-Key, optional Clientzertifikat
- detaillierte Bedienungs-, Modbus- und ALS-Inbetriebnahmedokumentation als DOCX/PDF

## Modbus-Grundstand

- PC = Modbus-TCP-Client/Master
- LOGO! = Modbus-TCP-Server/Slave
- Standardport: TCP 502
- Standard Unit-ID: 1
- Registerprotokoll PC→LOGO!: HR1–HR12
- Registerprotokoll LOGO!→PC: HR20–HR36
- 32-Bit-Zähler: High Word vor Low Word
- PC-Heartbeat und LOGO!-Heartbeat
- CommandSequence/AckSequence gegen Mehrfachauslösung
- lokale LOGO!-Zählung und VE-Wechsel auch bei PC/WLAN-Unterbrechung vorgesehen

## ARBURG ALS

R001.6 unterstützt derzeit:

1. Datei-/Hotfolder-Modus:
   - XLSX/XLSM
   - CSV/TXT/TSV
   - konfigurierbare Headerzeile, Trennzeichen, Encoding, Culture, Excel-Blatt
   - optional Archiv- und Fehlerordner

2. REST/JSON-Modus:
   - GET/POST
   - JSON-Wurzelpfad
   - frei konfigurierbares Feldmapping
   - Basic/Bearer/API-Key
   - zusätzliche Header
   - optional Clientzertifikat
   - Timeout und TLS-Optionen

Wichtig: Der konkrete ALS-Endpunkt bzw. die realen Exportspalten müssen bei der späteren Inbetriebnahme mit der vorhandenen ARBURG-ALS-Installation abgeglichen werden.

## Dokumentation

Erzeugte Handbücher:

- `Partcounter_R001_6_Bedienungsanleitung.docx`
- `Partcounter_R001_6_Bedienungsanleitung.pdf`

Die Dokumentation beschreibt insbesondere Netzwerk, Maschinen-/Modbus-Konfiguration, Registerbelegung, LOGO!-Logik, Inbetriebnahmereihenfolge, Etikettierung und ALS-Anbindung.

## Nächster sinnvoller Arbeitsschritt

- LOGO!-Standardprogramm `Partcounter_LOGO_V001` vollständig bis auf Block-/Merker-/Registerebene ausarbeiten
- reale I/O-Belegung für eine Testmaschine definieren
- erste echte LOGO! mit Partcounter koppeln
- Modbus-Handshake, Heartbeat, Zyklusimpuls und Ventilimpuls real testen
- anschließend reale ALS-Datenquelle anbinden und Mapping gegen Originaldaten verifizieren

Dieser Checkpoint ist die verbindliche Ausgangsbasis für die nächste Fortsetzung.
