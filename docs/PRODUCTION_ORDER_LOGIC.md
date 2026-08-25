# Partcounter R001.5 – Produktionsauftragslogik

## Zustände pro Maschine

Jede Maschine besitzt einen expliziten Auftragszustand:

- `None` – kein Auftrag
- `Running` – Auftrag läuft
- `Paused` – Auftrag pausiert
- `Completed` – Sollmenge automatisch erreicht
- `Ended` – Auftrag manuell beendet

Maschinen ohne aktiven Auftrag werden im Leitstand standardmäßig ausgeblendet. Über **„Maschinen ohne Auftrag anzeigen“** können sie eingeblendet werden.

## Temporäre Deaktivierung

Eine Maschine kann unabhängig vom Auftragszustand temporär deaktiviert werden.

Im Simulationsbetrieb:
- keine automatische Zykluszählung,
- Kachel wird aus den aktiven Ansichten entfernt.

Im Modbus-Echtbetrieb:
- laufender Auftrag wird zuerst pausiert,
- Polling zur betreffenden LOGO!-Station wird angehalten,
- Kachel wird aus Leitstand und Mini-Monitor entfernt.

Beim Reaktivieren wird die Kommunikation wieder zugelassen. Ein pausierter Auftrag wird absichtlich nicht automatisch fortgesetzt.

## Auftragsstart

Der Bediener wählt:

- Maschine
- Artikel
- Auftragsnummer
- Gesamt-Auftragsmenge
- Ventilimpuls

Der Artikel liefert:

- Werkzeugnummer
- Kavitätenzahl
- Standard-VE-Menge

Die erste VE wird berechnet als:

```text
CurrentVETarget = min(StandardVE, Auftragsmenge)
TargetCycles     = ceil(CurrentVETarget / Kavitäten)
```

## Auftragsfortschritt

Pro Maschine werden angezeigt:

- Auftragszustand
- Auftragsnummer
- Sollmenge
- produzierte Istmenge
- Restmenge
- Fortschritt in %
- abgeschlossene VE
- ungefähr benötigte VE

Die Istmenge basiert auf vollständigen Werkzeugzyklen. Sie kann daher die Sollmenge geringfügig überschreiten.

## Letzte VE

Nach jedem abgeschlossenen Behälter wird die Restmenge neu berechnet.

```text
Rest = Auftragsmenge - produzierte Istmenge

wenn Rest > 0:
    CurrentVETarget = min(StandardVE, Rest)
    TargetCycles = ceil(CurrentVETarget / Kavitäten)
```

Ist die Restmenge kleiner als die Standard-VE, wird die letzte VE entsprechend verkleinert.
Die tatsächliche Stückzahl bleibt dennoch ein Vielfaches der Kavitätenzahl.

Beispiel:

```text
Auftrag            10.260 Stück
Standard-VE         1.000 Stück
Kavitäten              64

nach 10 VE:
produziert          10.240 Stück
Rest                    20 Stück

letzte VE:
Soll                    20 Stück
Zyklen                    1
tatsächlich              64 Stück
Auftrags-Ist         10.304 Stück
```

## Auftrag abgeschlossen

Sobald:

```text
OrderProducedQuantity >= OrderTargetQuantity
```

wird der Auftrag `Completed`.

Im Echtbetrieb sendet Partcounter anschließend einen Pause-Befehl an die LOGO!, damit keine neue VE begonnen wird.
Der neue Auftrag wird später mit Reset übernommen.

## Mini-Monitor

Wird das Hauptfenster minimiert, erscheint automatisch ein kleines Always-on-Top-Fenster.

Es zeigt nur:

- laufende Aufträge
- pausierte Aufträge
- Maschinen mit aktueller VE-Vollmeldung

Je Maschine:

- Maschinennummer
- Artikel
- Auftragsnummer
- VE-Füllgrad
- Auftragsfortschritt
- verbleibende Zyklen

Doppelklick auf eine Maschine stellt das Hauptfenster wieder her und fokussiert die betreffende Kachel.
