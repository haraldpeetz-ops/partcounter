# Partcounter R001.10 – Etiketteneditor / Label Designer

## Ziel

R001.10 ersetzt das bisher fest im Programmcode hinterlegte Etikettenlayout durch ein editierbares Vorlagensystem mit WYSIWYG-Vorschau.

## Grundprinzip

Der Druckpfad löst die Vorlage in dieser Reihenfolge auf:

1. Vorlage mit passender Artikelzuordnung,
2. globale Standardvorlage,
3. interne kompatible Partcounter-Standardvorlage als Fallback.

Damit bleibt der bisherige Etikettendruck ohne Benutzeraktion funktionsfähig.

## Vorlagen

Jede Vorlage besitzt:

- eindeutige ID,
- Name,
- Breite und Höhe in mm,
- Kennzeichen `Standard`,
- optionale Artikelnummer,
- beliebig angeordnete Etikettenelemente,
- Änderungszeitpunkt.

Die Vorlagen werden in SQLite in der Tabelle `LabelTemplates` gespeichert. Die komplette Definition liegt zusätzlich als JSON vor, damit spätere Elementtypen ohne starres Datenbankschema ergänzt werden können.

## Unterstützte Elemente

- statischer Text,
- dynamischer Text / Datenfeld,
- QR-Code,
- Code128,
- Rechteck / Rahmen,
- Linie.

## Elementparameter

- X-Position [mm],
- Y-Position [mm],
- Breite [mm],
- Höhe [mm],
- Inhalt bzw. Datenformat,
- Schriftfamilie,
- Schriftgröße [pt],
- Fett,
- Kursiv,
- Unterstrichen,
- Textausrichtung links / zentriert / rechts,
- Rahmen-/Linienstärke,
- Z-Reihenfolge.

Elemente können direkt mit der Maus auf der Vorschau verschoben werden. Für reproduzierbare Industrielayouts können alle Positionen zusätzlich exakt in Millimetern eingegeben werden.

## Format-Presets

- A5 quer: 210 × 148 mm,
- A6 quer: 148 × 105 mm,
- 100 × 50 mm,
- 100 × 100 mm,
- 150 × 100 mm,
- freie Größe von 20 × 20 bis 500 × 500 mm.

## Datenplatzhalter

| Platzhalter | Inhalt |
|---|---|
| `{{VE_ID}}` | eindeutige VE-ID |
| `{{MachineNumber}}` | Maschinennummer |
| `{{MachineName}}` | Maschinenname |
| `{{VeNumber}}` | VE-Nummer |
| `{{OrderNumber}}` | Auftragsnummer |
| `{{ArticleNumber}}` | Artikelnummer |
| `{{ArticleDescription}}` | Artikelbezeichnung |
| `{{ToolNumber}}` | Werkzeugnummer |
| `{{Cavities}}` | aktive Kavitäten |
| `{{TargetQuantity}}` | VE-Sollmenge |
| `{{ActualQuantity}}` | tatsächliche VE-Menge |
| `{{Overfill}}` | zyklusbedingte Mehrmenge |
| `{{CompletionReason}}` | automatisch / manuell |
| `{{CompletedDate}}` | Fertigstellungsdatum |
| `{{CompletedTime}}` | Fertigstellungszeit |
| `{{CompletedAt}}` | Datum + Uhrzeit |
| `{{QrPayload}}` | vollständige Partcounter-QR-Nutzlast |

Platzhalter dürfen auch mit statischem Text kombiniert werden, z. B.:

`Artikel: {{ArticleNumber}}`

oder

`Menge {{ActualQuantity}} Stück / VE {{VeNumber}}`

## QR- und Barcode-Felder

QR- und Code128-Elemente verwenden denselben Platzhaltermechanismus. Standard:

- QR-Code: `{{QrPayload}}`
- Code128: `{{VE_ID}}`

## Partcounter-Standardvorlage

Beim ersten Start von R001.10 wird automatisch die Vorlage `Partcounter Standard` erzeugt, wenn noch keine LabelTemplates vorhanden sind. Sie bildet das bisherige feste R001.9-Etikett ab:

- Partcounter-Kopf,
- VE/Maschine,
- Artikel,
- Beschreibung,
- Auftrag,
- Werkzeug/Kavitäten,
- Menge,
- VE-Soll/Mehrmenge,
- Fertigstellungszeit,
- VE-ID,
- QR-Code,
- Code128.

## Testdruck

Der Editor kann die aktuell sichtbare Arbeitskopie direkt als Testetikett an den in Partcounter hinterlegten Windows-Drucker senden. Eine Vorlage muss dafür noch nicht gespeichert sein.

Für die Vorschau werden, wenn vorhanden, die aktuell im Leitstand ausgewählte Maschine und der ausgewählte Artikel verwendet. Andernfalls erzeugt der Editor reproduzierbare Musterdaten.

## Automatischer Produktionsdruck

Der bestehende automatische VE-Druck verwendet ab R001.10 dieselbe Rendering-Engine wie der Editor. Dadurch entsprechen Vorschau und Produktivdruck demselben Vorlagenmodell.

## Erweiterungspfad

Für spätere Revisionen vorbereitet bzw. ohne Architekturbruch ergänzbar:

- Kundenlogos / Bitmap-Bilder,
- GS1-128 / DataMatrix,
- kundenbezogene Vorlagenzuordnung,
- Vorlagenrevisionen und Freigabeworkflow,
- Import/Export von Vorlagen,
- Sperren freigegebener Produktionsvorlagen,
- Drucker-/Materialprofile,
- Etikettenjournal mit Vorlagenrevision.
