# Partcounter R001.25 Hotfix 3 – Betriebsart-Umschaltung

## Anlass

Im bisherigen R001.25-HF2-Stand war die Umschaltung Simulation/Echtbetrieb zwar im `MainViewModel` vollständig vorhanden, in der realen Bedienoberfläche aber nicht zuverlässig erreichbar:

1. Der Schalter befand sich ausschließlich rechts in der Kopfzeile. Bei kleiner effektiver Desktopbreite beziehungsweise Windows-DPI-Skalierung konnte dieser Bereich außerhalb des nutzbaren Fensters liegen.
2. `MainWindow.AttachAdminControls()` trennte den Kopfzeilen-Schalter nach dem Start vom regulären `ToggleOperatingModeCommand` und leitete den Klick durch die Admin-Freigabe. Damit wurde eine reguläre Bedienfunktion fälschlich wie eine geschützte Systemeinstellung behandelt.
3. Das Hauptfenster deklarierte `MinWidth=1180` / `MinHeight=720`, obwohl das bereits etablierte Layout-Gate bis 800×500 prüft. Auf einem 1366-Pixel-Display mit 125-%-Skalierung kann die effektive Breite unter 1180 WPF-Einheiten liegen.

## Korrekturen HF3

- Neuer dauerhaft sichtbarer Betriebsart-Balken direkt oberhalb der Hauptreiter.
- Eindeutige Anzeige des aktuellen Zustands über `SystemStatusText`.
- Großer Schalter mit direkter Bindung an `ToggleOperatingModeCommand`.
- Der neue Schalter ist **nicht** an die Admin-Sperre gekoppelt.
- Der historische Kopfzeilen-Schalter wird ausgeblendet, damit es keine widersprüchlichen Bedienwege gibt.
- Reale Mindestfenstergröße wird beim Start auf 800×500 gesetzt und damit an das bestehende Multi-Resolution-Layoutgate angeglichen.
- Administrative Bereiche wie Maschinen/Modbus, LOGO!-Konfiguration, Etiketten/Drucker und Systemeinstellungen bleiben weiterhin geschützt.

## Bedienkonzept

**Frei zugängliche Betriebsfunktionen:**
- Leitstand,
- Artikelstamm,
- VE-Historie,
- Simulation/Echtbetrieb umschalten,
- reguläre Auftragsbedienung.

**Admin-geschützte Konfiguration:**
- Maschinen-/Modbus-Konfiguration,
- LOGO!- und Inbetriebnahmeparameter,
- Drucker-/Etikettenkonfiguration,
- weitere Systemeinstellungen.

## Versionsstand

- Produkt: Partcounter
- Revision: R001.25 HF3
- Product Version: 0.1.25
- FileVersion: 0.1.25.3
- InformationalVersion: `0.1.25-r001.25-hf3-operating-mode-ui`
- LOGO!-Schnittstelle: Modbus TCP Protocol V3

## LOGO!-Stand

Der Siemens-LOGO!-Programmstand bleibt `PARTCOUNTER_LOGO_V001_R001_25_HF3_4_TRANSFERREADY.lsc`. HF3 verändert ausschließlich die PC-Bedienoberfläche; die Protocol-V3-Registermatrix und der validierte LOGO!-FBD-/VM-Graph bleiben unverändert.
