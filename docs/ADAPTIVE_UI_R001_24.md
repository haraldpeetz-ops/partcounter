# Partcounter R001.24 – Adaptive UI / Notebook- und DPI-Standard

## Ziel

Ab R001.24 darf die Bedienbarkeit von Partcounter nicht mehr von der ursprünglichen Entwicklungsauflösung 1680×980 abhängen. Die Anwendung muss ihre reale Windows-Arbeitsfläche erkennen und auf kleineren Notebook-Displays sowie bei höherer Windows-Skalierung bedienbar bleiben.

## Technische Maßnahmen

### Per-Monitor-DPI

Die Anwendung besitzt ein explizites Windows-Manifest mit:

- `asInvoker` – keine Adminrechte allein wegen DPI,
- `PerMonitorV2,PerMonitor`,
- DPI-aware `true/pm`.

Damit behandelt Windows Partcounter als DPI-bewusste Desktopanwendung und WPF erhält beim Monitor-/DPI-Wechsel eine passende logische Arbeitsfläche.

### Zentrale AdaptiveUiService-Schicht

Alle WPF-Window-Instanzen werden zentral erfasst. Bei Loaded, SizeChanged und WindowStateChanged wird die Oberfläche neu bewertet.

Der Service:

- begrenzt Fenster auf `SystemParameters.WorkArea`,
- reduziert historische `MinWidth`/`MinHeight`-Vorgaben, wenn sie größer als die reale Arbeitsfläche wären,
- nutzt auf kleineren Notebooks die verfügbare Arbeitsfläche weitgehend vollständig,
- setzt DataGrid-Scrollpfade auf horizontal/vertikal `Auto`,
- entfernt zu große Mindestmaße aus großen Layoutcontainern,
- löst große feste StackPanel-/Border-Breiten bei zu kleinem Viewport temporär auf und stellt sie bei ausreichend Platz wieder her,
- komprimiert große feste Grid-Spalten nur dann, wenn sie unverhältnismäßig viel Platz belegen,
- aktiviert Touch-/Trackpad-Panning auf ScrollViewern,
- reduziert Abstände und Überschriftgrößen auf kleinen Viewports.

### Leitstand-Kacheln

Die Maschinenkacheln passen ihre Spaltenzahl automatisch an:

| logische verfügbare Breite | Kacheln pro Zeile |
|---:|---:|
| < 850 | 1 |
| 850…1079 | 2 |
| 1080…1359 | 3 |
| 1360…1649 | 4 |
| >= 1650 | 5 |

Damit werden Kacheln auf einem Notebook nicht mehr bis zur Unbedienbarkeit zusammengestaucht.

### Kopfzeile

Bei kompakter Arbeitsfläche wechselt der rechte Status-/Bedienbereich aus horizontaler in vertikale Anordnung. Titel und Untertitel werden verkleinert bzw. umgebrochen. Dadurch überlagern Versionsstatus, Maschinenstatus und Support/Admin-Bedienelemente nicht mehr den Partcounter-Titel.

## Automatischer Layout-Smoke-Test

R001.24 besitzt einen eigenen Laufmodus `--layout-smoke`. Dieser startet die echte veröffentlichte WPF-Anwendung und schaltet alle sichtbaren Haupt- und verschachtelten Unterreiter durch.

Getestete logische WPF-Arbeitsflächen:

- 800×500
- 1024×600
- 1280×720
- 1366×768
- 1600×900
- 1920×1080

Der Validator sucht insbesondere nach:

- MinWidth/MinHeight größer als der Viewport,
- sichtbaren bedienbaren Elementen außerhalb des Fensters,
- nicht scrollbar erreichbaren Überläufen.

## Validierungsergebnis des ersten vollständigen R001.24-Codebuilds

GitHub Actions Run `33279312648`, Commit `93074ecc04f9d5d59a541e8d5fe08505be7c2acc`:

- Release Build: PASS
- Portable Publish: PASS
- SingleFile Publish: PASS
- R001.24 WPF-Stresstest: PASS
- Multi-Resolution WPF Layout Smoke Test: **PASS**
- alle sechs Viewports vollständig durchlaufen
- **keine nicht-scrollbaren Überläufe erkannt**

Zusätzlich bestand derselbe Build erneut den 30-Maschinen-Stresstest mit 1.920.000 simulierten Teilen und 1.920/1.920 persistierten VE-Datensätzen.

## Was der automatische Test realistisch leistet

Der Layout-Smoke-Test ist absichtlich strenger als die offiziell empfohlene Mindestarbeitsfläche und umfasst 800×500. Er überprüft WPF-Geometrie und Erreichbarkeit reproduzierbar.

Trotzdem ersetzt er nicht jede denkbare physische Kombination aus:

- Grafikkartentreiber,
- Windows-Taskleistenposition,
- benutzerdefinierter Schrift-/Textskalierung,
- mehreren Monitoren mit stark unterschiedlicher DPI,
- exotischen Remote-Desktop-Konfigurationen.

Darum bleibt für eine endgültige Produktfreigabe ein kurzer realer Notebook-Abnahmetest sinnvoll. R001.24 macht einen solchen Laptop jedoch nicht mehr zum ersten Ort, an dem offensichtliche abgeschnittene Fenster entdeckt werden – diese Fehlerklasse wird nun automatisiert geprüft.

## Dauerhafter Entwicklungsstandard

Künftige Partcounter-Revisionen müssen den R001.24-Layout-Smoke-Test weiterhin bestehen. Neue Fenster und Views gelten nicht als fertig, wenn sie auf einem der definierten Viewports einen nicht erreichbaren Überlauf erzeugen.
