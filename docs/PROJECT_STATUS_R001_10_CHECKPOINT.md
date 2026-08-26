# Partcounter – Projektstatus R001.10 Checkpoint

**Revision:** R001.10 – WYSIWYG Label Designer  
**Branch:** `r001.10-label-editor`  
**Pull Request:** #4  
**Basis:** vollständiger R001.9 Commissioning/Diagnostics + LOGO V001 / Modbus V2

## Hardwarestatus

Die Hardwarearchitektur der Referenzmaschine 01 bleibt gegenüber R001.9 unverändert und gilt für den Pilotversuch als eingefroren:

- Siemens LOGO! `6ED1052-2MD08-0BA2` / LOGO! 12/24RCEo,
- 24 V DC,
- I1 = 24-V-Maschinenzyklus,
- Q1 = 24-V-Koppel-/Interface-Relais → kleines 24-V-Festo-Ventil,
- I2-Endlagenüberwachung Station 01 = AUS, aber vorbereitet,
- Ventilimpuls 50…5000 ms / 10-ms-Raster / Standard 750 ms,
- Modbus ProtocolVersion 2.

Der Etiketteneditor verändert keine LOGO!-Register, Hardwarelogik oder Inbetriebnahmeparameter.

## R001.10 – Etiketteneditor

Neu implementiert:

- eigener Reiter `Etiketteneditor`,
- WYSIWYG-Canvas,
- Elemente per Maus frei verschiebbar,
- X/Y/Breite/Höhe zusätzlich exakt in mm,
- freie Etikettengröße 20…500 mm,
- Format-Presets A5 quer, A6 quer, 100×50, 100×100, 150×100,
- statischer Text,
- dynamischer Text,
- QR-Code,
- Code128,
- Rechtecke / Rahmen,
- Linien,
- Schriftfamilie,
- Schriftgröße,
- Fett / Kursiv / Unterstrichen,
- Textausrichtung links / Mitte / rechts,
- Linien-/Rahmenstärke,
- Z-Reihenfolge.

## Datenfelder

Unterstützte Platzhalter:

- `{{VE_ID}}`
- `{{MachineNumber}}`
- `{{MachineName}}`
- `{{VeNumber}}`
- `{{OrderNumber}}`
- `{{ArticleNumber}}`
- `{{ArticleDescription}}`
- `{{ToolNumber}}`
- `{{Cavities}}`
- `{{TargetQuantity}}`
- `{{ActualQuantity}}`
- `{{Overfill}}`
- `{{CompletionReason}}`
- `{{CompletedDate}}`
- `{{CompletedTime}}`
- `{{CompletedAt}}`
- `{{QrPayload}}`

Statischer Text und Platzhalter können frei kombiniert werden.

## Vorlagenverwaltung

SQLite-Tabelle `LabelTemplates`:

- Vorlagen-ID,
- Name,
- Breite / Höhe,
- Standardkennzeichen,
- optionale Artikelzuordnung,
- vollständige JSON-Definition,
- Änderungszeitpunkt.

Regeln:

1. maximal eine aktive globale Standardvorlage,
2. maximal eine aktive Artikelvorlage je Artikelnummer,
3. Druckauflösung: Artikelvorlage → Standardvorlage → interner Fallback,
4. beim ersten Start ohne Vorlagen wird automatisch `Partcounter Standard` erzeugt,
5. diese Standardvorlage bildet das frühere feste Partcounter-Etikett nach.

## Druckintegration

- Editor und Produktionsdruck verwenden `LabelRenderService`,
- `LabelPrintService` löst die passende Vorlage vor jedem Druck auf,
- bestehender automatischer VE-Druck bleibt erhalten,
- Testdruck ist direkt mit der aktuell sichtbaren, noch nicht gespeicherten Arbeitskopie möglich,
- QR und Code128 werden weiterhin mit ZXing erzeugt.

## Kompatibilität

Bestehende Installationen besitzen noch keine `LabelTemplates`-Tabelle. R001.10 erzeugt sie automatisch beim ersten Zugriff. Es ist keine manuelle Datenbankmigration erforderlich.

## Nächste praktische Schritte

1. R001.10 auf Windows öffnen und Etiketteneditor visuell prüfen.
2. Standardvorlage mit vorhandenem Etikettendrucker testdrucken.
3. Beispielvorlage für einen realen Artikel anlegen und Artikelzuordnung testen.
4. Morgen unabhängig davon Station 01 an der realen LOGO! gemäß R001.9-Inbetriebnahmeplan testen.
5. Nach Pilotversuch LOGO V001 als Golden Master einfrieren.

## Spätere Label-Erweiterungen

Architektonisch vorbereitet:

- Bild/Logo-Element,
- DataMatrix / GS1,
- Vorlagenimport/-export,
- Kunden-/Lieferantenzuordnung,
- Vorlagenrevision und Freigabeworkflow,
- Drucker-/Materialprofile,
- Etikettenjournal mit verwendeter Vorlagenrevision.
