# Partcounter R001.25 – manueller VE-Wechsel fail-safe

## Ziel
Ein manueller VE-Wechsel verändert die tatsächlich produzierte Menge einer VE und darf deshalb niemals mit einer veralteten Grenzplanung weiterlaufen.

## Echtbetriebssequenz
1. Partcounter sendet `PauseCounting` und wartet auf Ack.
2. Erst nach bestätigter Pause wird `ManualVeChange` gesendet; das Manual-Kommando trägt das Pause-Bit weiterhin.
3. Bis zum eindeutig erkannten manuellen `CompletionSequence`-Ereignis wird keine Zählfreigabe erteilt.
4. Nach dem Abschluss berechnet Partcounter aus Auftragsrest, Kavitäten und aktueller VE-Nummer ein neues Ziel und `HoldAfterVeNumber`.
5. Ziel/Hold werden mit `pauseCounting=true` geschrieben und vollständig bestätigt.
6. Nur bei zuvor laufendem Auftrag folgt ein bestätigtes Resume.

## Unsichere Kommunikation
Ist die Pause bestätigt, aber die Antwort auf den manuellen Wechsel geht verloren, bleibt der Auftrag gesperrt. Ein später eindeutig erkanntes Manual-Completion-Ereignis kann die Neuplanung abschließen. Ohne eindeutigen Abschluss ist ein normales Resume gesperrt; es ist ein kontrollierter Reset oder Abbruch erforderlich.

Ein unerwarteter manueller VE-Abschluss ohne zuvor bestätigte Pause erzeugt `SAFETY_VE_BOUNDARY_STOP`.
