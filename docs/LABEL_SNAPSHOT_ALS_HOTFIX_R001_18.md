# Partcounter R001.18 – Etiketten-Layout-Snapshot & ARBURG-ALS-Hotfix

Stand: 28.08.2026
Branch: `r001.18-label-snapshot-als-hotfix`
Version: `0.1.18`

## 1. ARBURG ALS – Fehlerkorrektur Feldmapping

### Beobachteter Fehler

Beim Öffnen/Benutzen des Reiters `ARBURG ALS -> Feldmapping / Maschinenzuordnung` konnte WPF folgende Exception auslösen:

`TwoWay- oder OneWayToSource-Bindungen funktionieren nicht mit der schreibgeschützten Eigenschaft 'Required' vom Typ 'Partcounter.Models.AlsFieldMapping'.`

### Ursache

Die Spalte `Pflicht` ist rein informativ. `AlsFieldMapping.Required` besitzt absichtlich keinen Setter. Eine `DataGridCheckBoxColumn` kann jedoch ohne expliziten Binding-Modus versuchen, eine TwoWay-Bindung aufzubauen.

### Korrektur

`AlsIntegrationView.xaml.cs` normalisiert die Bindungen der schreibgeschützten Mapping-Felder bereits unmittelbar nach `InitializeComponent()` auf `BindingMode.OneWay`, bevor der ViewModel-DataContext gesetzt wird.

Explizit geschützt:

- `Required`
- `TargetField`
- `Description`
- `Example`

`SourceField` bleibt editierbar und behält seine TwoWay-Funktion.

Es wurde **nicht** das Datenmodell künstlich beschreibbar gemacht. Die fachliche Schreibschutz-Semantik bleibt erhalten.

## 2. Historischer Etiketten-Layout-Snapshot

### Ziel

Ein späterer Reprint soll nicht nur dieselben historischen VE-Daten verwenden, sondern – für ab R001.18 regulär gedruckte VE – auch die damals tatsächlich aufgelöste Etikettenvorlage.

### Speicherung

Neue SQLite-Tabelle:

`LabelPrintSnapshots`

Gespeichert werden pro VE-ID:

- Template-ID
- Template-Name
- `TemplateUpdatedAtUtc`
- komplette serialisierte `LabelTemplateDefinition`
- eingebettete Bild-/Logo-Daten aus der Vorlage
- SHA-256 über die gespeicherte JSON-Definition
- Snapshot-Zeitpunkt

Die VE-ID ist Primärschlüssel. Ein vorhandener Snapshot wird nicht durch spätere Layoutänderungen ersetzt.

### Erstdruck

Beim regulären VE-Druck löst `LabelPrintService` zunächst die zu verwendende Vorlage auf und versucht, diese vor dem Rendern als Snapshot zu archivieren.

Der Produktionsdruck selbst wird nicht blockiert, falls die Snapshot-Speicherung ausnahmsweise fehlschlägt. In diesem Fall bleibt der Nachdruck später transparent als Fallback gekennzeichnet.

Testetiketten erhalten keinen Produktions-Snapshot.

## 3. Reprint-Verhalten

Beim Nachdruck gilt:

1. Partcounter lädt die historischen VE-Daten aus `PackagingUnits`.
2. Partcounter sucht den Snapshot der VE-ID.
3. Ist ein Snapshot vorhanden, wird dessen SHA-256 geprüft.
4. Bei gültigem Snapshot wird exakt diese archivierte Vorlagendefinition gerendert.
5. Bei älteren VE ohne Snapshot wird das aktuell aufgelöste Layout verwendet.
6. Die verwendete Layoutquelle wird im Nachdruckjournal protokolliert.

Ein beschädigter Snapshot wird nicht stillschweigend verwendet.

## 4. Nachdruckjournal

`LabelReprintJournal` erhält migrationssicher die zusätzliche Spalte:

`LayoutSource`

Beispiele:

- `Original-Snapshot: Versandetikett · abc123 · SHA256 4d91c3...`
- `Fallback aktuelles Layout: Versandetikett · abc123 · kein historischer Snapshot verfügbar`

Das Journalfenster zeigt die Layoutquelle je Nachdruckversuch an.

## 5. Bedienoberfläche VE-Historie

Bei Auswahl einer VE wird angezeigt:

- Anzahl erfolgreicher Nachdrucke
- Originaldruckzeit
- vorhandener Original-Layout-Snapshot samt Kurz-Hash
- oder Hinweis, dass es sich um eine ältere VE ohne Snapshot handelt

Damit weiß der Bediener **vor** dem Reprint, ob ein historisches Layout oder ein Fallback verwendet wird.

## 6. Unverändert

R001.18 verändert nicht:

- Modbus Protocol V2
- Partcounter_LOGO_V001
- Zykluszählung
- VE-Wechslerlogik
- Q1-Ansteuerung
- Auftragslogik
- VE-ID oder historische Produktionsmengen

Der Snapshot betrifft ausschließlich die Reproduzierbarkeit des Etikettenlayouts.
