# Partcounter R001.25 HF7 – Commissioning Navigation Fix

## Fehlerbild

Nach dem Programmstart erschien zeitversetzt die Meldung, der Bereich `Inbetriebnahme / Diagnose` sei nicht gefunden worden. Sie konnte zeitlich mit dem Aktivieren des Echtbetriebs zusammenfallen, war aber kein Modbus- oder LOGO!-Fehler.

## Ursache

Der Administrations-Hub verschiebt den Inbetriebnahme-Reiter aus dem Haupt-TabControl in einen verschachtelten Administrations-TabControl. Die Live-Abnahme suchte weiterhin ausschließlich in der ursprünglichen Haupt-Tabliste und konnte die bereits vorhandene Ansicht deshalb nicht mehr finden.

## Korrektur

- `MainWindow` stellt die erzeugte `CommissioningView` intern über eine stabile Referenz bereit.
- Der innere TabControl der Inbetriebnahmeansicht besitzt einen eindeutigen Namen.
- `LiveCommissioningBootstrap` hängt die Live-Abnahme direkt an diese Ansicht an und ist nicht mehr von Position, Sichtbarkeit oder Realisierung des Reiters abhängig.
- Das Layout-Gate wartet ausdrücklich darauf, dass der Reiter `Live-Abnahme` tatsächlich angelegt wurde. Eine erneute Navigationsregression lässt die automatisierte Prüfung damit fehlschlagen.

Die Modbus-Transporthärtung aus HF6 und das LOGO!-Protocol V3 bleiben unverändert.
